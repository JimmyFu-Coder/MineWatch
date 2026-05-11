using Microsoft.EntityFrameworkCore;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(MineWatchDbContext dbContext)
    {
        if (await dbContext.Devices.AnyAsync()) return;
        var devices = new[]
        {
            new Device { Id = Guid.NewGuid(), Name = "Truck-001", Type = "Truck" },
            new Device { Id = Guid.NewGuid(), Name = "Truck-002", Type = "Truck" },
            new Device { Id = Guid.NewGuid(), Name = "Truck-003", Type = "Truck" },
        };
        dbContext.Devices.AddRange(devices);
        await dbContext.SaveChangesAsync();
    }
}