using System;

namespace Domain.Entities;

public class IpReputation : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public string IpAddress { get; private set; }
    public int RiskScore { get; private set; }
    public int TotalIncidents { get; private set; }
    public bool IsBlocked { get; private set; }
    public DateTime LastUpdated { get; private set; }

    private IpReputation() { }

    public IpReputation(Guid tenantId, string ipAddress, int riskScore)
    {
        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Ugyldig TenantId", nameof(tenantId));
        IpAddress = !string.IsNullOrWhiteSpace(ipAddress) ? ipAddress : throw new ArgumentException("IP-adresse skal angives", nameof(ipAddress));
        UpdateRiskScore(riskScore);
        TotalIncidents = 1;
        IsBlocked = false;
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

    public void SetBlocked(bool blocked)
    {
        IsBlocked = blocked;
        LastUpdated = DateTime.UtcNow;
    }
}