namespace Automation.Simulator.TestServer.Simulations;

internal sealed class FlyerSimulator(ISimulationValueStore store)
    : DeviceSimulatorBase("Flyer", store);
