using System.Text.Json;
using TestOPCUA_WebApp.Models;

namespace TestOPCUA_WebApp.Services;

public class TestServerNodeReader(HttpClient httpClient)
{
    public async Task<IReadOnlyList<string>> GetNodesAsync(
        TestServerConfiguration server, CancellationToken cancellationToken)
    {
        string url = $"{server.ApiAddress.TrimEnd('/')}/api/opcuaclients/values?device={Uri.EscapeDataString(server.Name)}";
        try
        {
            using JsonDocument values = await httpClient.GetFromJsonAsync<JsonDocument>(url, cancellationToken)
                ?? throw new InvalidOperationException("Keine Variablenantwort.");
            return values.RootElement.EnumerateArray()
                .Where(value => value.GetProperty("writable").GetBoolean())
                .Select(value => value.GetProperty("nodeId").GetString()!)
                .ToList();
        }
        catch (HttpRequestException) { return []; }
    }
}

