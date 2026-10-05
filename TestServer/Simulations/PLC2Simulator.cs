namespace Automation.Simulator.TestServer.Simulations;

internal sealed class PLC2Simulator(ISimulationValueStore store)
    : DeviceSimulatorBase("PLC2", store);
