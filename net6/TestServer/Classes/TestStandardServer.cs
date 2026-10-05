using Opc.Ua;
using Opc.Ua.Server;

namespace Server;

public static partial class ServerProgram
{
    private sealed class TestStandardServer : StandardServer
    {
        protected override MasterNodeManager CreateMasterNodeManager(
            IServerInternal server,
            ApplicationConfiguration configuration)
        {
            INodeManager[] nodeManagers =
            [
                new TestNodeManager(server, configuration)
            ];

            return new MasterNodeManager(
                server,
                configuration,
                null,
                nodeManagers);
        }
    }
}
