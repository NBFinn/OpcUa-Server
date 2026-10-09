using Opc.Ua;
using Opc.Ua.Configuration;

namespace OpcUA_Server;

/// <summary>In-process OPC UA server. The caller owns its lifetime.</summary>
public sealed class OpcUaServer : IAsyncDisposable
{
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    private readonly string configurationPath;
    private readonly string certificateRoot;
    private readonly Action<string>? log;
    private TestServer? server;
    private bool disposed;

    public string? EndpointUrl { get; private set; }
    public int? Port { get; private set; }
    public bool IsRunning => server is not null;

    public OpcUaServer(string? configurationPath = null, Action<string>? log = null)
    {
        this.configurationPath = Path.GetFullPath(configurationPath ??
            Path.Combine(AppContext.BaseDirectory, "server.json"));
        this.log = log;
        certificateRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpcUaServer", "pki", $"net{Environment.Version.Major}",
            $"instance{Guid.NewGuid().ToString("N")[..12]}");
    }

    /// <summary>Starts the listener and returns its actual endpoint. Repeated calls while running return the same endpoint.</summary>
    public async Task<string> StartAsync(int? preferredPort = null, CancellationToken cancellationToken = default)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (disposed) throw new ObjectDisposedException(nameof(OpcUaServer));
            if (server is not null) return EndpointUrl!;
            ServerSettings settings = ServerSettings.Load(configurationPath, preferredPort);
            int port = PortSelection.FindAvailable(settings.Port, "OPC UA", log);
            for (int attempt = 0; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string address = new UriBuilder("opc.tcp", settings.Host, port,
                    $"/{settings.Name}Simulator").Uri.AbsoluteUri;
                ApplicationConfiguration configuration = ServerConfiguration.Create(settings.Name, address, certificateRoot);
                var application = new ApplicationInstance(configuration, ServerConfiguration.Telemetry);
                if (!await application.CheckApplicationInstanceCertificatesAsync(true, 60))
                    throw new InvalidOperationException("Could not create the server certificate.");
                await configuration.ValidateAsync(ApplicationType.Server);
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = new TestServer(settings);
                try
                {
                    await application.StartAsync(candidate);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        await candidate.StopAsync();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    server = candidate;
                    EndpointUrl = address;
                    Port = port;
                    return address;
                }
                catch (Exception exception) when (exception is not OperationCanceledException && attempt < 31 &&
                    (PortSelection.IsAddressInUse(exception) || !PortSelection.IsAvailable(port)))
                {
                    candidate.Dispose();
                    log?.Invoke($"OPC UA: port {port} became occupied during startup; retrying.");
                    port = PortSelection.FindAvailable(PortSelection.Next(port), "OPC UA", log);
                }
                catch { candidate.Dispose(); throw; }
            }
        }
        finally { lifecycle.Release(); }
    }

    /// <summary>Stops the listener. A stopped instance can be started again.</summary>
    public async Task StopAsync()
    {
        await lifecycle.WaitAsync();
        try { await StopCoreAsync(); }
        finally { lifecycle.Release(); }
    }

    private async Task StopCoreAsync()
    {
        TestServer? running = server;
        server = null;
        EndpointUrl = null;
        Port = null;
        if (running is null) return;
        try { await running.StopAsync(); }
        finally { running.Dispose(); }
    }

    /// <summary>Stops the listener and permanently prevents this instance from being started again.</summary>
    public async ValueTask DisposeAsync()
    {
        await lifecycle.WaitAsync();
        try
        {
            if (disposed) return;
            disposed = true;
            await StopCoreAsync();
        }
        finally { lifecycle.Release(); }
    }
}
