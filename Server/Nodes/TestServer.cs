using Opc.Ua;
using Opc.Ua.Server;

namespace OpcUA_Server;

internal sealed class TestServer(ServerSettings settings) : StandardServer
{
    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration) =>
        new(server, configuration, null, new INodeManager[] { new TestNodeManager(server, configuration, settings) });
}

internal sealed class TestNodeManager : CustomNodeManager2
{
    private readonly ServerSettings settings;
    private ushort NodeNamespace => NamespaceIndexes[1];

    public TestNodeManager(IServerInternal server, ApplicationConfiguration configuration, ServerSettings settings)
        : base(server, configuration, "urn:testserver:reserved", "urn:testserver:nodes")
    {
        this.settings = settings;
        SystemContext.NodeIdFactory = this;
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            var folder = new FolderState(null)
            {
                NodeId = new NodeId(settings.Name, NodeNamespace),
                BrowseName = new QualifiedName(settings.Name, NodeNamespace),
                DisplayName = settings.Name,
                TypeDefinitionId = ObjectTypeIds.FolderType
            };
            folder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out IList<IReference>? references))
                externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, folder.NodeId));
            foreach (NodeSettings definition in settings.Nodes)
            {
                var typed = definition.GetTypedValue();
                byte access = definition.Writable ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead;
                var variable = new BaseDataVariableState(folder)
                {
                    NodeId = new NodeId(NodeId.Parse(definition.Id).Identifier, NodeNamespace),
                    BrowseName = new QualifiedName(definition.Name, NodeNamespace),
                    DisplayName = definition.Name,
                    ReferenceTypeId = ReferenceTypeIds.Organizes,
                    TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                    DataType = typed.DataType,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = access,
                    UserAccessLevel = access,
                    Value = typed.Value,
                    StatusCode = StatusCodes.Good,
                    Timestamp = DateTime.UtcNow,
                    Historizing = false
                };
                folder.AddChild(variable);
            }
            AddPredefinedNode(SystemContext, folder);
        }
    }
}
