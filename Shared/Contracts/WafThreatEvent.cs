namespace Shared.Contracts;

public record WafThreatEvent
{
    public required string EventId { get; init; }
    public required string SourceIp { get; init; }
    public required string RequestPath { get; init; }
    public required string AttackType { get; init; }
    public required string RuleId { get; init; }
    public required string RuleMessage { get; init; }
    public required string RawPayload { get; init; }
    public required DateTime Timestamp { get; init; }
}
