namespace OpcUA_Server;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        if (!args.Contains("--profile", StringComparer.OrdinalIgnoreCase))
            args = [.. args, "--profile", "PLC1"];
        await Server.ServerProgram.Main(args);
    }
}

