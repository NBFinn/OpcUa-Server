using Opc.Ua;
using Opc.Ua.Configuration;
using ServerConfiguration = OpcUA_Server.ServerConfiguration;
using OpcUA_Server;

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) => { args.Cancel = true; shutdown.Cancel(); };
TestServer? server = null;
try
{
    ServerSettings settings = ServerSettings.Load(args);
    string certificateRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpcUaServer", "pki", $"net{Environment.Version.Major}", $"process{Environment.ProcessId}");
    int port = PortSelection.FindAvailable(settings.Port, "OPC UA");
    for (int attempt = 0; ; attempt++)
    {
        string address = new UriBuilder("opc.tcp", settings.Host, port,
            $"/{settings.Name}Simulator").Uri.AbsoluteUri;
        ApplicationConfiguration configuration = ServerConfiguration.Create(settings.Name, address, certificateRoot);
        var application = new ApplicationInstance(configuration, ServerConfiguration.Telemetry);
        if (!await application.CheckApplicationInstanceCertificatesAsync(true, 60))
            throw new InvalidOperationException("Could not create the server certificate.");
        await configuration.ValidateAsync(ApplicationType.Server);
        var candidate = new TestServer(settings);
        try
        {
            await application.StartAsync(candidate);
            server = candidate;
            Console.WriteLine($"OPC-UA: {address}");
            Console.WriteLine($"Nodes: {settings.Nodes.Count}. Press Ctrl+C to stop.");
            break;
        }
        catch (Exception exception) when (attempt < 31 &&
            (PortSelection.IsAddressInUse(exception) || !PortSelection.IsAvailable(port)))
        {
            candidate.Dispose();
            Console.WriteLine($"OPC UA: port {port} became occupied during startup; retrying.");
            port = PortSelection.FindAvailable(PortSelection.Next(port), "OPC UA");
        }
        catch { candidate.Dispose(); throw; }
    }
    await Task.Delay(Timeout.Infinite, shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
catch (Exception exception) { Console.Error.WriteLine(exception.Message); Environment.ExitCode = 1; }
finally
{
    if (server is not null) { await server.StopAsync(); server.Dispose(); }
}
