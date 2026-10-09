# OPC UA Server - .NET 10

This folder contains a console OPC UA server. It has no dashboard, Aspire, REST API, gateway, mediator or simulation modes. The only direct NuGet package is the OPC Foundation server SDK.

## Start

Install a .NET SDK that supports net10.0 and the .NET 10 runtime. No ASP.NET Core runtime is needed. From this folder:

```powershell
dotnet run --project OpcUA_Server.csproj
```

Or open OpcUA_Server.sln in a compatible Visual Studio version and start OpcUA_Server. No browser opens. Ctrl+C stops the console server. For deployment, publish with dotnet publish -c Release and run the published application with the matching runtime.

The server prints its actual OPC-UA endpoint. Connect using that address; the default is opc.tcp://localhost:5844/TestServerSimulator.

## Configure nodes

Edit server.json before building. It is copied to the build and publish directories. A published application reads the file beside its executable. During development, rebuild after editing the source JSON, or provide a custom file with --config.

Name sets the server folder and endpoint path. Host sets the advertised hostname; localhost is the default. For clients on another computer, use a hostname they can resolve and reach and configure the firewall accordingly. Port is the preferred TCP port. Nodes defines the variables.

Each node requires a unique Id in namespace 3, a Name, Type, Value and Writable flag. Supported scalar types are Boolean, Int32, Double and String. Example:

```json
{ "Id": "ns=3;s=Test.Counter", "Name": "Counter", "Type": "Int32", "Value": 0, "Writable": true }
```

Use true/false for Boolean, an integer for Int32, a number for Double, and quoted text for String. Set Writable to false for a read-only variable. The server validates configuration before opening its listener.

Clients read and write using normal OPC UA operations. Values are held in memory; restart restores initial JSON values. The previous device-tree/profile JSON, --profile, --rest-port and --scenario options are no longer used.

## Automatic ports

The server checks active TCP listeners and an exclusive IPv4/IPv6 socket bind. If the preferred port is unavailable, it scans for another. After 65535, fallback scanning resumes at 1024. The actual OPC UA listener also retries startup port conflicts, with a maximum of 32 bind attempts. Other startup errors remain visible.

A preliminary check is not a permanent reservation: an unrelated process can claim a port between checking and binding. The actual listener bind decides ownership. Always connect to the printed endpoint.

```powershell
dotnet run --project OpcUA_Server.csproj -- --port 5844
dotnet run --project OpcUA_Server.csproj -- --config C:/path/to/server.json
```

To test selection, start the first command in two terminals. The endpoints must have different ports. Each process has a separate certificate directory.

## Security and certificates

The server supports anonymous access with None, Basic256Sha256 and Aes256_Sha256_RsaPss endpoints. There are no usernames or password storage. Use the None endpoint for local tests. Encrypted connections require mutual certificate trust; accepting the server certificate in the client does not automatically make the server trust the client.

Certificates are stored under %LOCALAPPDATA%/OpcUaServer/pki/net10/process<PID> in the own, trusted, issuers and rejected directories. The process-specific path isolates concurrent instances and stays short on Windows. A new process may require renewed certificate trust. These directories are outside the repository. Certificate stores are retained after exit.

## Adapter and validation

The separate OpcUa-Adapter repository provides a direct client for connection, browsing, reading, writing and subscriptions. It is not compiled into this server.

The .NET 10 server and direct adapter were built and run together. Four readable nodes, correctly typed writes, rejection of an invalid write type, subscription notifications, cache updates, disconnect/reconnect and automatic port selection passed. Three server versions also ran concurrently with distinct ports while port 5844 was deliberately occupied. Direct F5 interaction in Visual Studio was not tested.

If startup fails, read the console error. Invalid or duplicated Node IDs and unsupported JSON values are rejected before listening. If a client cannot connect, verify the printed endpoint and its security settings.
