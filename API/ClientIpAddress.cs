using System.Net;

namespace API;

public static class ClientIpAddress
{
    public static string? Get(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
            return null;

        return address.IsIPv4MappedToIPv6
            ? address.MapToIPv4().ToString()
            : address.ToString();
    }
}
