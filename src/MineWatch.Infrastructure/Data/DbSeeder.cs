using Microsoft.EntityFrameworkCore;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(MineWatchDbContext dbContext)
    {
        if (await dbContext.Devices.AnyAsync())
            return;
        var devices = new[]
        {
            new Device { Id = Guid.NewGuid(), Name = "Truck-001", Type = "Truck" },
            new Device { Id = Guid.NewGuid(), Name = "Truck-002", Type = "Truck" },
            new Device { Id = Guid.NewGuid(), Name = "Truck-003", Type = "Truck" },
        };
        dbContext.Devices.AddRange(devices);
        await dbContext.SaveChangesAsync();
        if (!await dbContext.AlertRules.AnyAsync())
        {
            var rules = new[]
            {
          new AlertRule
          {
              Id = Guid.NewGuid(),
              Name = "Speed Limit - Trucks",
              RuleType = AlertRuleType.Speed,
              Severity = AlertSeverity.High,
              SpeedThreshold = 40,  // 40 km/h
              DeviceType = "Truck",
              CoolDownSeconds = 300,
              IsEnabled = true,
              CreatedAt = DateTime.UtcNow
          },
          new AlertRule
          {
              Id = Guid.NewGuid(),
              Name = "Restricted Zone - Office Area",
              RuleType = AlertRuleType.GeoFence,
              Severity = AlertSeverity.Critical,
              GeoFenceSpec = """{"type":"circle","mode":"outside","center":[-31.95,115.86],"radius":300,"points":null}""",
              DeviceType = null,
              CoolDownSeconds = 0,
              IsEnabled = true,
              CreatedAt = DateTime.UtcNow
          },
          new AlertRule
          {
              Id = Guid.NewGuid(),
              Name = "Idle Timeout - Trucks",
              RuleType = AlertRuleType.Idle,
              Severity = AlertSeverity.Medium,
              IdleSpeedThreshold = 2,  // speed below 2 km/h counts as idle
              IdleDurationSeconds = 300,  // 5 minutes
              DeviceType = "Truck",
              CoolDownSeconds = 600,
              IsEnabled = true,
              CreatedAt = DateTime.UtcNow
          }
      };
            dbContext.AlertRules.AddRange(rules);
            await dbContext.SaveChangesAsync();
        }
    }
}
