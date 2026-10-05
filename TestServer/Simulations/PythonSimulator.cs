namespace Automation.Simulator.TestServer.Simulations;

internal sealed class PythonSimulator(ISimulationValueStore store)
    : DeviceSimulatorBase("Python", store);
