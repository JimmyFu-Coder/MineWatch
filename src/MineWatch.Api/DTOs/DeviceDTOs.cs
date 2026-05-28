using MineWatch.Infrastructure.Entities;

namespace MineWatch.Api.DTOs;


public record CreateDeviceRequest(string Name, string Type);
public record UpdateDeviceRequest(string? Name, string? Type);
public record DeviceResponse(Guid Id, string Name, string Type, DeviceStatus Status, DateTime CreatedAt, DateTime? UpdatedAt);

public record PageResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPage => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPage;
    public bool HasPreviousPage => Page > 1; 
}
