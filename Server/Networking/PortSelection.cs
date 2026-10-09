using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OpcUA_Server;

internal static class PortSelection
{
    public static int FindAvailable(int preferredPort, string service, Action<string>? log = null)
    {
        // Wrap at 65535; never select a privileged fallback port.
        for (int offset = 0; offset < 65535; offset++)
        {
            int port = (preferredPort - 1 + offset) % 65535 + 1;
            if (offset > 0 && port < 1024) continue;
            if (!IsAvailable(port)) continue;
            if (port != preferredPort)
                log?.Invoke($"{service}: port {preferredPort} is unavailable; using {port}.");
            return port;
        }

        throw new InvalidOperationException($"No available TCP port was found for {service}.");
    }

    public static bool IsAvailable(int port)
    {
        // HTTP.sys can share a port across URL prefixes. Treat any TCP listener as occupied.
        if (IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == port)) return false;

        try
        {
            using var socket = new Socket(
                Socket.OSSupportsIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            if (Socket.OSSupportsIPv6) socket.DualMode = true;
            socket.ExclusiveAddressUse = true;
            socket.Bind(new IPEndPoint(Socket.OSSupportsIPv6 ? IPAddress.IPv6Any : IPAddress.Any, port));
            socket.Listen(1);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    public static bool IsAddressInUse(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
                return true;
        }
        return false;
    }

    public static int Next(int port) => port == 65535 ? 1024 : port + 1;
}
