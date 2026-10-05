namespace Automation.Simulator.TestServer.Simulations;

internal abstract class DeviceSimulatorBase(string deviceName, ISimulationValueStore store) : IAsyncDisposable
{
    private readonly ISimulationValueStore store = store;
    private CancellationTokenSource? modeCancellation;
    private Task? modeTask;

    public string DeviceName { get; } = deviceName;

    public SimulationMode Mode { get; private set; } = SimulationMode.Static;

    public string? ScenarioName { get; private set; }

    public async Task SetModeAsync(
        SimulationMode mode,
        string? scenarioPath = null,
        CancellationToken cancellationToken = default)
    {
        await StopCurrentModeAsync();
        Mode = mode;
        ScenarioName = null;

        modeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken token = modeCancellation.Token;

        if (mode == SimulationMode.Cyclic)
        {
            modeTask = RunCyclicAsync(token);
        }
        else if (mode == SimulationMode.Scenario)
        {
            if (string.IsNullOrWhiteSpace(scenarioPath))
            {
                throw new ArgumentException("Für den Scenario-Modus fehlt der Dateipfad.");
            }

            ScenarioDefinition scenario = ScenarioParser.Load(scenarioPath);
            ScenarioName = scenario.Name;
            modeTask = RunScenarioAsync(scenario, token);
        }
    }

    private async Task RunCyclicAsync(CancellationToken token)
    {
        long cycle = 0;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(token))
        {
            int changes = store.ApplyCyclicPattern(DeviceName, cycle++);
        }
    }

    private async Task RunScenarioAsync(ScenarioDefinition scenario, CancellationToken token)
    {
        int previousTime = 0;
        foreach (ScenarioStep step in scenario.Steps)
        {
            await Task.Delay(Math.Max(0, step.AfterMs - previousTime), token);
            previousTime = step.AfterMs;

            foreach ((string node, object? value) in step.Set)
            {
                store.TrySet(DeviceName, node, value, out string message);
            }

            foreach ((string node, double amount) in step.Increment)
            {
                store.TryIncrement(DeviceName, node, amount, out string message);
            }

            if (!string.IsNullOrWhiteSpace(step.ActivateMalfunction))
            {
                string code = step.ActivateMalfunction;
                store.TryActivateMalfunction(DeviceName, code, out string message);
            }
        }

        Mode = SimulationMode.Static;
        ScenarioName = null;
    }

    private async Task StopCurrentModeAsync()
    {
        if (modeCancellation is null)
        {
            return;
        }

        modeCancellation.Cancel();
        try
        {
            if (modeTask is not null)
            {
                await modeTask;
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            modeCancellation.Dispose();
            modeCancellation = null;
            modeTask = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopCurrentModeAsync();
}
