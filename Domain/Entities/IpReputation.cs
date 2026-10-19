using System;

namespace Domain.Entities;

public class IpReputation : BaseEntity
{
    public Guid? TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public string IpAddress { get; private set; } = string.Empty;
    public int RiskScore { get; private set; }
    public int TotalIncidents { get; private set; }
    public bool IsBlocked { get; private set; }
    public DateTime? BlockedUntil { get; private set; }
    public DateTime LastUpdated { get; private set; }

    private IpReputation() { }

    public IpReputation(Guid tenantId, string ipAddress, int riskScore)
    {
        TenantId = tenantId;
        IpAddress = !string.IsNullOrWhiteSpace(ipAddress) ? ipAddress : throw new ArgumentException("IP-adresse skal angives", nameof(ipAddress));
        UpdateRiskScore(riskScore);
        TotalIncidents = 1;
        IsBlocked = false;
        BlockedUntil = null;
        LastUpdated = DateTime.UtcNow;
    }

    public IpReputation(string ipAddress, int riskScore)
    {
        IpAddress = !string.IsNullOrWhiteSpace(ipAddress) ? ipAddress : throw new ArgumentException("IP-adresse skal angives", nameof(ipAddress));
        UpdateRiskScore(riskScore);
        TotalIncidents = 1;
        IsBlocked = false;
        BlockedUntil = null;
        LastUpdated = DateTime.UtcNow;
    }

    public void UpdateRiskScore(int newScore)
    {
        if (newScore < 0 || newScore > 100)
            throw new ArgumentOutOfRangeException(nameof(newScore), "Risikoscore skal være mellem 0 og 100");
        RiskScore = newScore;
        LastUpdated = DateTime.UtcNow;
    }

    public void IncrementIncidents()
    {
        TotalIncidents++;
        LastUpdated = DateTime.UtcNow;
    }

    public void ApplyProgressiveBlock()
    {
        IncrementIncidents();
        IsBlocked = true;

        TimeSpan penaltyDuration = TotalIncidents switch
        {
            <= 2 => TimeSpan.FromMinutes(15),
            <= 5 => TimeSpan.FromHours(1),
            _ => TimeSpan.FromHours(24)
        };

        BlockedUntil = DateTime.UtcNow.Add(penaltyDuration);
        LastUpdated = DateTime.UtcNow;
    }
    public bool CheckAndLiftBlockIfExpired()
    {
        if (IsBlocked && BlockedUntil.HasValue && DateTime.UtcNow >= BlockedUntil.Value)
        {
            IsBlocked = false;
            BlockedUntil = null;
            LastUpdated = DateTime.UtcNow;
            return true;
        }
        return false;
    }
}