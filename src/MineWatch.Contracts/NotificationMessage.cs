namespace MineWatch.Contracts;

public class NotificationMessage
{
    public string Type { get; set; } = string.Empty; // "telemetry" or "alert"
    public string Payload { get; set; } = string.Empty; // JSON payload
}
