namespace DefaultNamespace;

public class TelemetryMessage
{
    public Guid Id { get; set; }
    public string VehicleNo { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Lat { get; set; }                                                
    public double Lon { get; set; }
    public double Speed { get; set; }
    public double Heading { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid DeviceId { get; set; }
}