namespace Automation.Simulator.TestServer.Simulations;

internal sealed class PLC1Simulator(ISimulationValueStore store)
    : DeviceSimulatorBase("PLC1", store);
