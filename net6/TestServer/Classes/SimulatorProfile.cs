using System.Text.Json;

namespace Server;

public static partial class ServerProgram
{
    private sealed record SimulatorProfile
    {
        public string Name { get; init; } = string.Empty;
        public int OpcPort { get; init; }
        public int RestPort { get; init; }
        public string NodeFilePath { get; init; } = string.Empty;
        public string DefaultScenario { get; init; } = string.Empty;

        public static SimulatorProfile Parse(string[] args)
        {
            string configurationPath = ResolveConfigurationPath(
                ReadOption(args, "--config") ?? "OpcUaServers.json");
            string? name = ReadOption(args, "--profile");

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "--profile must specify a server name from the configuration.");
            }

            string json = File.ReadAllText(configurationPath);
            ServerConfigurationFile? configuration =
                JsonSerializer.Deserialize<ServerConfigurationFile>(json, JsonOptions);
            SimulatorProfile profile = configuration?.Servers.FirstOrDefault(server =>
                server.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException(
                    $"Server '{name}' was not found in '{configurationPath}'.");

            Validate(profile, configurationPath);

            return profile with
            {
                OpcPort = ReadPort(args, "--port", profile.OpcPort),
                RestPort = ReadPort(args, "--rest-port", profile.RestPort),
                DefaultScenario =
                ReadOption(args, "--scenario") ?? profile.DefaultScenario,
                NodeFilePath = ResolveConfiguredPath(
                    configurationPath,
                    profile.NodeFilePath)
            };
        }

        private static void Validate(
            SimulatorProfile profile,
            string configurationPath)
        {
            if (string.IsNullOrWhiteSpace(profile.Name))
            {
                throw new InvalidDataException(
                    $"A server in '{configurationPath}' has no name.");
            }

            ValidatePort(profile.OpcPort, nameof(OpcPort));
            ValidatePort(profile.RestPort, nameof(RestPort));

            if (string.IsNullOrWhiteSpace(profile.NodeFilePath))
            {
                throw new InvalidDataException(
                    $"NodeFilePath is missing for server '{profile.Name}'.");
            }
        }

        private static int ReadPort(string[] args, string option, int fallback)
        {
            string? text = ReadOption(args, option);
            if (text is null)
            {
                return fallback;
            }

            return int.TryParse(text, out int port)
                ? ValidatePort(port, option)
                : throw new ArgumentException($"{option} does not contain a valid port.");
        }

        private static int ValidatePort(int port, string propertyName)
        {
            return port is > 0 and <= 65535
                ? port
                : throw new InvalidDataException(
                    $"{propertyName} does not contain a valid port.");
        }

        private static string? ReadOption(string[] args, string option)
        {
            int index = Array.FindIndex(args, item =>
                item.Equals(option, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        private static string ResolveConfigurationPath(string configuredPath)
        {
            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            string outputPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, configuredPath));
            if (File.Exists(outputPath))
            {
                return outputPath;
            }

            return Path.GetFullPath(
                Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "TestServer",
                    configuredPath));
        }

        private static string ResolveConfiguredPath(
            string configurationPath,
            string configuredPath)
        {
            return Path.IsPathRooted(configuredPath)
                ? Path.GetFullPath(configuredPath)
                : Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(configurationPath)!,
                    configuredPath));
        }
    }

    private sealed class ServerConfigurationFile
    {
        public List<SimulatorProfile> Servers { get; init; } = [];
    }
}
