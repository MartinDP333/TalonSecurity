using System.Net;
using System.Net.Sockets;

namespace API;

public static class ClientIpAddress
{
    public static string? Get(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null) return null;
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
    }

    public static bool IsInternal(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null) return true;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127);
    }
}
