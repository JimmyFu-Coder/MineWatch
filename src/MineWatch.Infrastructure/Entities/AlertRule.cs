namespace MineWatch.Infrastructure.Entities;

public enum AlertRuleType
{
    Speed,
    GeoFence,
    Idle
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}


public class AlertRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } =  string.Empty;
    public AlertRuleType RuleType { get; set; }
    public AlertSeverity Severity { get; set; }
    public double Threshold { get; set; }
    public string? DeviceType { get; set; } = string.Empty;
    public string? GeoFenceSpec { get; set; }
    public int CoolDownSeconds { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
