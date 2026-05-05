using MineWatch.Infrastructure.Entities;

namespace MineWatch.Api.DTOs;


public record CreateDeviceRequest(string Name, string Type);
public record UpdateDeviceRequest(string? Name, string? Type);
public record DeviceResponse(Guid Id, string Name, string Type, DeviceStatus Status, DateTime CreatedAt, DateTime? UpdatedAt);
