using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TestOPCUA_WebApp.Models;
using TestOPCUA_WebApp.Services;

namespace TestOPCUA_WebApp.Pages
{
    public class IndexModel : PageModel
    {
        private readonly TestServerConfigurationStore configurationStore;
        private readonly TestServerWriter serverWriter;
        private readonly TestServerConnectionTester connectionTester;
        private readonly TestServerNodeReader nodeReader;
        private readonly TestServerModeController modeController;

        public IndexModel(
            TestServerConfigurationStore configurationStore,
            TestServerWriter serverWriter,
            TestServerConnectionTester connectionTester,
            TestServerNodeReader nodeReader,
            TestServerModeController modeController)
        {
            this.configurationStore = configurationStore;
            this.serverWriter = serverWriter;
            this.connectionTester = connectionTester;
            this.nodeReader = nodeReader;
            this.modeController = modeController;
        }

        public IReadOnlyList<TestServerConfiguration> Servers { get; private set; } = [];
        public Dictionary<string, IReadOnlyList<string>> NodesByServer { get; private set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ConnectionStatuses { get; private set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> ModesByServer { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        [BindProperty] public string ModeServerName { get; set; } = "";
        [BindProperty] public string ServerMode { get; set; } = "Static";
        [BindProperty] public string? ScenarioPath { get; set; }

        [BindProperty] public TestServerConfiguration Server { get; set; } = new();
        [BindProperty] public string WriteServerName { get; set; } = string.Empty;
        [BindProperty] public string NodeId { get; set; } = string.Empty;
        [BindProperty] public string DataType { get; set; } = string.Empty;
        [BindProperty] public string Value { get; set; } = string.Empty;
        [TempData] public string? Message { get; set; }
        [TempData] public bool IsError { get; set; }

        public async Task OnGetAsync()
        {
            Servers = await configurationStore.GetAllAsync();

            foreach (TestServerConfiguration server in Servers)
            {
                Task<string> statusTask = connectionTester.GetStatusAsync(
                    server,
                    HttpContext.RequestAborted);
                NodesByServer[server.Name] = await nodeReader.GetNodesAsync(
                    server,
                    HttpContext.RequestAborted);
                ConnectionStatuses[server.Name] = await statusTask;
                ModesByServer[server.Name] = await modeController.GetModeAsync(server, HttpContext.RequestAborted);
            }
        }

        public async Task<IActionResult> OnPostSaveServerAsync()
        {
            if (string.IsNullOrWhiteSpace(Server.Name) ||
                !Uri.TryCreate(Server.ApiAddress, UriKind.Absolute, out Uri? apiAddress) ||
                (apiAddress.Scheme != Uri.UriSchemeHttp &&
                 apiAddress.Scheme != Uri.UriSchemeHttps) ||
                apiAddress.Port is < 1 or > 65535)
            {
                Message = "Enter a server name and a valid API address.";
                IsError = true;
                return RedirectToPage();
            }

            (bool reachable, string testMessage) = await connectionTester.TestAsync(
                Server,
                HttpContext.RequestAborted);
            if (!reachable)
            {
                Message = $"Server was not saved: {testMessage}";
                IsError = true;
                return RedirectToPage();
            }

            await configurationStore.SaveAsync(Server);
            Message = $"The API for '{Server.Name}' was verified and saved.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteServerAsync(string name)
        {
            await configurationStore.DeleteAsync(name);
            Message = $"Server '{name}' was deleted.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetModeAsync()
        {
            var server = await configurationStore.GetAsync(ModeServerName);
            if (server is null)
            {
                Message = "The selected server was not found.";
                IsError = true;
                return RedirectToPage();
            }
            var result = await modeController.SetModeAsync(
                server, ServerMode, ScenarioPath, HttpContext.RequestAborted);
            Message = result.Message;
            IsError = !result.Success;
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostWriteValueAsync()
        {
            TestServerConfiguration? server = await configurationStore.GetAsync(WriteServerName);
            if (server is null)
            {
                Message = "The selected test server was not found.";
                IsError = true;
                return RedirectToPage();
            }

            (bool success, string message) = await serverWriter.WriteAsync(
                server, NodeId, DataType, Value, HttpContext.RequestAborted);
            Message = message;
            IsError = !success;
            return RedirectToPage();
        }
    }
}

