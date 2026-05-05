using Microsoft.EntityFrameworkCore;
using MineWatch.Api.DTOs;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Api.Services;



public interface IDeviceService
{
    Task<Device?> GetByIdAsync(Guid id);
    Task<(IEnumerable<Device> Items, int Total)> GetAllAsync(int page, int pageSize);      
    Task<Device> CreateAsync(CreateDeviceRequest request);
    Task<Device?> UpdateAsync(Guid id, UpdateDeviceRequest request);                       
    Task<bool> DeleteAsync(Guid id);
    
}

public class DeviceService : IDeviceService
{
    private readonly MineWatchDbContext _context;
    
    public async Task<Device?> GetByIdAsync(Guid id)
    {
        return await _context.Devices.FindAsync(id);
    }
    
    public async Task<(IEnumerable<Device> Items, int Total)> GetAllAsync(int page, int pageSize)
    {
        var query = _context.Devices.AsQueryable();
        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }
    
    
    public async Task<Device> CreateAsync(CreateDeviceRequest request)
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Status = DeviceStatus.Online,
            CreatedAt = DateTime.UtcNow
        };
        _context.Devices.Add(device);
        await _context.SaveChangesAsync();
        return device;
    }
    
    public async Task<Device?> UpdateAsync(Guid id, UpdateDeviceRequest request)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
            return null;
        device.Name = request.Name ?? device.Name;
        device.Type = request.Type ?? device.Type;
        device.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return device;
    }
    
    public async Task<bool> DeleteAsync(Guid id)
    {
        var device = await _context.Devices.FindAsync(id);
        if (device == null)
            return false;
        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();
        return true;
    }
}
