using System.Globalization;
using System.Text;
using System.Text.Json;
using TestOPCUA_WebApp.Models;

namespace TestOPCUA_WebApp.Services;

public class TestServerWriter
{
    private readonly HttpClient httpClient;
    private readonly ILogger<TestServerWriter> logger;

    public TestServerWriter(HttpClient httpClient, ILogger<TestServerWriter> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    public async Task<(bool Success, string Message)> WriteAsync(
        TestServerConfiguration server, string nodeId, string dataType, string value,
        CancellationToken cancellationToken)
    {
        try
        {
            object convertedValue = ConvertValue(value, dataType);
            string root = server.ApiAddress.TrimEnd('/');
            string device = Uri.EscapeDataString(server.Name);
            using HttpResponseMessage modeResponse = await httpClient.PostAsJsonAsync(
                $"{root}/api/opcuaclients/mode?device={device}",
                new { mode = "Manual" }, cancellationToken);
            if (!modeResponse.IsSuccessStatusCode)
                return (false, $"Manual-Modus konnte nicht aktiviert werden: HTTP {(int)modeResponse.StatusCode}");
            using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
                $"{root}/api/opcuaclients/set?device={device}",
                new { node = nodeId, value = convertedValue }, cancellationToken);
            string text = await response.Content.ReadAsStringAsync(cancellationToken);
            string message = ReadMessage(text) ??
                (response.IsSuccessStatusCode
                    ? $"Wert wurde als {dataType} geschrieben."
                    : $"HTTP-Antwort {(int)response.StatusCode}");
            return (response.IsSuccessStatusCode, message);
        }
        catch (Exception exception) when (
            exception is FormatException or OverflowException)
        {
            return (
                false,
                $"Der Wert '{value}' passt nicht zum Datentyp der Node.");
        }
        catch (InvalidOperationException exception)
        {
            return (false, exception.Message);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception,
                "Wert konnte nicht an Testserver {ServerName} geschrieben werden", server.Name);
            return (false, $"Testserver '{server.Name}' ist nicht erreichbar.");
        }
    }

    private static object ConvertValue(string value, string dataType)
    {
        return dataType switch
        {
            "Boolean" => bool.Parse(value),
            "Byte" => byte.Parse(value, CultureInfo.InvariantCulture),
            "Int16" => short.Parse(value, CultureInfo.InvariantCulture),
            "Int32" => int.Parse(value, CultureInfo.InvariantCulture),
            "UInt16" => ushort.Parse(value, CultureInfo.InvariantCulture),
            "UInt32" => uint.Parse(value, CultureInfo.InvariantCulture),
            "Float" => float.Parse(value, CultureInfo.InvariantCulture),
            "Double" => double.Parse(value, CultureInfo.InvariantCulture),
            "String" => value,
            _ => throw new InvalidOperationException(
                $"Der Datentyp '{dataType}' wird von der WebApp noch nicht unterstützt.")
        };
    }

    private static string? ReadMessage(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("message", out JsonElement message))
                return message.GetString();
            if (document.RootElement.TryGetProperty("error", out JsonElement error))
                return error.GetString();
            return null;
        }
        catch (JsonException) { return null; }
    }
}

