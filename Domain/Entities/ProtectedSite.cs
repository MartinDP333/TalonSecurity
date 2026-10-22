using System;

namespace Domain.Entities;

public class ProtectedSite : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public string Hostname { get; private set; }
    public string OriginUrl { get; private set; }
    public bool IsEnabled { get; private set; }

    private ProtectedSite() { }

    public ProtectedSite(Guid tenantId, string hostname, string originUrl)
    {
        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Invalid tenant ID.", nameof(tenantId));
        Hostname = NormalizeHostname(hostname);
        OriginUrl = ValidateOrigin(originUrl);
        IsEnabled = true;
    }

    public void Disable() => IsEnabled = false;
    public void Enable() => IsEnabled = true;

    private static string NormalizeHostname(string value)
    {
        var hostname = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(hostname) || hostname.Contains('/') || hostname.Contains(' '))
            throw new ArgumentException("A valid hostname is required.", nameof(value));
        return hostname;
    }

    private static string ValidateOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new ArgumentException("Origin URL must be an absolute HTTP or HTTPS URL.", nameof(value));
        return uri.ToString().TrimEnd('/');
    }
}
