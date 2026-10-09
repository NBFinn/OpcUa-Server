# OPC UA Server Module

Reusable OPC UA server source code to embed in your own C# application, just like the companion [adapter source package](https://github.com/NBFinn/OpcUa-Adapter). This repository is not an executable application, a separate Visual Studio project or a prebuilt DLL. There are no .csproj, .sln, Program.cs or framework-specific copies. The same source supports .NET 8, 9 and 10.

A useful name for this is an in-process component or source module. It is not a dynamically loaded plugin system: your project compiles the copied C# files and controls the server's lifetime.

## Structure

```text
Server/
  OpcUaServer.cs                    Public StartAsync/StopAsync API
  Configuration/
    ServerSettings.cs               JSON parsing and validation
    ServerConfiguration.cs          OPC UA security settings
  Networking/
    PortSelection.cs                Available TCP port selection
  Nodes/
    TestServer.cs                   Address space and four writable nodes
  server.json                      Example configuration
README.md
```

The namespace is OpcUA_Server. It has no dashboard, Aspire, REST API, mediator, gateway, background worker registration or automatic startup.

## Add to your application

1. Copy the Server folder beside your application's .csproj file.
2. Use .NET 8, 9 or 10 with C# 12, implicit usings and nullable reference types enabled.
3. Add the OPC Foundation server SDK to your application:

```powershell
dotnet add YourApplication.csproj package OPCFoundation.NetStandard.Opc.Ua.Server --version 1.5.378.176
```

That is the module's only direct package dependency. The SDK also brings its required transitive dependencies. No ASP.NET runtime, MassTransit or mediator setup is needed.

4. In your application's .csproj, copy the configuration beside its executable:

```xml
<ItemGroup>
  <None Update="Server/server.json"
        TargetPath="server.json"
        CopyToOutputDirectory="PreserveNewest"
        CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

SDK-style projects compile C# files automatically. If yours sets EnableDefaultCompileItems=false, also include:

```xml
<ItemGroup>
  <Compile Include="Server/**/*.cs" />
</ItemGroup>
```

Do not create a separate server project or add a project reference. The server runs in the process of the application that includes these sources. The former net8, net9 and net10 project folders were removed because they are no longer needed.

## Start and stop

Keep the server instance in a field or another owner that lives as long as your application needs it:

```csharp
using OpcUA_Server;

var server = new OpcUaServer();
string endpoint = await server.StartAsync();
Console.WriteLine(endpoint);
```

StartAsync returns once the listener is ready. It does not keep your application alive, open a browser, block indefinitely, install a Ctrl+C handler or terminate your process on an error. Your existing UI, service or application lifecycle continues running. Keep it alive while clients use the server.

During your application's shutdown:

```csharp
await server.DisposeAsync();
```

DisposeAsync stops the listener permanently for that object. You can also use await using when its scope covers the entire required server lifetime. Do not place await using in a short startup method: leaving that method would immediately stop the server.

For a temporary stop and later restart on the same object:

```csharp
await server.StopAsync();
string newEndpoint = await server.StartAsync();
```

Repeated StartAsync calls while running return the current endpoint. Repeated StopAsync calls are safe. EndpointUrl, Port and IsRunning expose current state. After stop, the endpoint and port properties are null. After disposal, a new StartAsync call throws ObjectDisposedException.

## Options and error handling

```csharp
var server = new OpcUaServer(
    configurationPath: "C:/path/to/server.json",
    log: message => Console.WriteLine(message));
string endpoint = await server.StartAsync(
    preferredPort: 5844,
    cancellationToken: cancellationToken);
```

The optional log callback receives port-selection and retry messages. By default the module writes nothing to your console. Keep callbacks short and do not throw from them.

The default configuration path is server.json beside the host application's executable. An explicit relative configurationPath is resolved against its current working directory. A preferredPort override affects this start only and does not rewrite JSON.

StartAsync throws configuration, certificate and startup exceptions to your application; catch them in your own error handling. Cancellation is checked while waiting for the lifecycle lock and between startup stages. It is not a shutdown token for an already running server: call StopAsync or DisposeAsync to stop it. Some OPC SDK startup operations do not support immediate cancellation.

## Configuration and test nodes

server.json contains Name, Host, preferred Port and Nodes. The preferred endpoint is opc.tcp://localhost:5844/TestServerSimulator. Always connect to the actual endpoint returned by StartAsync because occupied ports are skipped.

| Node ID | Type | Initial value |
| --- | --- | --- |
| ns=3;s=Test.Enabled | Boolean | false |
| ns=3;s=Test.Counter | Int32 | 0 |
| ns=3;s=Test.Temperature | Double | 20.0 |
| ns=3;s=Test.Message | String | Hello from TestServer |

Each node requires a unique Id in namespace 3, Name, Type, Value and Writable. Supported scalar types are Boolean, Int32, Double and String. Set Writable=false to make a variable read-only. Invalid IDs, duplicates and invalid values are rejected before the listener starts.

Values change through standard OPC UA writes and remain in memory. Stop/restart restores the configured initial values. No REST routes, simulation modes or scenario engine are included. Multiple OpcUaServer objects may run in the same host application; each creates its own listener, address space and certificate store.

Host controls the advertised address. For remote clients, use a hostname they can reach and resolve and configure the firewall accordingly.

## Automatic ports and security

The module checks existing TCP listeners and an exclusive IPv4/IPv6 socket bind. If the preferred port is unavailable, it scans for another, wrapping from 65535 to 1024. Actual listener startup retries port conflicts up to 32 bind attempts. A preliminary probe cannot permanently reserve a port against unrelated processes; the actual bind decides ownership. Read Port or use the returned EndpointUrl.

The server supports anonymous access with None, Basic256Sha256 and Aes256_Sha256_RsaPss endpoints. Encrypted connections require mutual certificate trust. The direct adapter can use UseSecurity=false for local tests.

Each module instance stores certificates under %LOCALAPPDATA%/OpcUaServer/pki/net<version>/instance<id>. Separate stores isolate multiple instances in the same process. The short paths avoid long Windows certificate filenames. Stores are retained after exit; a new module object may require renewed trust. No users or passwords are stored in the example JSON.

## Validation

An external host application compiled these source files with the direct adapter on .NET 8, 9 and 10. Runtime tests passed for two concurrent in-process servers with an occupied preferred port, reading four nodes, typed writes, rejected invalid writes, subscriptions, stop and port release, repeated start/stop, restart with initial values, disposal and cancelled startup. No project or executable from that test host is part of this source package. GUI applications and encrypted remote connections were not exercised.
