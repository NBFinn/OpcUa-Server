namespace Automation.Simulator.TestServer.Simulations;

internal interface ISimulationValueStore
{
    IReadOnlyList<SimulationValueSnapshot> GetValues(string device);

    bool TrySet(string device, string node, object? value, out string message);

    bool TryIncrement(string device, string node, double amount, out string message);

    bool TryActivateMalfunction(string device, string code, out string message);

    int ApplyCyclicPattern(string device, long cycle);
}

internal sealed record SimulationValueSnapshot(
    string NodeId,
    string Name,
    string BrowsePath,
    string DataType,
    object? Value,
    bool Writable,
    DateTime? SourceTimestamp,
    string StatusCode);
