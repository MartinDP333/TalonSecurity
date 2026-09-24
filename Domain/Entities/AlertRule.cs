using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class AlertRule : BaseEntity
    {
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public string RuleName { get; set; } = string.Empty;
        public string TriggerCondition { get; set; } = string.Empty;
        public SeverityLevel TargetSeverity { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
