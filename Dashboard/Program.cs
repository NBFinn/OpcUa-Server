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

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddHttpClient<TestOPCUA_WebApp.Services.TestServerModeController>();
builder.Services.AddSingleton<TestOPCUA_WebApp.Services.TestServerConfigurationStore>();
builder.Services.AddHttpClient<TestOPCUA_WebApp.Services.TestServerWriter>();
builder.Services.AddHttpClient<TestOPCUA_WebApp.Services.TestServerConnectionTester>();
builder.Services.AddHttpClient<TestOPCUA_WebApp.Services.TestServerNodeReader>();
var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();
app.Run();




