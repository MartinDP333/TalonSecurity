using System;

namespace Domain.Entities;

public class SimulationScenario : BaseEntity
{
    public string ScenarioName { get; private set; }
    public string Description { get; private set; }
    public string TargetEndpoint { get; private set; }
    public string PayloadTemplate { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }

    private SimulationScenario() { }

    public SimulationScenario(string scenarioName, string description, string targetEndpoint, string payloadTemplate)
    {
        ScenarioName = !string.IsNullOrWhiteSpace(scenarioName) ? scenarioName : throw new ArgumentException("Scenarionavn skal angives", nameof(scenarioName));
        Description = description ?? string.Empty;
        TargetEndpoint = !string.IsNullOrWhiteSpace(targetEndpoint) ? targetEndpoint : throw new ArgumentException("Mål-endpoint skal angives", nameof(targetEndpoint));
        PayloadTemplate = payloadTemplate ?? string.Empty;
    }

    public void MarkAsExecuted()
    {
        LastExecutedAt = DateTime.UtcNow;
    }
}