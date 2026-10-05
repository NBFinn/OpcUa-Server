# OPC UA Server

Choose the folder matching the .NET version you want to use. Each folder is an independent project containing the OPC UA test server, its node management interface, and an Aspire dashboard. There is no automatic framework switching.

| Folder | Server and web management | Aspire host | IDE |
| --- | --- | --- | --- |
| [net8](net8/README.md) | .NET 8 | .NET 8 / Aspire 9.5.2 | Visual Studio 2022 17.10+ |
| [net9](net9/README.md) | .NET 9 | .NET 9 / Aspire 9.5.2 | Visual Studio 2022 17.12+ |
| [net10](net10/README.md) | .NET 10 | .NET 10 / Aspire 13.5.4 | Visual Studio 2026 |

## Start

1. Open `OpcUA_Server.sln` inside your chosen folder.
2. Select **OpcUaHost** as the startup project and the **http** launch profile.
3. Press F5. Aspire starts both the server and the node management interface.

The shared **Full application** launch profile also starts the host. The host handles both child applications; do not additionally start the children yourself.

For example, to start the .NET 8 variant from the CLI:

```powershell
git clone https://github.com/NBFinn/OpcUa-Server.git
cd OpcUa-Server\net8
dotnet run --project AppHost\OpcUaHost.csproj --launch-profile http
```

Aspire uses `http://localhost:16006`, node management uses `http://localhost:6180`, and the REST API uses `http://localhost:6084`. Follow the Aspire token login URL printed in the host console. The Aspire button in the node management page includes that token automatically. The server automatically skips occupied OPC UA and REST ports; its final addresses appear in the log and the management page discovers the REST port when started through Aspire. The Aspire dashboard, telemetry/resource and management ports remain separately configured, so run only one complete host at a time unless you change those ports.

## Test data

Every variant contains the same four generic writable nodes: `Test.Enabled` (Boolean), `Test.Counter` (Int32), `Test.Temperature` (Double), and `Test.Message` (String), in namespace 3. A two-second demo scenario is included. No original machine data or fixed LAN IP addresses are present in the current source tree. Older commits still contain the previous data.

## Upstream alignment

The simulator and management interface were compared with the current local upstream project on October 5, 2026, including its uncommitted changes. The shared node model, node manager, simulator lifecycle, mode handling and scenario parser already match the upstream behavior. English messages, generic test data and the standalone dashboard integrations are intentional adaptations.

Upstream now checks whether the configured OPC UA port is occupied and rejects startup if it is. These variants preserve automatic selection of available OPC UA and REST ports, including startup conflict retries. The upstream gateway API's explicit client-port configuration is not copied here: the management page talks directly to the simulator REST API, and the adapter remains separate. No upstream source or configuration is modified by this project.

## Compatibility limits

Aspire 9.5.2 is an out-of-support compatibility release used by the .NET 8 and .NET 9 variants; use `net10` for the current Aspire version.

All three complete solutions build, and full startup checks passed for .NET 8, .NET 9 and .NET 10. Validation details are recorded in the individual folder README files. Install the matching .NET and ASP.NET Core runtimes, plus the SDK required by the selected host. Visual Studio itself must support that SDK; see [Microsoft's support matrix](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs). Aspire 9.5.2 framework metadata is available on [NuGet](https://www.nuget.org/packages/Aspire.Hosting.AppHost/9.5.2).

See each folder's README for automatic port selection and a two-server test, node writing, modes, configuration, standalone startup, certificates and troubleshooting.

The adapter is maintained separately in [NBFinn/OpcUa-Adapter](https://github.com/NBFinn/OpcUa-Adapter) and is not used by these server projects.
