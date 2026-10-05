using Automation.Simulator.TestServer.Simulations;
using Opc.Ua;
using Opc.Ua.Server;
using System.Globalization;
using System.Text.Json;

namespace Server;

public static partial class ServerProgram
{
    private sealed class TestNodeManager : CustomNodeManager2, ISimulationValueStore
    {
        private const string ReservedNamespaceUri = "urn:testserver:reserved";
        private readonly Dictionary<string, List<SimulationNode>> simulationNodes =
            new(StringComparer.OrdinalIgnoreCase);
        private ushort SimulationNamespaceIndex => NamespaceIndexes[1];
        private NodeResourceFile? activeResources;
        public TestNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration)
            : base(server, configuration, ReservedNamespaceUri, NamespaceUri)
        {
            SystemContext.NodeIdFactory = this;
        }

        public override void CreateAddressSpace(
            IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            try
            {
                lock (Lock)
                {
                    NodeResourceFile resources =
                    LoadResources(NodeResourceName);
                    activeResources = resources;
                    var usedNodeIds = new HashSet<string>(StringComparer.Ordinal);
                    int createdStructureObjects = 0;

                    foreach (DeviceResource device in resources.Devices)
                    {
                        ValidateDevice(device, usedNodeIds);

                        var folder = new FolderState(null)
                        {
                            NodeId = new NodeId(device.NodeId, SimulationNamespaceIndex),
                            BrowseName = new QualifiedName(device.Name, SimulationNamespaceIndex),
                            DisplayName = device.Name,
                            TypeDefinitionId = ObjectTypeIds.FolderType
                        };

                        folder.AddReference(
                            ReferenceTypeIds.Organizes,
                            true,
                            ObjectIds.ObjectsFolder);

                        if (!externalReferences.TryGetValue(
                                ObjectIds.ObjectsFolder,
                                out IList<IReference>? references))
                        {
                            references = [];
                            externalReferences[ObjectIds.ObjectsFolder] = references;
                        }

                        references.Add(new NodeStateReference(
                            ReferenceTypeIds.Organizes,
                            false,
                            folder.NodeId));

                        Dictionary<string, FolderState> locationFolders =
                            CreateLocationFolders(
                                folder,
                                device.NodeId,
                                device.Nodes.Select(node => node.SourceItemLocation));

                        var structureObjects = new Dictionary<string, BaseObjectState>(
                            StringComparer.Ordinal);

                        List<NodeResource> typedStructureNodes = device.Nodes
                            .Where(node => node.DataType.Equals(
                                "Object",
                                StringComparison.OrdinalIgnoreCase))
                            .OrderBy(node => node.NodeId.Length)
                            .ToList();

                        foreach (NodeResource structure in typedStructureNodes)
                        {
                            NodeState locationParent = FindLocationParent(
                                structure.SourceItemLocation,
                                folder,
                                locationFolders);
                            NodeState parent = FindNearestStructureParent(
                                structure.NodeId,
                                locationParent,
                                structureObjects);
                            NodeId structureNodeId = CreateServerNodeId(structure.NodeId);

                            if (!usedNodeIds.Add(structureNodeId.ToString())) continue;

                            var objectNode = new BaseObjectState(parent)
                            {
                                NodeId = structureNodeId,
                                BrowseName = new QualifiedName(
                                    structure.Name,
                                    SimulationNamespaceIndex),
                                DisplayName = structure.Name,
                                Description = structure.Description,
                                TypeDefinitionId = ObjectTypeIds.BaseObjectType
                            };

                            parent.AddChild(objectNode);
                            structureObjects[structure.NodeId] = objectNode;
                            createdStructureObjects++;
                        }

                        foreach (NodeResource resource in device.Nodes.Where(node =>
                                     !node.DataType.Equals(
                                         "Object",
                                         StringComparison.OrdinalIgnoreCase)))
                        {
                            NodeId fullNodeId = CreateServerNodeId(resource.NodeId);
                            ValidateResource(resource, fullNodeId.ToString(), usedNodeIds);

                            (NodeId dataTypeId, object value) = ConvertValue(resource);
                            DateTime sourceTimestamp = ParseTimestamp(
                                resource.SourceTimestamp,
                                DateTime.UtcNow);
                            StatusCode statusCode = ParseStatusCode(resource.StatusCode);

                            byte accessLevel = resource.Writable
                                ? AccessLevels.CurrentReadOrWrite
                                : AccessLevels.CurrentRead;

                            NodeState variableParent = FindNearestStructureParent(
                            resource.NodeId,
                            FindLocationParent(
                                resource.SourceItemLocation,
                                folder,
                                locationFolders),
                            structureObjects);

                            var variable = new BaseDataVariableState(variableParent)
                            {
                                NodeId = fullNodeId,

                                BrowseName = new QualifiedName(
                                    resource.Name,
                                    SimulationNamespaceIndex),

                                DisplayName = resource.Name,
                                Description = resource.Description,

                                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,

                                DataType = dataTypeId,
                                ValueRank = ValueRanks.Scalar,
                                AccessLevel = accessLevel,
                                UserAccessLevel = accessLevel,
                                Value = value,
                                StatusCode = statusCode,
                                Timestamp = sourceTimestamp
                            };

                            if (resource.Writable)
                            {
                                variable.OnSimpleWriteValue = (ISystemContext context, NodeState node, ref object valueToWrite) =>
                                {
                                    DateTime now = DateTime.UtcNow;

                                    resource.Value = JsonSerializer.SerializeToElement(
                                        valueToWrite,
                                        valueToWrite.GetType());
                                    resource.SourceTimestamp = now;
                                    resource.ServerTimestamp = now;
                                    resource.StatusCode = nameof(StatusCodes.Good);

                                    variable.Value = valueToWrite;
                                    variable.Timestamp = now;
                                    variable.StatusCode = StatusCodes.Good;

                                    SaveResources(NodeResourceName, resources);

                                    return ServiceResult.Good;
                                };
                            }

                            variableParent.AddChild(variable);
                            if (!simulationNodes.TryGetValue(device.Name, out List<SimulationNode>? nodes))
                            {
                                nodes = [];
                                simulationNodes[device.Name] = nodes;
                            }
                            nodes.Add(new SimulationNode(resource, variable));
                        }

                        AddPredefinedNode(SystemContext, folder);
                    }
                    SimulationRuntime.RegisterStore(this);
                }
            }
            catch
            {
                throw;
            }
        }

        public bool TrySet(string device, string node, object? value, out string message)
        {
            lock (Lock)
            {
                if (!TryFindSimulationNode(device, node, out SimulationNode target))
                {
                    message = $"Node '{node}' was not found in device '{device}'.";
                    return false;
                }
                try
                {
                    object converted = ConvertSimulationValue(target.Resource.DataType, value);
                    ApplySimulationValue(target, converted);
                    SaveActiveResources();
                    message = $"{device}/{target.Resource.Name} = {converted}";
                    return true;
                }
                catch (Exception exception)
                {
                    message = exception.Message;
                    return false;
                }
            }
        }

        public IReadOnlyList<SimulationValueSnapshot> GetValues(string device)
        {
            lock (Lock)
            {
                if (!simulationNodes.TryGetValue(device, out List<SimulationNode>? nodes))
                    return [];

                return nodes.Select(item => new SimulationValueSnapshot(
                        item.Variable.NodeId.ToString(),
                        item.Resource.Name,
                        item.Resource.BrowsePath,
                        item.Resource.DataType,
                        item.Variable.Value,
                        item.Resource.Writable,
                        item.Resource.SourceTimestamp,
                        item.Resource.StatusCode))
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }

        public bool TryIncrement(string device, string node, double amount, out string message)
        {
            lock (Lock)
            {
                if (!TryFindSimulationNode(device, node, out SimulationNode target))
                {
                    message = $"Node '{node}' was not found in device '{device}'.";
                    return false;
                }
                try
                {
                    double current = Convert.ToDouble(target.Variable.Value, CultureInfo.InvariantCulture);
                    object converted = ConvertSimulationValue(target.Resource.DataType, current + amount);
                    ApplySimulationValue(target, converted);
                    SaveActiveResources();
                    message = $"{device}/{target.Resource.Name} = {converted}";
                    return true;
                }
                catch (Exception exception)
                {
                    message = $"Node is not numeric: {exception.Message}";
                    return false;
                }
            }
        }

        public bool TryActivateMalfunction(string device, string code, out string message)
        {
            lock (Lock)
            {
                if (!simulationNodes.TryGetValue(device, out List<SimulationNode>? nodes))
                {
                    message = $"Device '{device}' was not found.";
                    return false;
                }

                SimulationNode? target = nodes.FirstOrDefault(item =>
                    item.Resource.Name.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                    item.Resource.NodeId.Contains(code, StringComparison.OrdinalIgnoreCase));
                target ??= nodes.FirstOrDefault(item =>
                    IsMalfunctionName(item.Resource.Name) &&
                    item.Resource.DataType.Equals("Boolean", StringComparison.OrdinalIgnoreCase));
                target ??= nodes.FirstOrDefault(item =>
                    IsMalfunctionName(item.Resource.Name) &&
                    item.Resource.DataType.Equals("String", StringComparison.OrdinalIgnoreCase));

                if (target is null)
                {
                    message = $"No matching fault variable was found for '{code}'.";
                    return false;
                }

                object value = target.Resource.DataType.Equals("Boolean", StringComparison.OrdinalIgnoreCase)
                    ? true : code;
                ApplySimulationValue(target, value);
                SaveActiveResources();
                message = $"Fault '{code}' activated through {target.Resource.Name}.";
                return true;
            }
        }

        public int ApplyCyclicPattern(string device, long cycle)
        {
            lock (Lock)
            {
                if (!simulationNodes.TryGetValue(device, out List<SimulationNode>? nodes)) return 0;
                int changes = 0;

                foreach ((SimulationNode node, int index) in nodes
                             .Where(item => item.Resource.Writable &&
                                            item.Resource.Name != "str_Module_ID")
                             .Select((item, index) => (item, index)))
                {
                    object? value = CreateCyclicValue(
                        node.Resource.DataType,
                        cycle,
                        index);

                    if (value is null)
                    {
                        continue;
                    }

                    ApplySimulationValue(node, value);
                    changes++;
                }

                if (changes > 0) SaveActiveResources();
                return changes;
            }
        }

        private static object? CreateCyclicValue(
            string dataType,
            long cycle,
            int index)
        {
            long number = cycle + index;

            return dataType.Trim().ToLowerInvariant() switch
            {
                "boolean" or "bool" => number % 2 == 0,
                "byte" => (byte)(number % 256),
                "int16" or "short" => (short)(number % 1001),
                "uint16" or "ushort" => (ushort)(number % 1001),
                "int32" or "int" => (int)(number % 1001),
                "uint32" or "uint" => (uint)(number % 1001),
                "int64" or "long" => number,
                "uint64" or "ulong" => (ulong)number,
                "float" or "single" => (float)(number % 1001),
                "double" => (double)(number % 1001),
                "string" => $"Cycle {cycle} - Node {index}",
                "datetime" => DateTime.UtcNow,
                "basedatatype" => $"Cycle {cycle} - Node {index}",
                _ => null
            };
        }

        private bool TryFindSimulationNode(string device, string node, out SimulationNode target)
        {
            target = null!;
            if (!simulationNodes.TryGetValue(device, out List<SimulationNode>? nodes)) return false;
            SimulationNode? found = nodes.FirstOrDefault(item =>
                item.Resource.NodeId.Equals(node, StringComparison.OrdinalIgnoreCase) ||
                item.Resource.Name.Equals(node, StringComparison.OrdinalIgnoreCase) ||
                item.Variable.NodeId.ToString().Equals(node, StringComparison.OrdinalIgnoreCase));
            if (found is null) return false;
            target = found;
            return true;
        }

        private void ApplySimulationValue(SimulationNode target, object value)
        {
            DateTime now = DateTime.UtcNow;
            target.Resource.Value = JsonSerializer.SerializeToElement(value, value.GetType());
            target.Resource.SourceTimestamp = now;
            target.Resource.ServerTimestamp = now;
            target.Resource.StatusCode = nameof(StatusCodes.Good);
            target.Variable.Value = value;
            target.Variable.Timestamp = now;
            target.Variable.StatusCode = StatusCodes.Good;
            target.Variable.ClearChangeMasks(SystemContext, false);
        }

        private void SaveActiveResources()
        {
            if (activeResources is not null) SaveResources(NodeResourceName, activeResources);
        }

        private static object ConvertSimulationValue(string dataType, object? value)
        {
            if (value is JsonElement element)
            {
                value = element.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when element.TryGetInt64(out long integer) => integer,
                    JsonValueKind.Number => element.GetDouble(),
                    JsonValueKind.String => element.GetString(),
                    _ => element.ToString()
                };
            }

            return dataType.Trim().ToLowerInvariant() switch
            {
                "boolean" or "bool" => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                "byte" => Convert.ToByte(value, CultureInfo.InvariantCulture),
                "int16" or "short" => Convert.ToInt16(value, CultureInfo.InvariantCulture),
                "uint16" or "ushort" => Convert.ToUInt16(value, CultureInfo.InvariantCulture),
                "int32" or "int" => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                "uint32" or "uint" => Convert.ToUInt32(value, CultureInfo.InvariantCulture),
                "int64" or "long" => Convert.ToInt64(value, CultureInfo.InvariantCulture),
                "uint64" or "ulong" => Convert.ToUInt64(value, CultureInfo.InvariantCulture),
                "float" or "single" => Convert.ToSingle(value, CultureInfo.InvariantCulture),
                "double" => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                "string" => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
                "datetime" => value is DateTime dateTime ? dateTime :
                    DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!,
                        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                _ => throw new InvalidDataException($"Data type '{dataType}' cannot be simulated.")
            };
        }

        private static bool IsMalfunctionName(string name) =>
            name.Contains("stör", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("fault", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("error", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("alarm", StringComparison.OrdinalIgnoreCase);

        private sealed record SimulationNode(NodeResource Resource, BaseDataVariableState Variable);

        private Dictionary<string, FolderState> CreateLocationFolders(
            FolderState deviceFolder,
            string deviceId,
            IEnumerable<string?> locations)
        {
            var folders = new Dictionary<string, FolderState>(
                StringComparer.OrdinalIgnoreCase);

            foreach (string location in locations
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Select(value => value!)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                NodeState parent = deviceFolder;
                string currentPath = string.Empty;

                foreach (string segment in SplitLocation(location))
                {
                    currentPath = currentPath.Length == 0
                        ? segment
                        : $"{currentPath}->{segment}";

                    if (folders.TryGetValue(currentPath, out FolderState? existing))
                    {
                        parent = existing;
                        continue;
                    }

                    var folder = new FolderState(parent)
                    {
                        NodeId = new NodeId(
                            $"{deviceId}/location/{currentPath}",
                            SimulationNamespaceIndex),
                        BrowseName = new QualifiedName(segment, SimulationNamespaceIndex),
                        DisplayName = segment,
                        TypeDefinitionId = ObjectTypeIds.FolderType
                    };

                    parent.AddChild(folder);
                    folders[currentPath] = folder;
                    parent = folder;
                }
            }

            return folders;
        }

        private static NodeState FindLocationParent(
            string? location,
            NodeState fallback,
            IReadOnlyDictionary<string, FolderState> folders)
        {
            string normalized = string.Join("->", SplitLocation(location));
            return normalized.Length > 0 && folders.TryGetValue(normalized, out FolderState? folder)
                ? folder
                : fallback;
        }

        private static IEnumerable<string> SplitLocation(string? location) =>
            (location ?? string.Empty).Split(
                    "->",
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(segment => segment.TrimStart('=').Trim())
                .Where(segment => segment.Length > 0);

        private static NodeState FindNearestStructureParent(
            string nodeId,
            NodeState fallback,
            IReadOnlyDictionary<string, BaseObjectState> structureObjects)
        {
            KeyValuePair<string, BaseObjectState>? nearest = structureObjects
                .Where(pair => IsDescendantNodeId(pair.Key, nodeId))
                .OrderByDescending(pair => pair.Key.Length)
                .Cast<KeyValuePair<string, BaseObjectState>?>()
                .FirstOrDefault();

            return nearest?.Value ?? fallback;
        }

        private static bool IsDescendantNodeId(
            string parentNodeId,
            string possibleChildNodeId)
        {
            if (possibleChildNodeId.Length <= parentNodeId.Length ||
                !possibleChildNodeId.StartsWith(
                    parentNodeId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            char separator = possibleChildNodeId[parentNodeId.Length];
            return separator is '.' or '[';
        }

        private NodeId CreateServerNodeId(string sourceNodeId)
        {
            if (string.IsNullOrWhiteSpace(sourceNodeId))
            {
                throw new ArgumentException("The source NodeId must not be empty.");
            }

            try
            {
                NodeId parsed = NodeId.Parse(sourceNodeId.Trim());
                return new NodeId(parsed.Identifier, SimulationNamespaceIndex);
            }
            catch (ServiceResultException)
            {
                return new NodeId(sourceNodeId.Trim(), SimulationNamespaceIndex);
            }
            catch (FormatException)
            {
                return new NodeId(sourceNodeId.Trim(), SimulationNamespaceIndex);
            }
        }

        private static NodeResourceFile LoadResources(
            string resourceName)
        {
            string? devicePath = ResolveActiveDeviceResourcePath();

            if (devicePath is not null)
            {
                DeviceTreeResourceFile? deviceTree =
                    JsonSerializer.Deserialize<DeviceTreeResourceFile>(
                        File.ReadAllText(devicePath),
                        JsonOptions);

                if (deviceTree is null)
                {
                    throw new InvalidDataException(
                        $"Could not read device file '{devicePath}'.");
                }

                string deviceName = activeProfile?.Name ?? deviceTree.RootFolder.Name;
                string deviceIdentifier = string.IsNullOrWhiteSpace(
                    deviceTree.Configuration.DeviceIdentifier)
                    ? deviceName
                    : deviceTree.Configuration.DeviceIdentifier;
                List<NodeResource> nodes = FlattenDeviceNodes(
                    deviceTree.RootFolder,
                    deviceTree.RootFolder.Name).ToList();

                if (string.IsNullOrWhiteSpace(deviceName))
                {
                    throw new InvalidDataException(
                        $"Device file '{devicePath}' does not contain a device name.");
                }

                activeDeviceTree = deviceTree;
                activeDeviceTreePath = devicePath;

                return new NodeResourceFile
                {
                    Devices =
                    [
                        new DeviceResource
                        {
                            NodeId = deviceIdentifier,
                            Name = deviceName,
                            Nodes = nodes
                        }
                    ]
                };
            }

            activeDeviceTree = null;
            activeDeviceTreePath = null;
            string path = ResolveResourcePath(resourceName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The node resource file was not found.",
                    path);
            }

            string json = File.ReadAllText(path);

            NodeResourceFile? resources =
                JsonSerializer.Deserialize<NodeResourceFile>(
                    json,
                    JsonOptions);

            if (resources is not null && activeProfile is not null)
            {
                resources = new NodeResourceFile
                {
                    Devices = resources.Devices
                        .Where(device => device.Name.Equals(
                            activeProfile.Name,
                            StringComparison.OrdinalIgnoreCase))
                        .ToList()
                };
            }

            if (resources is not null)
            {
                foreach (DeviceResource device in resources.Devices)
                {
                    foreach (NodeResource resource in device.Nodes)
                    {
                        resource.BrowsePath = string.Join(
                            "/",
                            new[] { device.Name }
                                .Concat(SplitLocation(resource.SourceItemLocation))
                                .Append(resource.Name)
                                .Where(segment => !string.IsNullOrWhiteSpace(segment)));
                    }
                }
            }

            if (resources is null || resources.Devices.Count == 0)
            {
                throw new InvalidDataException(
                    $"{resourceName}.json contains no devices.");
            }

            return resources;
        }

        private static void SaveResources(
            string resourceName,
            NodeResourceFile resources)
        {
            if (activeDeviceTree is not null &&
                !string.IsNullOrWhiteSpace(activeDeviceTreePath))
            {
                WriteJsonAtomically(activeDeviceTreePath, activeDeviceTree);
                return;
            }

            string path = ResolveResourcePath(resourceName);
            NodeResourceFile resourcesToSave = resources;

            if (activeProfile is not null && File.Exists(path))
            {
                NodeResourceFile? completeResources =
                    JsonSerializer.Deserialize<NodeResourceFile>(
                        File.ReadAllText(path),
                        JsonOptions);
                DeviceResource? activeDevice = resources.Devices.FirstOrDefault();
                if (completeResources is not null && activeDevice is not null)
                {
                    int index = completeResources.Devices.FindIndex(device =>
                        device.Name.Equals(
                            activeProfile.Name,
                            StringComparison.OrdinalIgnoreCase));
                    if (index >= 0)
                    {
                        completeResources.Devices[index] = activeDevice;
                    }
                    else
                    {
                        completeResources.Devices.Add(activeDevice);
                    }

                    resourcesToSave = completeResources;
                }
            }

            WriteJsonAtomically(path, resourcesToSave);
        }

        private static IEnumerable<NodeResource> FlattenDeviceNodes(
            DeviceTreeNode node,
            string browsePath)
        {
            string nodeType = string.IsNullOrWhiteSpace(node.NodeType)
                ? node.Type
                : node.NodeType;
            NodeResource? resource = node.NodeData;
            bool isVariable = nodeType.Equals(
                "Variable",
                StringComparison.OrdinalIgnoreCase);
            bool isObject = nodeType.Equals(
                "Object",
                StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(node.NodeId);

            if (resource is null &&
                (isVariable || isObject))
            {
                resource = new NodeResource
                {
                    NodeId = node.NodeId,
                    Name = node.Name,
                    DataType = isObject ? "Object" : node.DataType,
                    Value = node.Value,
                    SourceTimestamp = node.SourceTimestamp,
                    ServerTimestamp = node.ServerTimestamp,
                    StatusCode = node.StatusCode,
                    Writable = node.Writable,
                    Description = node.Description,
                    SourceServerId = node.SourceServerId,
                    SourceItemLocation = node.SourceItemLocation,
                    UpdateRate = node.UpdateRate
                };
            }

            if (resource is not null &&
                (isVariable || isObject))
            {
                string rootName = browsePath.Split('/', 2)[0];
                resource.BrowsePath = string.IsNullOrWhiteSpace(resource.SourceItemLocation)
                    ? browsePath
                    : string.Join(
                        "/",
                        new[] { rootName }
                            .Concat(SplitLocation(resource.SourceItemLocation))
                            .Append(resource.Name));
                yield return resource;
            }

            foreach (DeviceTreeNode child in node.Children.Values)
            {
                string childPath = string.IsNullOrWhiteSpace(browsePath)
                    ? child.Name
                    : $"{browsePath}/{child.Name}";
                foreach (NodeResource childResource in FlattenDeviceNodes(child, childPath))
                {
                    yield return childResource;
                }
            }
        }

        private static string? ResolveActiveDeviceResourcePath()
        {
            if (activeProfile is null)
            {
                return null;
            }

            if (!File.Exists(activeProfile.NodeFilePath))
            {
                throw new FileNotFoundException(
                    $"The configured node file for '{activeProfile.Name}' was not found.",
                    activeProfile.NodeFilePath);
            }

            return activeProfile.NodeFilePath;
        }

        private static void WriteJsonAtomically<T>(string path, T value)
        {
            string temporaryPath = path + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(
                    value,
                    new JsonSerializerOptions(JsonOptions)
                    {
                        WriteIndented = true
                    }));
            File.Move(temporaryPath, path, true);
        }

        private static string ResolveResourcePath(string resourceName)
        {
            string fileName = $"{resourceName}.json";
            string projectPath = Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "TestServer",
                    fileName));

            return File.Exists(projectPath)
                ? projectPath
                : Path.Combine(AppContext.BaseDirectory, fileName);
        }

        private static DateTime ParseTimestamp(
            DateTime? timestamp,
            DateTime fallback)
        {
            return timestamp?.ToUniversalTime() ?? fallback;
        }

        private static StatusCode ParseStatusCode(string? statusCode)
        {
            return string.IsNullOrWhiteSpace(statusCode) ||
                   statusCode.Equals("Good", StringComparison.OrdinalIgnoreCase)
                ? StatusCodes.Good
                : StatusCodes.Bad;
        }

        private static void ValidateDevice(
            DeviceResource device,
            HashSet<string> usedNodeIds)
        {
            if (string.IsNullOrWhiteSpace(device.NodeId) ||
                string.IsNullOrWhiteSpace(device.Name))
            {
                throw new InvalidDataException(
                    "A device has no valid NodeId or name.");
            }

            if (!usedNodeIds.Add(device.NodeId))
            {
                throw new InvalidDataException(
                    $"Device NodeId '{device.NodeId}' is duplicated.");
            }
        }

        private static void ValidateResource(
            NodeResource resource,
            string fullNodeId,
            HashSet<string> usedNodeIds)
        {
            if (string.IsNullOrWhiteSpace(resource.NodeId))
            {
                throw new InvalidDataException(
                    "A node has no NodeId.");
            }

            if (!usedNodeIds.Add(fullNodeId))
            {
                throw new InvalidDataException(
                    $"NodeId '{fullNodeId}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(resource.Name))
            {
                throw new InvalidDataException(
                    $"Node '{resource.NodeId}' has no name.");
            }

            if (string.IsNullOrWhiteSpace(resource.DataType))
            {
                throw new InvalidDataException(
                    $"Node '{resource.NodeId}' has no data type.");
            }
        }

        private static (NodeId DataTypeId, object Value) ConvertValue(
            NodeResource resource)
        {
            try
            {
                return resource.DataType
                    .Trim()
                    .ToLowerInvariant() switch
                {
                    "boolean" or "bool" =>
                        (DataTypeIds.Boolean,
                            resource.Value.ValueKind is JsonValueKind.True or JsonValueKind.False
                                ? resource.Value.GetBoolean()
                                : false),

                    "byte" =>
                        (DataTypeIds.Byte,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetByte(out byte byteValue)
                                ? byteValue
                                : (byte)0),

                    "int16" or "short" =>
                        (DataTypeIds.Int16,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetInt16(out short int16Value)
                                ? int16Value
                                : (short)0),

                    "uint16" or "ushort" =>
                        (DataTypeIds.UInt16,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetUInt16(out ushort uint16Value)
                                ? uint16Value
                                : (ushort)0),

                    "int32" or "int" =>
                        (DataTypeIds.Int32,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetInt32(out int int32Value)
                                ? int32Value
                                : 0),

                    "uint32" or "uint" =>
                        (DataTypeIds.UInt32,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetUInt32(out uint uint32Value)
                                ? uint32Value
                                : 0U),

                    "int64" or "long" =>
                        (DataTypeIds.Int64,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetInt64(out long int64Value)
                                ? int64Value
                                : 0L),

                    "uint64" or "ulong" =>
                        (DataTypeIds.UInt64,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetUInt64(out ulong uint64Value)
                                ? uint64Value
                                : 0UL),

                    "float" or "single" =>
                        (DataTypeIds.Float,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetSingle(out float floatValue)
                                ? floatValue
                                : 0.0f),

                    "double" =>
                        (DataTypeIds.Double,
                            resource.Value.ValueKind == JsonValueKind.Number &&
                            resource.Value.TryGetDouble(out double doubleValue)
                                ? doubleValue
                                : 0.0d),

                    "string" =>
                        (DataTypeIds.String,
                            resource.Value.ValueKind == JsonValueKind.String
                                ? resource.Value.GetString() ?? string.Empty
                                : string.Empty),

                    "datetime" =>
                        (
                            DataTypeIds.DateTime,
                            resource.Value.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(
                                resource.Value.GetString(),
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind,
                                out DateTime dateTimeValue)
                                ? dateTimeValue
                                : DateTime.UnixEpoch
                        ),

                    "basedatatype" =>
                        (DataTypeIds.BaseDataType, null!),

                    _ => throw new InvalidDataException(
                        $"Unknown data type " +
                        $"'{resource.DataType}'.")
                };
            }
            catch (Exception exception) when (
                exception is FormatException or
                InvalidOperationException or
                OverflowException)
            {
                throw new InvalidDataException(
                    $"The value of node '{resource.NodeId}' does not match " +
                    $"data type '{resource.DataType}'.",
                    exception);
            }
        }
    }
}
