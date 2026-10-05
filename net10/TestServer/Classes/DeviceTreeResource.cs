namespace Server;

using System.Text.Json;
using System.Text.Json.Serialization;

public static partial class ServerProgram
{
    private sealed class DeviceTreeResourceFile
    {
        public DeviceTreeConfiguration Configuration { get; init; } = new();

        public DeviceTreeNode RootFolder { get; init; } = new();
    }

    private sealed class DeviceTreeConfiguration
    {
        public string DeviceIdentifier { get; init; } = string.Empty;

        public string ServerUrl { get; init; } = string.Empty;
    }

    private sealed class DeviceTreeNode
    {
        public string Name { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public string NodeType { get; init; } = string.Empty;

        public string NodeId { get; init; } = string.Empty;

        public string DataType { get; init; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement Value { get; init; }

        public DateTime? SourceTimestamp { get; init; }

        public DateTime? ServerTimestamp { get; init; }

        public string StatusCode { get; init; } = "Good";

        public bool Writable { get; init; }

        public string Description { get; init; } = string.Empty;

        public string? SourceServerId { get; init; }

        public string? SourceItemLocation { get; init; }

        public string? UpdateRate { get; init; }

        public NodeResource? NodeData { get; init; }

        public Dictionary<string, DeviceTreeNode> Children { get; init; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private static DeviceTreeResourceFile? activeDeviceTree;
    private static string? activeDeviceTreePath;
}
