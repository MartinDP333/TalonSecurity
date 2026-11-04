using System.Net;
using Application.Interfaces;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class GeoIpCountryLookup : IDisposable
{
    private readonly ILogger<GeoIpCountryLookup> _logger;
    private readonly string? _databasePath;
    private readonly Lazy<DatabaseReader?> _reader;

    public GeoIpCountryLookup(ILogger<GeoIpCountryLookup> logger, string? databasePath)
    {
        _logger = logger;
        _databasePath = string.IsNullOrWhiteSpace(databasePath) ? null : databasePath;
        _reader = new Lazy<DatabaseReader?>(OpenReader);
    }

    public string LookupCountryCode(string? sourceIp)
    {
        if (!IPAddress.TryParse(sourceIp, out var address) || IsPrivateOrLocal(address))
            return "ZZ";

        var reader = _reader.Value;
        if (reader is null)
            return "ZZ";

        try
        {
            return reader.Country(address).Country.IsoCode?.ToUpperInvariant() ?? "ZZ";
        }
        catch (AddressNotFoundException)
        {
            return "ZZ";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Geo-IP lookup failed for source address.");
            return "ZZ";
        }
    }

    private DatabaseReader? OpenReader()
    {
        if (_databasePath is null || !File.Exists(_databasePath))
        {
            _logger.LogWarning("Geo-IP database is not configured or was not found; country enrichment will return ZZ. Configure GeoIP__DatabasePath.");
            return null;
        }

        try
        {
            return new DatabaseReader(_databasePath);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not open Geo-IP database at {DatabasePath}.", _databasePath);
            return null;
        }
    }

    private static bool IsPrivateOrLocal(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                || bytes[0] == 127
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal
            || address.Equals(IPAddress.IPv6None)
            || address.Equals(IPAddress.IPv6Any)
            || address.IsIPv6UniqueLocal;
    }

    public void Dispose()
    {
        if (_reader.IsValueCreated)
            _reader.Value?.Dispose();
    }
}
