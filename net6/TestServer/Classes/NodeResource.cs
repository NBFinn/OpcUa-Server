using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server;

public static partial class ServerProgram
{
    private sealed class NodeResource
    {
        public string NodeId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string DataType { get; init; } = string.Empty;

        public JsonElement Value { get; set; }

        public DateTime? SourceTimestamp { get; set; }

        public DateTime? ServerTimestamp { get; set; }

        public string StatusCode { get; set; } = "Good";

        public bool Writable { get; init; }

        public string Description { get; init; } = string.Empty;

        public string? SourceServerId { get; init; }

        public string? SourceItemLocation { get; init; }

        [JsonIgnore]
        public string BrowsePath { get; set; } = string.Empty;

        public string? UpdateRate { get; init; }
    }
}
