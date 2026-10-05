# OPC UA Server — .NET 8

This folder is an independent application: a four-node OPC UA test server, a web interface for managing servers and writing node values, and an Aspire AppHost that starts and monitors both.

## Requirements

- Server: .NET 8 runtime.
- Web management: ASP.NET Core 8 runtime.
- Aspire host: .NET 8 runtime.
- Build: .NET 8 SDK or newer and a C# 12-capable toolchain.
- IDE: Visual Studio 2022 17.10 or later. Install the ASP.NET and web development workload.

This variant uses Aspire 9.5.2 for .NET 8/9 compatibility. That Aspire release is out of support. The .NET 10 folder uses the current Aspire version.

## Start the complete application

Open `OpcUA_Server.sln`, select **OpcUaHost** as the startup project, choose the **http** launch profile, and press **F5**. The shared **Full application** profile also starts only the host; the host launches both child applications automatically. Multi-project profiles require Visual Studio 2022 17.11 or later and may need enabling under Tools > Options > Environment > Preview Features.

From a terminal in this folder:

```powershell
dotnet restore OpcUA_Server.sln
dotnet run --project AppHost\OpcUaHost.csproj --launch-profile http
```

Aspire provides the resources, status and logs. In Visual Studio it opens automatically. In the CLI, follow the token login URL printed by the host. Open the `ServerDashboard` resource to reach the node management page. Its Aspire button passes the current login token automatically.

| Service | Address |
| --- | --- |
| Aspire dashboard | `http://localhost:16006` |
| Web management | `http://localhost:6180` |
| REST API | `http://localhost:6084` |
| OPC UA | `opc.tcp://<server-IP>:5844/TestServerSimulator` |

Use the OPC UA address printed by the simulator. No fixed LAN IP is stored in the configuration. OPC UA and REST ports are preferred starting values; occupied ports are skipped automatically. Start only one Aspire host at a time unless you also change its dashboard, telemetry and resource ports. Stop debugging or press Ctrl+C in the host terminal to stop the applications.

## Automatic server ports

`OpcPort` and `RestPort` in `TestServer/OpcUaServers.json` are preferred ports, not fixed assignments. You do not need to enable automatic selection.

1. `PortSelection.FindAvailable` starts at the preferred port and checks existing TCP listeners and an exclusive IPv4/IPv6 socket bind.
2. If unavailable, it tries the next port. After 65535, fallback scanning wraps to 1024. OPC UA and REST cannot reuse each other's active TCP port.
3. A probe cannot reserve a port until the actual service starts. The OPC UA listener and REST listener therefore retry recognized startup port conflicts, with a limit of 32 bind attempts. Other startup errors still fail visibly.
4. The final addresses are printed in the server log. Aspire supplies a unique `OPCUA_ENDPOINT_FILE` path to both applications. The server atomically publishes its actual endpoints after both services start; the management page uses that REST address for the matching saved local entry. Deleted entries stay deleted and remote entries are not replaced. Source configuration is not rewritten.

For example, if TCP ports 5844 and 6084 are occupied, a startup may print:

```text
OPC UA: port 5844 is unavailable; using 5845.
OPC-UA: opc.tcp://<server-IP>:5845/TestServerSimulator
REST API: port 6084 is unavailable; using 6085.
Simulation ready. CLI: sim help | REST: http://localhost:6085/api/opcuaclients
```

You can change the preferred values in JSON or override them for one launch:

```powershell
dotnet run --project OpcUA_Server.csproj -- --port 5844 --rest-port 6084
```

To test this yourself, start two standalone server terminals using that command. Compare their printed addresses: the second server must use different OPC UA and REST ports. In standalone mode, enter each server's printed REST address in the management page. OPC UA clients must also connect to the printed OPC UA endpoint.

Automatic selection applies to the server's OPC UA and REST listeners. The Aspire dashboard, telemetry/resource services and web management still use their separately configured ports. A free-port probe is never an absolute reservation against unrelated processes; the actual listener bind decides ownership.

Implementation: `TestServer/Classes/PortSelection.cs`, `TestServer/ServerProgram.cs`, and `TestServer/Simulations/SimulationRuntime.cs`. Dashboard discovery is wired in `AppHost/AppHost.cs` and `Dashboard/Services/TestServerConfigurationStore.cs`.

## Four test nodes

| Node ID | Type | Initial value |
| --- | --- | --- |
| `ns=3;s=Test.Enabled` | Boolean | `false` |
| `ns=3;s=Test.Counter` | Int32 | `0` |
| `ns=3;s=Test.Temperature` | Double | `20.0` |
| `ns=3;s=Test.Message` | String | `Hello from TestServer` |

These are generic test nodes. The original machine data, Node IDs and production scenarios are not included in the current source tree.

## Use the web management page

1. The preconfigured `TestServer` entry uses `http://localhost:6084` by default. When launched with Aspire, it discovers the server's actual REST address automatically. The first entry is selected automatically.
2. Choose a node from the full dropdown or filter it with the search field. **Show all nodes** clears the search.
3. Choose the matching data type, enter a value and select **Write value**. Writing switches the simulator to Manual mode.
4. Use **Apply mode** to select Static, Manual, Cyclic or Scenario.

**Static** keeps values unchanged. **Manual** permits REST writes. **Cyclic** changes supported values approximately every second. **Scenario** runs the included two-second demo, then returns to Static. It changes only the four test nodes.

**Save server** validates the connection and adds or updates an entry. **Delete** removes a dashboard entry without stopping its server. Entries are saved in `Dashboard/Data/testservers.json`. Enter an HTTP REST address here, not the OPC UA endpoint.

## Configuration

- `TestServer/OpcUaServers.json`: profile name, OPC UA/REST ports, node file and default scenario.
- `TestServer/Devices/TestServer.json`: the four-node tree and initial values.
- `TestServer/Simulations/Scenarios/TestServer.normal-production.yaml`: the demo steps.
- `AppHost/Properties/launchSettings.json`: Aspire ports, HTTP launch settings and telemetry/resource endpoints.
- `Dashboard/Properties/launchSettings.json`: standalone web management port.

Rebuild after editing source configuration. Node data and scenarios are copied into `bin/...`. Writes update the active output node file; source values remain separate. With Aspire, the local dashboard entry follows the actual REST port automatically. For standalone startup, enter the REST address printed by the server in the dashboard. If changing the web management port, update the endpoint in `AppHost/AppHost.cs`.

In a scenario, use `Enabled`, `Counter`, `Temperature` or `Message` as keys. `afterMs` is elapsed time from scenario startup; `set` assigns values and `increment` changes numeric values. The parser implements a simple YAML subset, so use node names rather than full NodeIds. An absolute scenario path can be supplied through the web interface.

## Standalone startup

If Aspire is not needed, run these commands in separate terminals:

```powershell
dotnet run --project OpcUA_Server.csproj
dotnet run --project Dashboard\OpcUA_Server.Dashboard.csproj --launch-profile Dashboard
```

This starts the server and management page without an Aspire instance. The targets in this folder are fixed; there is no automatic framework override or special build configuration to select.

## OPC UA access

Connect using the endpoint printed by the server, for example with UaExpert. The test server permits anonymous access and supports None and SignAndEncrypt. Username/password authentication is not configured. The current implementation does not apply optional `Users`, `AllowAnonymous` or `SecurityPolicies` profile fields.

Certificates are created under `%LOCALAPPDATA%/OpcUaServer/pki/net8/process<PID>/TestServer` and regenerated on startup. The short per-process path avoids the Windows PFX path-length limit and isolates concurrent server certificates. An encrypted client may need to trust the new certificate after a restart. This is a local test simulator.

## Validation

The complete solution builds. The .NET 8 server, web management and Aspire host were started together; four writable nodes, HTTP responses from both dashboards and the token-bearing Aspire link were checked.

Port conflict checks on .NET 8 covered occupied OPC UA/REST ports, two active servers, identical preferred OPC UA/REST ports and automatic dashboard discovery. The shared implementation also builds in .NET 9 and .NET 10. The CLI/build checks were performed on Windows. Direct F5 interaction in Visual Studio was not tested.

## Troubleshooting

- Missing runtime: install the matching .NET/ASP.NET Core runtimes listed above. A runtime alone cannot build projects; install the required SDK too.
- OPC UA/REST ports in use: the server automatically tries another TCP port. Use the final addresses printed in its log. Aspire and web management ports remain configured separately.
- Server unavailable: check simulator logs in Aspire and the dashboard's REST address.
- Dashboard missing: start `OpcUaHost`, not just `OpcUA_Server`.
- Write rejected: check the selected type and Manual mode.
- Old styling: reload with Ctrl+F5.
- Missing scenario: rebuild so the included YAML is copied to output.

The OPC UA client adapter is a separate repository: [NBFinn/OpcUa-Adapter](https://github.com/NBFinn/OpcUa-Adapter). It is not used by this application.
