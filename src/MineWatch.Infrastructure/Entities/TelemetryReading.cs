namespace MineWatch.Infrastructure.Entities;

public class TelemetryReading
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public float Temperature { get; set; }
    public float Pressure { get; set; }
    public Device Device { get; set; } = null!;
}