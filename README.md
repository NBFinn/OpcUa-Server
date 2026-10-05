# OPC UA Server

A standalone OPC UA test server with a web dashboard for managing simulator profiles, browsing writable nodes and changing values. An Aspire AppHost starts and monitors the server and dashboard together.

The OPC UA client adapter is maintained separately in [NBFinn/OpcUa-Adapter](https://github.com/NBFinn/OpcUa-Adapter). It is not used or compiled by this server project.

## Requirements and compatibility

- Windows is the environment used for validation.
- For Visual Studio 2022, install the **.NET 8 SDK** and the **ASP.NET and web development** workload. Use Visual Studio 2022 17.8 or later; 17.11 or later supports the shared multi-project launch profile.
- Server and dashboard automatically choose a target matching the selected SDK and Visual Studio toolchain. The current Aspire AppHost requires the **.NET 10 SDK** and Visual Studio 2026 or the CLI.
- For standalone applications, install the runtime matching the selected target. The dashboard also requires the corresponding ASP.NET Core runtime.
- Restore NuGet packages before the first build.

| Component | Target frameworks | Validation |
| --- | --- | --- |
| Server and dashboard | .NET 6, 7, 8, 9 and 10 | All targets build; startup, REST values and the dashboard were tested on 6, 8, 9 and 10 |
| Aspire AppHost | .NET 10 | Combined startup tested |
| Separate adapter | .NET 6 through 10 | See the adapter repository |

.NET 7 startup has not been tested because that runtime was unavailable on the validation machine. .NET 6 and 7 are legacy compatibility targets: the SDK reports their end of support, and current OPC UA dependencies report target-framework support warnings. Those warnings are not suppressed. A successful local test is not a support commitment from the package authors.

.NET Framework 4.x, .NET Core 3.1 and .NET 5 are not configured as targets for this solution. Runtime compatibility does not imply compatibility with older SDKs: the server and dashboard use C# 12.

## Quick start with automatic framework selection

1. Open **`OpcUA_Server.sln`**. This classic solution includes only the server and dashboard; it does not load the .NET 10 Aspire host.
2. Keep the ordinary **Debug** configuration. The framework is selected automatically; no `Debug-Net...` selection is needed.
3. Select the **Server and Dashboard** startup profile and press F5.
4. The dashboard opens at `http://localhost:6180`. The server's REST API uses port `6084`.

For Visual Studio 2022 17.11 or later, enable **Tools > Options > Environment > Preview Features > Enable Multi-Project Launch Profiles** if the shared profile is not shown. Alternatively, right-click the solution, select **Configure Startup Projects**, choose **Multiple startup projects**, and set both projects to **Start**. This also works in earlier .NET 8-capable Visual Studio 2022 versions.

The configuration dropdown also provides **Debug-Net6**, **Debug-Net7**, **Debug-Net9** and **Debug-Net10**. Each selects the same framework for both applications. Install the matching runtime; the dashboard requires the matching ASP.NET Core runtime. .NET 9 requires a compatible Visual Studio 2022 update (17.12 or later); .NET 10 requires Visual Studio 2026 or the CLI. Legacy .NET 6/7 targets still need a C# 12-capable build toolchain.

The shared `Build/ServerTargetFramework.props` applies the same automatic selection to both applications:

| Selected toolchain | Automatic application target |
| --- | --- |
| .NET 8 SDK | .NET 8 |
| .NET 9 SDK | .NET 9; .NET 8 in Visual Studio 2022 before 17.12 |
| .NET 10 SDK, CLI or Visual Studio 2026 | .NET 10 |
| Newer SDK loaded in Visual Studio 2022 | At most .NET 9; at most .NET 8 before 17.12 |

SDK selection happens before project evaluation. Install a compatible SDK through Visual Studio Installer; this project cannot make an unsupported SDK load in an older IDE. The matching .NET and ASP.NET Core runtimes must also be installed. A runtime alone is not a build SDK. Automatic targets are limited to the tested .NET 8-10 range; .NET 6/7 remain explicit legacy targets and require the C# 12 toolchain.

These IDE limits follow the [Microsoft SDK and Visual Studio support matrix](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs).

To build with automatic framework selection from a terminal:

```powershell
git clone https://github.com/NBFinn/OpcUa-Server.git
cd OpcUa-Server
dotnet build OpcUA_Server.sln
```

## Optional Aspire startup (.NET 10 SDK)

```powershell
dotnet restore AppHost\OpcUaHost.csproj
dotnet run --project AppHost\OpcUaHost.csproj --launch-profile http
```

The AppHost itself uses .NET 10. Its server and dashboard use the same automatic target selection and normally run on .NET 10 when launched through the .NET 10 CLI. Aspire opens automatically in the browser. Open the `ServerDashboard` resource or navigate to `http://localhost:6180`.

For Aspire in Visual Studio 2026, open `OpcUA_Server.slnx`, select `OpcUaHost` as the startup project, choose `http`, and start it. Keep `OpcUA_Server.sln` for Visual Studio 2022.

Stop the AppHost with **Ctrl+C**, or stop debugging in Visual Studio, to shut down its child processes.

## Endpoints

| Service | Address / port | Purpose |
| --- | --- | --- |
| Aspire | `http://localhost:16006` | Resources, status and logs |
| Web dashboard | `http://localhost:6180` | Server entries, modes and node values |
| Simulator REST API | `http://localhost:6084` | Dashboard backend |
| OPC UA | `opc.tcp://<server-IP>:5844/TestServerSimulator` | OPC UA client connections |
| Aspire OTLP | `20216` | Telemetry |
| Aspire resource service | `21206` | Internal resource service |

Use the OPC UA address printed in the server console: the server determines its IP address at startup. The REST API uses **HTTP**, not HTTPS.

Aspire prints its token login URL in the console. When started through the AppHost, the Aspire button in the web dashboard includes the current login token automatically.

The bundled JSON files use local loopback endpoints and neutral demo device/module metadata. They contain no fixed LAN IP addresses or example user credentials. The OPC UA metadata uses port `5844`; the running server prints its actual network endpoint at startup. The dataset contains only four generic test nodes; imported machine data and scenarios have been removed.

## Using the dashboard

The dashboard interface, validation messages and application messages use English. The first configured server is selected automatically.

### Add, update or remove a server entry

1. Enter `TestServer` as **Server name**. It must match the running simulator profile name.
2. Enter `http://localhost:6084` as **API address**. Do not enter the OPC UA endpoint here.
3. Select **Save server** to validate the connection and save the entry.
4. Select **Refresh status** to reload the page and refresh connection status.

Entries are saved in `Dashboard/Data/testservers.json`. Saving an existing name updates that entry. **Delete** removes only the dashboard entry; it does not stop the simulator or delete its nodes.

### Find a node and write a value

1. Select the test server.
2. Choose a node from the dropdown. Without a search term, the full writable-node list is available.
3. Optionally filter the list using the search field. **Show all nodes** clears the filter.
4. Select the node's data type, enter a value and select **Write value**.

Examples: Boolean `true`, Int16 `42`, String `Test`. The type must match the node. The dashboard switches the simulator to **Manual** before writing.

### Change the simulation mode

Choose a mode and select **Apply mode**:

| Mode | Behavior |
| --- | --- |
| Static | No automatic value changes |
| Manual | Allow manual changes through the simulator REST API |
| Cyclic | Change supported writable values approximately once per second |
| Scenario | Run timed steps from a scenario file, then return to Static |

These are simulation modes, independent of OPC UA transport security modes such as `None` and `SignAndEncrypt`.

## Standalone startup and target selection

To start only the server with automatic framework selection:

```powershell
dotnet run --project OpcUA_Server.csproj -- --profile TestServer
```

Standalone startup and launch settings default to `TestServer`.

Start the dashboard in a separate terminal if needed:

```powershell
dotnet run --project Dashboard\OpcUA_Server.Dashboard.csproj --launch-profile Dashboard
```

To select another target, use the `ServerTargetFramework` MSBuild property. For example:

```powershell
dotnet run --project OpcUA_Server.csproj -p:ServerTargetFramework=net8.0 -- --profile TestServer
dotnet run --project Dashboard\OpcUA_Server.Dashboard.csproj -p:ServerTargetFramework=net8.0 --launch-profile Dashboard
```

Run these commands in separate terminals. Replace `net8.0` with `net6.0`, `net7.0`, `net9.0` or `net10.0`. Use `dotnet --list-runtimes` to check installed runtimes.

For a build without running:

```powershell
dotnet build OpcUA_Server.csproj -c Release -p:ServerTargetFramework=net8.0
dotnet build Dashboard\OpcUA_Server.Dashboard.csproj -c Release -p:ServerTargetFramework=net8.0
```

Output is written to `bin/Release/net8.0` and `Dashboard/bin/Release/net8.0`. The AppHost uses .NET 10; its child applications select their target automatically. `ServerTargetFramework` overrides the child applications only. The optional `Debug-Net6` through `Debug-Net10` configurations force a particular target. Keep `Debug` or `Release` for automatic selection.

Do not start the same simulator ports both standalone and through Aspire. Without a running AppHost, the dashboard's Aspire link does not provide an active Aspire instance.

## Configure profiles and nodes

`TestServer/OpcUaServers.json` defines profiles:

| Field | Meaning |
| --- | --- |
| Name | Profile name, such as TestServer |
| OpcPort | OPC UA listening port |
| RestPort | HTTP REST API port |
| NodeFilePath | Node file, relative to the profile configuration file |
| DefaultScenario | Scenario filename for startup in Scenario mode |

Nodes are stored in `TestServer/Devices/TestServer.json` under `RootFolder` and `Children`. Check `NodeId`, `Name`, `DataType`, `Value` and `Writable` when editing them. Imported source-device addresses in that file are metadata; `OpcUaServers.json` controls the local listening ports.

Rebuild and restart after changing source configuration. Configuration, node files and scenarios are copied to the output directory. Simulation changes are saved to the active node file, usually the copy under `bin/.../Devices`. Back up values you want to keep before rebuilding: the source file and output copy are separate files.

Override ports for a standalone test:

```powershell
dotnet run --project OpcUA_Server.csproj -- --profile TestServer --port 5944 --rest-port 6184
```

Other options are `--config <configuration-path>` and `--scenario <filename>`. To add another profile, create a configuration entry with separate ports and a node file, add its name to the profile list in `AppHost/AppHost.cs`, and save a dashboard entry with the matching name and REST address.

## Scenario files

The included `TestServer/Simulations/Scenarios/TestServer.normal-production.yaml` uses only the four demo nodes. It enables the demo, increments the counter, changes the temperature and message, then finishes after two seconds in Static mode.

| Node ID | Data type | Initial value |
| --- | --- | --- |
| `ns=3;s=Test.Enabled` | Boolean | `false` |
| `ns=3;s=Test.Counter` | Int32 | `0` |
| `ns=3;s=Test.Temperature` | Double | `20.0` |
| `ns=3;s=Test.Message` | String | `Hello from TestServer` |

All four nodes are writable. In a scenario, use the node names `Enabled`, `Counter`, `Temperature` and `Message`. `afterMs` is elapsed time since scenario startup; `set` assigns values and `increment` changes numeric values. The parser supports this simple format, not arbitrary YAML; full OPC UA NodeIds contain colons and should not be used as keys.

Alternatively, enter an **absolute path on the server machine** in the dashboard's scenario field. When left empty, the dashboard mode switch searches for `Scenarios/TestServer.normal-production.yaml`, with a fallback to the source directory. It currently does not use a differently named `DefaultScenario` from the profile configuration for this mode switch.

## OPC UA client connections and certificates

Use the endpoint printed in the console with an OPC UA client such as UaExpert. The current server offers anonymous access, `None`, and `SignAndEncrypt` with `Aes256_Sha256_RsaPss` or `Basic256Sha256`.

Authentication and security policies are defined in `TestServer/ServerProgram.cs`. Optional profile fields such as `Users`, `AllowAnonymous` and `SecurityPolicies` are not wired into that implementation. The public configuration contains no example credentials. Username/password authentication is not configured.

Certificates are stored under `OpcUA_Server/SimulatorPki/TestServer` next to the executable. The current startup implementation deletes that profile's certificate directory and creates certificates again. Encrypted clients may therefore need to trust the new server certificate after a restart. This simulator is intended for local testing.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Port already in use | Stop another instance or use different ports |
| Server unavailable | Read simulator logs in Aspire; verify the profile name and HTTP REST address |
| Profile not found | Use the configured profile name `TestServer` |
| Missing scenario file | Create the file, rebuild, or provide an absolute path |
| Write fails | Check node type and write access; REST writes require Manual mode |
| JSON error | Check the reported line; do not use trailing commas in `Dashboard/Data/testservers.json` |
| Old dashboard styling | Reload with Ctrl+F5 |
| Untrusted certificate | Check both peers' trust stores and consider whether the server restarted |
| .NET runtime missing | Run `dotnet --list-runtimes` and install the runtime matching the selected target |

Read-only API checks:

```powershell
Invoke-RestMethod http://localhost:6084/api/opcuaclients
Invoke-RestMethod 'http://localhost:6084/api/opcuaclients/values?device=TestServer'
```

AppHost and dashboard exclude the optional Visual Studio hosting startup assembly `Microsoft.WebTools.ApiEndpointDiscovery`. If that error returns, verify that the newly built application is being launched.

## Repository structure

```text
AppHost/             Aspire orchestration
Dashboard/           Razor pages, REST access and saved server entries
TestServer/          OPC UA server, profiles, nodes and simulation
Properties/          Standalone launch settings
Program.cs           Standalone server entry point
OpcUA_Server.slnx    Solution
README.md            This guide
```

The dashboard communicates with the simulator REST API. It does not require the separate adapter. Build output, local certificates, user-specific IDE settings and adapter archives are excluded from this repository.
