namespace Automation.Simulator.TestServer.Simulations;

internal sealed class ScenarioDefinition
{
    public string Name { get; init; } = string.Empty;

    public List<ScenarioStep> Steps { get; init; } = [];
}

internal sealed class ScenarioStep
{
    public int AfterMs { get; set; }

    public Dictionary<string, object?> Set { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, double> Increment { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string? ActivateMalfunction { get; set; }
}
