namespace MineWatch.Infrastructure.Entities;

public enum AlertStatus
{
    Active,
    Acknowledged,
    Resolved
}

public class Alert
{
    public Guid Id { get; set; }
    public Guid RuleId { get; set; }
    public AlertRule Rule { get; set; } = null!;
    public Guid DeviceId { get; set; }
    public Device Device { get; set; } = null!;
    public Guid TelemetryReadingId { get; set; }
    public TelemetryReading TelemetryReading { get; set; } = null!;
    public AlertStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? TriggerLat { get; set; }
    public double? TriggerLon { get; set; }
    public double? TriggerSpeed { get; set; }
    public DateTime TriggeredAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
