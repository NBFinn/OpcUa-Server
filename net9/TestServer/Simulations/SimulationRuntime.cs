using System.Net;
using System.Text.Json;

namespace Automation.Simulator.TestServer.Simulations;

internal static class SimulationRuntime
{
    private static readonly JsonSerializerOptions ResponseJsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, DeviceSimulatorBase> Simulators =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, StartupSelection> StartupSelections =
        new(StringComparer.OrdinalIgnoreCase);
    private static ISimulationValueStore? store;
    private static CancellationTokenSource? cancellation;
    private static Task? cliTask;
    private static Task? restTask;
    private static string? selectedProfile;
    private static int restPort = 5080;

    public static void ConfigureProfile(
        string profile,
        int profileRestPort,
        string scenario)
    {
        selectedProfile = profile;
        restPort = profileRestPort;
        StartupSelections.Clear();

        string prefix = $"OPCUA_SIM_{profile.ToUpperInvariant()}";
        string? configuredMode = Environment.GetEnvironmentVariable(
            $"{prefix}_MODE");
        SimulationMode mode = Enum.TryParse(
            configuredMode,
            true,
            out SimulationMode parsedMode)
            ? parsedMode
            : SimulationMode.Static;
        string? scenarioPath = Environment.GetEnvironmentVariable(
            $"{prefix}_SCENARIO");

        if (mode == SimulationMode.Scenario)
        {
            scenarioPath = string.IsNullOrWhiteSpace(scenarioPath)
                ? Path.IsPathRooted(scenario)
                    ? scenario
                    : ResolveScenarioPath(scenario)
                : Path.GetFullPath(scenarioPath);
        }

        StartupSelections[profile] = new StartupSelection(
            mode,
            scenarioPath);

        Console.WriteLine($"{profile}: {mode}" +
            (scenarioPath is null ? string.Empty : $" ({scenarioPath})"));
        Console.WriteLine();
    }

    public static void RegisterStore(ISimulationValueStore valueStore)
    {
        if (ReferenceEquals(store, valueStore) && Simulators.Count > 0)
        {
            return;
        }

        store = valueStore;
        Simulators.Clear();
        if (!string.IsNullOrWhiteSpace(selectedProfile))
        {
            Simulators[selectedProfile] = new ProfileSimulator(selectedProfile, valueStore);
            return;
        }
        Simulators["TestServer"] = new ProfileSimulator("TestServer", valueStore);
    }

    public static async Task StartAsync(CancellationToken parentToken = default)
    {
        if (cancellation is not null || store is null) return;
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(parentToken);

        foreach (DeviceSimulatorBase simulator in Simulators.Values)
        {
            StartupSelection selection = ResolveStartupSelection(simulator.DeviceName);
            await simulator.SetModeAsync(
                selection.Mode,
                selection.ScenarioPath,
                cancellation.Token);
        }

        cliTask = Task.Run(() => RunCliAsync(cancellation.Token), cancellation.Token);
        restTask = RunRestAsync(cancellation.Token);

        Console.WriteLine(
            $"Simulation ready. CLI: sim help | REST: http://localhost:{restPort}/api/opcuaclients");
    }

    public static async Task StopAsync()
    {
        if (cancellation is null) return;
        cancellation.Cancel();
        foreach (DeviceSimulatorBase simulator in Simulators.Values)
        {
            await simulator.DisposeAsync();
        }

        await IgnoreCancellationAsync(cliTask);
        await IgnoreCancellationAsync(restTask);
        cancellation.Dispose();
        cancellation = null;
    }

    private static async Task RunCliAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            string? line;
            try
            {
#if NET7_0_OR_GREATER
                line = await Console.In.ReadLineAsync(token);
#else
                line = await Console.In.ReadLineAsync().WaitAsync(token);
#endif
            }
            catch (OperationCanceledException) { return; }
            if (line is null) return;
            if (line.StartsWith("sim ", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(await ExecuteCommandAsync(line[4..], token));
            }
        }
    }

    private static async Task<string> ExecuteCommandAsync(string command, CancellationToken token)
    {
        string[] parts = command.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts[0].Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            return "sim status | sim mode <device> <Static|Manual|Cyclic> | " +
                   "sim scenario <device> <file> | sim set <device> <Node> <value> | " +
                   "sim increment <device> <Node> <amount> | sim fault <device> <Code>";
        }

        if (parts[0].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join(Environment.NewLine, Simulators.Values.Select(item =>
                $"{item.DeviceName}: {item.Mode}" +
                (item.ScenarioName is null ? "" : $" ({item.ScenarioName})")));
        }

        if (parts.Length < 3 || !Simulators.TryGetValue(parts[1], out DeviceSimulatorBase? simulator))
            return "Unknown or missing device name.";

        if (parts[0].Equals("mode", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse(parts[2], true, out SimulationMode mode) && mode != SimulationMode.Scenario)
        {
            await simulator.SetModeAsync(mode, cancellationToken: token);
            return $"{simulator.DeviceName}: {mode} active.";
        }

        if (parts[0].Equals("scenario", StringComparison.OrdinalIgnoreCase))
        {
            string path = command.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries)[2].Trim('"');
            await simulator.SetModeAsync(SimulationMode.Scenario, path, token);
            return $"{simulator.DeviceName}: scenario started.";
        }

        if (simulator.Mode != SimulationMode.Manual) return "Device is not in Manual mode.";

        if (parts[0].Equals("fault", StringComparison.OrdinalIgnoreCase))
        {
            bool faultSuccess = store!.TryActivateMalfunction(
                parts[1], parts[2], out string faultMessage);
            return faultSuccess ? faultMessage : $"Error: {faultMessage}";
        }

        if (parts.Length < 4) return "Incomplete command.";

        bool success;
        string message;
        switch (parts[0].ToLowerInvariant())
        {
            case "set":
                success = store!.TrySet(parts[1], parts[2], ParseValue(parts[3]), out message);
                break;
            case "increment":
                if (!double.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double amount))
                    return "Invalid numeric value.";
                success = store!.TryIncrement(parts[1], parts[2], amount, out message);
                break;
            default:
                return "Unknown command. Use 'sim help'.";
        }

        return success ? message : $"Error: {message}";
    }

    private static async Task RunRestAsync(CancellationToken token)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{restPort}/");
        try
        {
            listener.Start();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Could not start simulation REST API: {exception.Message}");
            return;
        }

        using CancellationTokenRegistration registration = token.Register(listener.Stop);
        while (!token.IsCancellationRequested)
        {
            try { _ = HandleRequestAsync(await listener.GetContextAsync(), token); }
            catch (Exception) when (token.IsCancellationRequested) { return; }
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            bool corsAllowed = ApplyCorsHeaders(context);
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = corsAllowed ? 204 : 403;
                context.Response.Close();
                return;
            }

            string path = context.Request.Url?.AbsolutePath.Trim('/').ToLowerInvariant() ?? "";
            if (context.Request.HttpMethod == "GET" && path == "api/opcuaclients")
            {
                await RespondAsync(context, 200, Simulators.Values.Select(item => new
                { device = item.DeviceName, mode = item.Mode.ToString(), scenario = item.ScenarioName }), token);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "api/opcuaclients/values")
            {
                string valuesDevice = context.Request.QueryString["device"] ?? "";
                if (!Simulators.ContainsKey(valuesDevice))
                {
                    await RespondAsync(context, 404, new { error = "Unknown device." }, token);
                    return;
                }

                await RespondAsync(context, 200, store!.GetValues(valuesDevice), token);
                return;
            }

            if (context.Request.HttpMethod != "POST")
            {
                await RespondAsync(context, 405, new { error = "Expected GET or POST." }, token);
                return;
            }

            string device = context.Request.QueryString["device"] ?? "";
            if (!Simulators.TryGetValue(device, out DeviceSimulatorBase? simulator))
            {
                await RespondAsync(context, 404, new { error = "Unknown device." }, token);
                return;
            }

            using JsonDocument document = await JsonDocument.ParseAsync(context.Request.InputStream, cancellationToken: token);
            JsonElement body = document.RootElement;
            if (path == "api/opcuaclients/mode")
            {
                if (!Enum.TryParse(body.GetProperty("mode").GetString(), true, out SimulationMode mode))
                {
                    await RespondAsync(context, 400, new { error = "Invalid mode." }, token);
                    return;
                }

                string? scenarioPath = body.TryGetProperty("scenarioPath", out JsonElement value)
                    ? value.GetString() : null;
                if (mode == SimulationMode.Scenario && string.IsNullOrWhiteSpace(scenarioPath))
                {
                    scenarioPath = ResolveDefaultScenarioPath(device);
                }
                await simulator.SetModeAsync(mode, scenarioPath, cancellation!.Token);
                await RespondAsync(context, 200, new { device, mode = mode.ToString() }, token);
                return;
            }

            if (simulator.Mode != SimulationMode.Manual)
            {
                await RespondAsync(context, 409, new { error = "Device is not in Manual mode." }, token);
                return;
            }

            string node = body.TryGetProperty("node", out JsonElement nodeValue) ? nodeValue.GetString() ?? "" : "";
            bool success;
            string message;
            if (path == "api/opcuaclients/set")
                success = store!.TrySet(device, node, body.GetProperty("value"), out message);
            else if (path == "api/opcuaclients/increment")
                success = store!.TryIncrement(device, node, body.GetProperty("amount").GetDouble(), out message);
            else if (path == "api/opcuaclients/malfunction")
                success = store!.TryActivateMalfunction(device, body.GetProperty("code").GetString() ?? "", out message);
            else
            {
                await RespondAsync(context, 404, new { error = "Unknown route." }, token);
                return;
            }

            await RespondAsync(context, success ? 200 : 400, new { success, message }, token);
        }
        catch (Exception exception)
        {
            await RespondAsync(context, 500, new { error = exception.Message }, token);
        }
    }

    private static async Task RespondAsync(HttpListenerContext context, int status, object body, CancellationToken token)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(body, ResponseJsonOptions);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = data.Length;
        await context.Response.OutputStream.WriteAsync(data, token);
        context.Response.Close();
    }

    private static object ParseValue(string value)
    {
        if (bool.TryParse(value, out bool boolean)) return boolean;
        if (long.TryParse(value, out long integer)) return integer;
        if (double.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out double number)) return number;
        return value.Trim('"');
    }

    private static bool ApplyCorsHeaders(HttpListenerContext context)
    {
        string? originText = context.Request.Headers["Origin"];
        if (string.IsNullOrWhiteSpace(originText)) return true;
        if (!Uri.TryCreate(originText, UriKind.Absolute, out Uri? origin)) return false;

        int[] uiPorts = [5081, 5082, 5083, 5084, 5085];
        bool allowedPort = uiPorts.Contains(origin.Port);
        bool allowedHost = origin.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                           origin.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                           origin.Host.Equals(context.Request.Url?.Host, StringComparison.OrdinalIgnoreCase);
        if (!allowedPort || !allowedHost) return false;

        context.Response.Headers["Access-Control-Allow-Origin"] = origin.GetLeftPart(UriPartial.Authority);
        context.Response.Headers["Vary"] = "Origin";
        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        return true;
    }

    private static async Task IgnoreCancellationAsync(Task? task)
    {
        try { if (task is not null) await task; }
        catch (OperationCanceledException) { }
    }

    private static StartupSelection ResolveStartupSelection(string device)
    {
        if (StartupSelections.TryGetValue(device, out StartupSelection? selection))
        {
            return selection;
        }

        string prefix = $"OPCUA_SIM_{device.ToUpperInvariant()}";
        string? configuredMode = Environment.GetEnvironmentVariable($"{prefix}_MODE");
        SimulationMode mode = Enum.TryParse(configuredMode, true, out SimulationMode parsed)
            ? parsed
            : SimulationMode.Static;
        string? scenarioPath = Environment.GetEnvironmentVariable($"{prefix}_SCENARIO");
        return new StartupSelection(mode, scenarioPath);
    }

    private static string ResolveDefaultScenarioPath(string device)
    {
        string fileName = $"{device}.normal-production.yaml";
        string outputPath = Path.Combine(AppContext.BaseDirectory, "Scenarios", fileName);
        if (File.Exists(outputPath)) return outputPath;

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "TestServer", "Simulations", "Scenarios", fileName));
    }

    private static string ResolveScenarioPath(string fileName)
    {
        string outputPath = Path.Combine(AppContext.BaseDirectory, "Scenarios", fileName);
        if (File.Exists(outputPath)) return outputPath;

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            "TestServer", "Simulations", "Scenarios", fileName));
    }

    private sealed record StartupSelection(
        SimulationMode Mode,
        string? ScenarioPath);
}
