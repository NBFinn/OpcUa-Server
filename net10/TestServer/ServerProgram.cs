using Automation.Simulator.TestServer.Simulations;
using Opc.Ua;
using Opc.Ua.Configuration;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace Server;

public static partial class ServerProgram
{
    private const string NamespaceUri = "urn:testserver:nodes";
    private const string NodeResourceName = "GatewayNodeResources";
    private static readonly SemaphoreSlim LifecycleLock = new(1, 1);
    private static ApplicationInstance? application;
    private static TestStandardServer? server;
    private static SimulatorProfile? activeProfile;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static async Task Main(string[] args)
    {
        activeProfile = SimulatorProfile.Parse(args);
        using CancellationTokenSource shutdown = new();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            shutdown.Cancel();
        };

        try
        {
            SimulationRuntime.ConfigureProfile(
                activeProfile.Name,
                activeProfile.RestPort,
                activeProfile.DefaultScenario);
            await StartAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
        }
        catch (OperationCanceledException)
        {

        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
        }
        finally
        {
            await StopAsync();
        }
    }

    public static async Task StartAsync()
    {
        await LifecycleLock.WaitAsync();

        try
        {
            if (server is not null)
            {
                return;
            }

            SimulatorProfile profile = activeProfile ?? throw new InvalidOperationException(
                "No simulator profile was selected.");
            profile = profile with { OpcPort = PortSelection.FindAvailable(profile.OpcPort, "OPC UA") };
            string pkiRoot = DeleteServerCertificates(profile.Name);

            string serverIp = GetPrimaryIpv4Address();
            ApplicationConfiguration configuration =
                CreateConfiguration(serverIp, pkiRoot, profile);

            var newApplication = new ApplicationInstance(configuration, null)
            {
                ApplicationName = configuration.ApplicationName,
                ApplicationType = ApplicationType.Server,
                ApplicationConfiguration = configuration
            };

            bool certificateOk =
                await newApplication.CheckApplicationInstanceCertificatesAsync(true, 60);

            if (!certificateOk)
            {
                throw new InvalidOperationException(
                    "Could not create the OPC UA certificate.");
            }

            await configuration.ValidateAsync(ApplicationType.Server);

            // A free-port probe is advisory. Retry if another process wins the actual bind.
            for (int attempt = 0; ; attempt++)
            {
                configuration.ServerConfiguration.BaseAddresses.Clear();
                configuration.ServerConfiguration.BaseAddresses.Add(
                    $"opc.tcp://{serverIp}:{profile.OpcPort}/{profile.Name}Simulator");
                var newServer = new TestStandardServer();

                try
                {
                    await newApplication.StartAsync(newServer);
                    application = newApplication;
                    server = newServer;
                    activeProfile = profile;
                    Console.WriteLine($"OPC-UA: opc.tcp://{serverIp}:{profile.OpcPort}/{profile.Name}Simulator");
                    await SimulationRuntime.StartAsync();
                    string? endpointFile = Environment.GetEnvironmentVariable("OPCUA_ENDPOINT_FILE");
                    if (!string.IsNullOrWhiteSpace(endpointFile))
                    {
                        string fullPath = Path.GetFullPath(endpointFile);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                        string temporaryPath = fullPath + ".tmp";
                        await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(new
                        {
                            Name = profile.Name,
                            ApiAddress = $"http://localhost:{SimulationRuntime.RestPort}",
                            OpcAddress = $"opc.tcp://{serverIp}:{profile.OpcPort}/{profile.Name}Simulator"
                        }));
                        File.Move(temporaryPath, fullPath, overwrite: true);
                    }
                    break;
                }
                catch (Exception exception) when (server is null && attempt < 31 &&
                    (PortSelection.IsAddressInUse(exception) || !PortSelection.IsAvailable(profile.OpcPort)))
                {
                    newServer.Dispose();
                    Console.WriteLine($"OPC UA: port {profile.OpcPort} became occupied during startup; retrying.");
                    profile = profile with
                    {
                        OpcPort = PortSelection.FindAvailable(PortSelection.Next(profile.OpcPort), "OPC UA")
                    };
                }
                catch
                {
                    if (server is null) newServer.Dispose();
                    throw;
                }
            }
        }
        finally
        {
            LifecycleLock.Release();
        }
    }

    private static string DeleteServerCertificates(string profileName)
    {
        string pkiBase = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpcUaServer",
            "pki",
            $"net{Environment.Version.Major}",
            $"process{Environment.ProcessId}"));
        string pkiRoot = Path.GetFullPath(Path.Combine(pkiBase, profileName));

        if (!string.Equals(Path.GetDirectoryName(pkiRoot),
                pkiBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid certificate directory for the simulator profile.");
        }

        if (Directory.Exists(pkiRoot))
        {
            Directory.Delete(pkiRoot, recursive: true);
        }

        return pkiRoot;
    }

    public static async Task StopAsync()
    {
        await LifecycleLock.WaitAsync();

        try
        {
            if (server is null)
            {
                return;
            }

            TestStandardServer runningServer = server;
            server = null;
            application = null;

            await SimulationRuntime.StopAsync();
            await runningServer.StopAsync();
            runningServer.Dispose();
        }
        finally
        {
            LifecycleLock.Release();
        }
    }

    private static ApplicationConfiguration CreateConfiguration(
        string serverIp,
        string pkiRoot,
        SimulatorProfile profile)
    {
        return new ApplicationConfiguration
        {
            ApplicationName = $"Demo OPC UA {profile.Name} Simulator",

            ApplicationUri =
                $"urn:{Utils.GetHostName()}:OpcUaSimulator:{profile.Name}",

            ProductUri =
                $"urn:demo:OpcUaSimulator:{profile.Name}",

            ApplicationType = ApplicationType.Server,

            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 15_000
            },

            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses =
                [
                    $"opc.tcp://{serverIp}:{profile.OpcPort}/{profile.Name}Simulator"
                ],

                SecurityPolicies =
                [
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    },
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.SignAndEncrypt,
                        SecurityPolicyUri = SecurityPolicies.Aes256_Sha256_RsaPss
                    },
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.SignAndEncrypt,
                        SecurityPolicyUri = SecurityPolicies.Basic256Sha256
                    }
                ],

                UserTokenPolicies =
                [
                    new UserTokenPolicy
                    {
                        PolicyId = "anonymous",
                        TokenType = UserTokenType.Anonymous,
                        SecurityPolicyUri = SecurityPolicies.None
                    }
                ]
            },

            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = "Directory",
                    StorePath = Path.Combine(
                        pkiRoot,
                        "own"),

                    SubjectName = $"CN=Demo {profile.Name} OPC UA Simulator"
                },

                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = "Directory",
                    StorePath = Path.Combine(pkiRoot, "trusted")
                },

                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = "Directory",
                    StorePath = Path.Combine(pkiRoot, "issuers")
                },

                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = "Directory",
                    StorePath = Path.Combine(pkiRoot, "rejected")
                },

                AutoAcceptUntrustedCertificates = false,
                AddAppCertToTrustedStore = true
            }
        };
    }

    private static string GetPrimaryIpv4Address()
    {
        foreach (NetworkInterface networkInterface in
                 NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            IPInterfaceProperties properties =
                networkInterface.GetIPProperties();

            bool hasIpv4Gateway = properties.GatewayAddresses.Any(
                gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                           !gateway.Address.Equals(IPAddress.Any));

            if (!hasIpv4Gateway)
            {
                continue;
            }

            UnicastIPAddressInformation? address =
                properties.UnicastAddresses.FirstOrDefault(
                    item => item.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(item.Address));

            if (address is not null)
            {
                return address.Address.ToString();
            }
        }

        throw new InvalidOperationException(
            "No active IPv4 address with a default gateway was found.");
    }
}
