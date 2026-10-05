namespace Server;

public static partial class ServerProgram
{
    private sealed class NodeResourceFile
    {
        public List<DeviceResource> Devices { get; init; } = [];
    }
}
