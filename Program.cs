namespace OpcUA_Server;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

        if (!args.Contains("--profile", StringComparer.OrdinalIgnoreCase))
            args = [.. args, "--profile", "TestServer"];
        await Server.ServerProgram.Main(args);
    }
}

