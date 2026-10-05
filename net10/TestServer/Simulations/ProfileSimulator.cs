namespace Automation.Simulator.TestServer.Simulations;

// Use the configured profile name rather than a fixed machine name.
internal sealed class ProfileSimulator(string deviceName, ISimulationValueStore store)
    : DeviceSimulatorBase(deviceName, store);
