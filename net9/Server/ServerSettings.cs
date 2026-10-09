using System.Text.Json;
using Opc.Ua;

namespace OpcUA_Server;

internal sealed class ServerSettings
{
    public string Name { get; init; } = "TestServer";
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5844;
    public List<NodeSettings> Nodes { get; init; } = [];

    public static ServerSettings Load(string[] args)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "server.json");
        int? portOverride = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (i + 1 >= args.Length) throw new ArgumentException($"Missing value after {args[i]}.");
            switch (args[i])
            {
                case "--config": path = Path.GetFullPath(args[++i]); break;
                case "--port": portOverride = int.Parse(args[++i]); break;
                default: throw new ArgumentException($"Unknown argument: {args[i]}.");
            }
        }
        var settings = JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? throw new InvalidDataException("Server configuration is empty.");
        if (portOverride is not null) settings = new ServerSettings
        {
            Name = settings.Name, Host = settings.Host, Port = portOverride.Value, Nodes = settings.Nodes
        };
        if (settings.Port is < 1 or > 65535) throw new InvalidDataException("Port must be between 1 and 65535.");
        if (string.IsNullOrWhiteSpace(settings.Name) || settings.Name.Any(character => !char.IsLetterOrDigit(character) && character != '_' && character != '-'))
            throw new InvalidDataException("Name must contain only letters, digits, underscores or hyphens.");
        if (Uri.CheckHostName(settings.Host) == UriHostNameType.Unknown) throw new InvalidDataException("Invalid host name.");
        if (settings.Nodes is null || settings.Nodes.Count == 0) throw new InvalidDataException("Configure at least one node.");
        var ids = new HashSet<NodeId> { new NodeId(settings.Name, 3) };
        foreach (NodeSettings node in settings.Nodes)
        {
            NodeId id = NodeId.Parse(node.Id);
            if (NodeId.IsNull(id) || id.NamespaceIndex != 3 || !ids.Add(id))
                throw new InvalidDataException($"Node ID must be unique and use namespace 3: {node.Id}.");
            if (string.IsNullOrWhiteSpace(node.Name)) throw new InvalidDataException("Every node requires a name.");
            _ = node.GetTypedValue();
        }
        return settings;
    }
}

internal sealed class NodeSettings
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = "String";
    public JsonElement Value { get; init; }
    public bool Writable { get; init; } = true;

    public (NodeId DataType, object Value) GetTypedValue() => Type switch
    {
        "Boolean" => (DataTypeIds.Boolean, Value.GetBoolean()),
        "Int32" => (DataTypeIds.Int32, Value.GetInt32()),
        "Double" => (DataTypeIds.Double, Value.GetDouble()),
        "String" => (DataTypeIds.String, Value.GetString() ?? string.Empty),
        _ => throw new InvalidDataException($"Unsupported node type: {Type}.")
    };
}
