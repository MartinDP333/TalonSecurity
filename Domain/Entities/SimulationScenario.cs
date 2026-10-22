using System;

namespace Domain.Entities;

public class SimulationScenario : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public string ScenarioName { get; private set; }
    public string Description { get; private set; }
    public string TargetEndpoint { get; private set; }
    public string PayloadTemplate { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }

    private SimulationScenario() { }

    public SimulationScenario(Guid tenantId, string scenarioName, string description, string targetEndpoint, string payloadTemplate)
    {
        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Invalid tenant ID.", nameof(tenantId));
        ScenarioName = !string.IsNullOrWhiteSpace(scenarioName) ? scenarioName : throw new ArgumentException("Scenario name is required.", nameof(scenarioName));
        Description = description ?? string.Empty;
        TargetEndpoint = !string.IsNullOrWhiteSpace(targetEndpoint) ? targetEndpoint : throw new ArgumentException("Target endpoint is required.", nameof(targetEndpoint));
        PayloadTemplate = payloadTemplate ?? string.Empty;
    }

    public void MarkAsExecuted() => LastExecutedAt = DateTime.UtcNow;
}
