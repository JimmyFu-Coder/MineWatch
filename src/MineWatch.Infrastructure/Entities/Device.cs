namespace MineWatch.Infrastructure.Entities;

public enum DeviceStatus
{
    Online,
    Offline,
    Maintenance
}

public class Device
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DeviceStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}