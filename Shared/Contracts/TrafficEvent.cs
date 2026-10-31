namespace Shared.Contracts;

public record TrafficEvent
{
    public required string IpAddress { get; init; }
    public Guid? TenantId { get; init; }
    public Guid? SiteTenantId { get; init; }
    public string? Hostname { get; init; }
    public bool IsBlocked { get; set; }
    public required string Endpoint { get; init; }
    public required string UserAgent { get; init; }
    public required DateTime Timestamp { get; init; }
}
