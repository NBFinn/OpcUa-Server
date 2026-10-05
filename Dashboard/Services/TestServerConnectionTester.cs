using TestOPCUA_WebApp.Models;

using System.Net.Http.Json;

namespace TestOPCUA_WebApp.Services;

public sealed class TestServerConnectionTester
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);
    private readonly HttpClient httpClient;

    public TestServerConnectionTester(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<(bool Success, string Message)> TestAsync(
        TestServerConfiguration server,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            string url = $"{server.ApiAddress.TrimEnd('/')}/api/opcuaclients";
            using HttpResponseMessage response = await httpClient.GetAsync(
                url,
                timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return (
                    false,
                    $"The API returned HTTP {(int)response.StatusCode}.");
            }
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OperationCanceledException)
        {
            return (
                false,
                $"The API at {server.ApiAddress} is unreachable.");
        }

        return (true, "The API is reachable.");
    }

    public async Task<string> GetStatusAsync(
        TestServerConfiguration server,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            string url = $"{server.ApiAddress.TrimEnd('/')}/api/opcuaclients";
            using HttpResponseMessage response = await httpClient.GetAsync(url, timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return "Unavailable";
            }

            var results = await response.Content.ReadFromJsonAsync<List<ClientStatus>>(cancellationToken: timeout.Token);
            var result = results?.FirstOrDefault(item => string.Equals(item.Device, server.Name, StringComparison.OrdinalIgnoreCase));
            return result is null ? "Unavailable" : "Connected";
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OperationCanceledException)
        {
            return "API unreachable";
        }
    }

    private sealed class ClientStatus
    {
        public string? Device { get; set; }
        public string? Mode { get; set; }
    }
}

