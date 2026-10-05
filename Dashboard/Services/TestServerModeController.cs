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
            return (false, "Bitte einen gültigen Modus auswählen.");
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
                        return (false, message.GetString() ?? "Modus konnte nicht geändert werden.");
                }
                catch (JsonException) { }
                return (false, $"Moduswechsel fehlgeschlagen: HTTP {(int)response.StatusCode}.");
            }
            return (true, $"{server.Name}: {mode} ist aktiv.");
        }
        catch (HttpRequestException) { return (false, $"Server '{server.Name}' ist nicht erreichbar."); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { return (false, "Der Server hat den Moduswechsel nicht rechtzeitig bestätigt."); }
    }

    private sealed class ServerState
    {
        public string? Device { get; set; }
        public string? Mode { get; set; }
    }
}
