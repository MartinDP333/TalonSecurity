using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class SimulationScenario : BaseEntity
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TargetEndpoint { get; set; } = string.Empty;
        public string PayloadTemplate { get; set; } = string.Empty;
        public DateTime? LastExecutedAt { get; set; }
    }
}
