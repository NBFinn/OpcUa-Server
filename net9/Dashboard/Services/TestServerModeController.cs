using System.Net.Http.Json;
using System.Text.Json;
using TestOPCUA_WebApp.Models;

namespace TestOPCUA_WebApp.Services;

public sealed class TestServerModeController(HttpClient httpClient)
{
    public async Task<string> GetModeAsync(TestServerConfiguration server, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var states = await httpClient.GetFromJsonAsync<List<ServerState>>(
                $"{server.ApiAddress.TrimEnd('/')}/api/opcuaclients", timeout.Token);
            return states?.FirstOrDefault(state =>
                string.Equals(state.Device, server.Name, StringComparison.OrdinalIgnoreCase))?.Mode ?? "";
        }
        catch (HttpRequestException) { return ""; }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return ""; }
    }

    public async Task<(bool Success, string Message)> SetModeAsync(
        TestServerConfiguration server, string mode, string? scenarioPath, CancellationToken token)
    {
        if (mode is not ("Static" or "Manual" or "Cyclic" or "Scenario"))
            return (false, "Select a valid mode.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"{server.ApiAddress.TrimEnd('/')}/api/opcuaclients/mode?device={Uri.EscapeDataString(server.Name)}",
                new { mode, scenarioPath = string.IsNullOrWhiteSpace(scenarioPath) ? null : scenarioPath.Trim() },
                timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(timeout.Token);
                try
                {
                    using var error = JsonDocument.Parse(body);
                    if (error.RootElement.TryGetProperty("error", out var message))
                        return (false, message.GetString() ?? "Could not change the mode.");
                }
                catch (JsonException) { }
                return (false, $"Mode change failed: HTTP {(int)response.StatusCode}.");
            }
            return (true, $"{server.Name}: {mode} is active.");
        }
        catch (HttpRequestException) { return (false, $"Server '{server.Name}' is unreachable."); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return (false, "The server did not confirm the mode change in time."); }
    }

    private sealed class ServerState
    {
        public string? Device { get; set; }
        public string? Mode { get; set; }
    }
}
