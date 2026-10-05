namespace Automation.Simulator.TestServer.Simulations;

internal sealed class UmreiferSimulator(ISimulationValueStore store)
    : DeviceSimulatorBase("Umreifer", store);
