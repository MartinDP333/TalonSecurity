using Domain.Enums;
using System;

namespace Domain.Entities;

public class AlertRule : BaseEntity
{
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public string RuleName { get; private set; }
    public string TriggerCondition { get; private set; }
    public SeverityLevel TargetSeverity { get; private set; }
    public bool IsEnabled { get; private set; }

    private AlertRule() { }

    public AlertRule(Guid tenantId, string ruleName, string triggerCondition, SeverityLevel targetSeverity)
    {
        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Ugyldig TenantId", nameof(tenantId));
        RuleName = !string.IsNullOrWhiteSpace(ruleName) ? ruleName : throw new ArgumentException("Regelnavn skal angives", nameof(ruleName));
        TriggerCondition = !string.IsNullOrWhiteSpace(triggerCondition) ? triggerCondition : throw new ArgumentException("Udløser-betingelse skal angives", nameof(triggerCondition));
        TargetSeverity = targetSeverity;
        IsEnabled = true;
    }

    public void ToggleRule()
    {
        IsEnabled = !IsEnabled;
    }
}