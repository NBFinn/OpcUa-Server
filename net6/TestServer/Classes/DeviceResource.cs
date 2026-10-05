namespace Server;

public static partial class ServerProgram
{
    private sealed class DeviceResource
    {
        public string NodeId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public List<NodeResource> Nodes { get; init; } = [];
    }
}
