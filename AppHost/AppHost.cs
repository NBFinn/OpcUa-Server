// Visual Studio can request this optional tooling assembly even when it is unavailable.
// Exclude only that assembly before creating the web host.
const string excludedAssembly = "Microsoft.WebTools.ApiEndpointDiscovery";
const string exclusionVariable = "ASPNETCORE_HOSTINGSTARTUPEXCLUDEASSEMBLIES";
var excludedAssemblies = (Environment.GetEnvironmentVariable(exclusionVariable) ?? "")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToList();
if (!excludedAssemblies.Contains(excludedAssembly, StringComparer.OrdinalIgnoreCase))
    excludedAssemblies.Add(excludedAssembly);
Environment.SetEnvironmentVariable(exclusionVariable, string.Join(';', excludedAssemblies));

var builder = DistributedApplication.CreateBuilder(args);

var simulations = new[] { "TestServer" }
    .Select(profile => builder.AddProject<Projects.OpcUA_Server>(
        $"Server{profile}", launchProfileName: null)
        .WithArgs("--profile", profile))
    .ToArray();

string aspireUrl = (builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:16006")
    .Split(';', StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd('/');
var dashboard = builder.AddProject<Projects.OpcUA_Server_Dashboard>(
    "Serververwaltung", launchProfileName: null)
    .WithHttpEndpoint(port: 6180, targetPort: 6180, name: "http", isProxied: false)
    .WithEnvironment(context =>
    {
        string? browserToken = builder.Configuration["AppHost:BrowserToken"];
        context.EnvironmentVariables["Aspire__Url"] = string.IsNullOrWhiteSpace(browserToken)
            ? aspireUrl
            : $"{aspireUrl}/login?t={Uri.EscapeDataString(browserToken)}";
    });

foreach (var simulation in simulations)
    dashboard.WaitFor(simulation);

builder.Build().Run();