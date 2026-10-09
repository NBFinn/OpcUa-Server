# OPC UA Server

A standalone OPC UA server with four writable test nodes. Start the console project directly. It has no dashboard, Aspire host, REST API, simulation modes, mediator, gateway or adapter integration.

Choose the independent folder for your runtime:

| Folder | Runtime | Start |
| --- | --- | --- |
| [net8](net8/README.md) | .NET 8 | `dotnet run --project net8/OpcUA_Server.csproj` |
| [net9](net9/README.md) | .NET 9 | `dotnet run --project net9/OpcUA_Server.csproj` |
| [net10](net10/README.md) | .NET 10 | `dotnet run --project net10/OpcUA_Server.csproj` |

Install a .NET SDK capable of building the chosen target and its matching runtime. The only direct package dependency is `OPCFoundation.NetStandard.Opc.Ua.Server` 1.5.378.176, together with the dependencies required by that OPC UA SDK. No ASP.NET runtime is required.

In Visual Studio, open the solution inside the chosen folder, select `OpcUA_Server` and press F5. Visual Studio and the installed SDK must support that target. There is no browser launch. Stop using Ctrl+C in the console.

The preferred endpoint is `opc.tcp://localhost:5844/TestServerSimulator`. If that TCP port is occupied, the server selects another available port and prints the actual endpoint. Use the printed endpoint in your OPC UA client.

## Structure

Every version contains only:

```text
OpcUA_Server.sln
OpcUA_Server.csproj
Program.cs
server.json
Server/
  ServerSettings.cs       JSON configuration and validation
  ServerConfiguration.cs  OPC UA security and certificate settings
  TestServer.cs           Address space with readable/writable variables
  PortSelection.cs        Available TCP port selection and conflict detection
README.md
```

Values change through standard OPC UA writes and remain in memory until shutdown. There are no scenarios, REST routes or automatic value generators. Restarting restores the JSON initial values. The old device-tree/profile JSON format has been replaced by the small `server.json` file; it is not imported automatically.

## Nodes

| Node ID | Type | Initial value |
| --- | --- | --- |
| `ns=3;s=Test.Enabled` | Boolean | `false` |
| `ns=3;s=Test.Counter` | Int32 | `0` |
| `ns=3;s=Test.Temperature` | Double | `20.0` |
| `ns=3;s=Test.Message` | String | `Hello from TestServer` |

All four nodes are writable. Node IDs are kept compatible with the previous test data. Connect with an OPC UA client or the separate [direct adapter](https://github.com/NBFinn/OpcUa-Adapter).

The folder READMEs explain configuration, automatic ports, certificates and usage. Builds and live server/adapter tests passed on .NET 8, 9 and 10, including reads, typed writes, rejected invalid writes, subscriptions, reconnects and concurrent port selection. Visual Studio F5 interaction was not tested.
