using System.Text.Json;
using TestOPCUA_WebApp.Models;

namespace TestOPCUA_WebApp.Services;

public class TestServerConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string filePath;
    private readonly SemaphoreSlim fileLock = new(1, 1);
    private readonly string? endpointFile = Environment.GetEnvironmentVariable("OPCUA_ENDPOINT_FILE");

    public TestServerConfigurationStore(IWebHostEnvironment environment)
    {
        filePath = Path.Combine(environment.ContentRootPath, "Data", "testservers.json");
    }

    public async Task<IReadOnlyList<TestServerConfiguration>> GetAllAsync()
    {
        await fileLock.WaitAsync();
        try { return await LoadAsync(); }
        finally { fileLock.Release(); }
    }

    public async Task<TestServerConfiguration?> GetAsync(string name)
    {
        IReadOnlyList<TestServerConfiguration> servers = await GetAllAsync();
        return servers.FirstOrDefault(server =>
            server.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SaveAsync(TestServerConfiguration server)
    {
        await fileLock.WaitAsync();
        try
        {
            List<TestServerConfiguration> servers = await LoadAsync(applyRunningEndpoint: false);
            int index = servers.FindIndex(item =>
                item.Name.Equals(server.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) servers[index] = server;
            else servers.Add(server);
            await SaveAllAsync(servers);
        }
        finally { fileLock.Release(); }
    }

    public async Task DeleteAsync(string name)
    {
        await fileLock.WaitAsync();
        try
        {
            List<TestServerConfiguration> servers = await LoadAsync(applyRunningEndpoint: false);
            servers.RemoveAll(server =>
                server.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            await SaveAllAsync(servers);
        }
        finally { fileLock.Release(); }
    }

    private async Task<List<TestServerConfiguration>> LoadAsync(bool applyRunningEndpoint = true)
    {
        if (!File.Exists(filePath)) return [];
        await using FileStream stream = File.OpenRead(filePath);
        List<TestServerConfiguration> servers = await JsonSerializer.DeserializeAsync<List<TestServerConfiguration>>(
            stream, JsonOptions) ?? [];
        // The host supplies a unique file for this launch. Do not rewrite saved entries.
        if (applyRunningEndpoint && !string.IsNullOrWhiteSpace(endpointFile) && File.Exists(endpointFile))
        {
            TestServerConfiguration? running = JsonSerializer.Deserialize<TestServerConfiguration>(
                await File.ReadAllTextAsync(endpointFile));
            TestServerConfiguration? saved = servers.FirstOrDefault(item =>
                item.Name.Equals(running?.Name, StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(item.ApiAddress, UriKind.Absolute, out Uri? address) && address.IsLoopback);
            if (saved is not null && running is not null) saved.ApiAddress = running.ApiAddress;
        }
        return servers;
    }

    private async Task SaveAllAsync(List<TestServerConfiguration> servers)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using FileStream stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(
            stream, servers.OrderBy(server => server.Name), JsonOptions);
    }
}
