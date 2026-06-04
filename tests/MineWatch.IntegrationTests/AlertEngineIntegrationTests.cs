using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;
using MineWatch.IntegrationTests.Infrastructure;
using MineWatch.Worker.Services.AlertEngine;
using MineWatch.Worker.Services.AlertEngine.Evaluators;
using MineWatch.Worker.Services.Notifications;
using Moq;

namespace MineWatch.IntegrationTests;

public class AlertEngineIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AlertEngineIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(MineWatchDbContext db, AlertEngine engine)> CreateEngine(string dbName)
    {
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        var dbContext = new MineWatchDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));

        var evaluators = new List<IRuleEvaluator>
        {
            new SpeedRuleEvaluator(Mock.Of<ILogger<SpeedRuleEvaluator>>()),
            new GeoFenceRuleEvaluator(Mock.Of<ILogger<GeoFenceRuleEvaluator>>()),
            new IdleRuleEvaluator(Mock.Of<ILogger<IdleRuleEvaluator>>())
        };

        var engine = new AlertEngine(factory.Object, evaluators,
            Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());
        return (dbContext, engine);
    }

    [Fact]
    public async Task SpeedRule_TriggersAlert_WhenExceeded()
    {
        var (db, engine) = await CreateEngine("Integ_Speed");
        var deviceId = Guid.NewGuid();

        db.Devices.Add(new Device { Id = deviceId, Name = "Truck-001", Type = "Truck", Status = DeviceStatus.Online, CreatedAt = DateTime.UtcNow });
        db.AlertRules.Add(new AlertRule
        {
            Id = Guid.NewGuid(), Name = "Speed 40", RuleType = AlertRuleType.Speed,
            SpeedThreshold = 40, DeviceType = null, IsEnabled = true, CoolDownSeconds = 0, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var reading = new TelemetryReading
        {
            Id = Guid.NewGuid(), DeviceId = deviceId, Speed = 60,
            Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow
        };
        await engine.EvaluateAsync(reading);

        var alerts = await db.Alerts.ToListAsync();
        Assert.Single(alerts);
        Assert.Equal(AlertStatus.Active, alerts[0].Status);
        Assert.Contains("60", alerts[0].Message);
    }

    [Fact]
    public async Task GeoFenceRule_TriggersAlert_WhenOutsideZone()
    {
        var (db, engine) = await CreateEngine("Integ_GeoFence");

        db.AlertRules.Add(new AlertRule
        {
            Id = Guid.NewGuid(), Name = "Restricted Zone", RuleType = AlertRuleType.GeoFence,
            GeoFenceSpec = """{"type":"circle","mode":"outside","center":[-31.95,115.86],"radius":300,"points":null}""",
            DeviceType = null, IsEnabled = true, CoolDownSeconds = 0, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var reading = new TelemetryReading
        {
            Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), Speed = 10,
            Lat = -32.0, Lon = 116.0, Timestamp = DateTime.UtcNow
        };
        await engine.EvaluateAsync(reading);

        var alerts = await db.Alerts.ToListAsync();
        Assert.Single(alerts);
        Assert.Contains("left", alerts[0].Message);
    }

    [Fact]
    public async Task Cooldown_BlocksSecondTrigger_ForSameRuleAndDevice()
    {
        var (db, engine) = await CreateEngine("Integ_Cooldown");
        var deviceId = Guid.NewGuid();

        db.AlertRules.Add(new AlertRule
        {
            Id = Guid.NewGuid(), Name = "Speed 10", RuleType = AlertRuleType.Speed,
            SpeedThreshold = 10, DeviceType = null, IsEnabled = true, CoolDownSeconds = 3600, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var reading1 = new TelemetryReading { Id = Guid.NewGuid(), DeviceId = deviceId, Speed = 60, Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow };
        var reading2 = new TelemetryReading { Id = Guid.NewGuid(), DeviceId = deviceId, Speed = 80, Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow };

        await engine.EvaluateAsync(reading1);
        await engine.EvaluateAsync(reading2);

        var alerts = await db.Alerts.ToListAsync();
        Assert.Single(alerts);
    }

    [Fact]
    public async Task DeviceTypeFilter_BlocksAlert_WhenTypeMismatch()
    {
        var (db, engine) = await CreateEngine("Integ_DeviceType");
        var deviceId = Guid.NewGuid();

        db.Devices.Add(new Device { Id = deviceId, Name = "Excavator-001", Type = "Excavator", Status = DeviceStatus.Online, CreatedAt = DateTime.UtcNow });
        db.AlertRules.Add(new AlertRule
        {
            Id = Guid.NewGuid(), Name = "Truck Speed", RuleType = AlertRuleType.Speed,
            SpeedThreshold = 10, DeviceType = "Truck", IsEnabled = true, CoolDownSeconds = 0, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var reading = new TelemetryReading
        {
            Id = Guid.NewGuid(), DeviceId = deviceId, Speed = 60,
            Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow
        };
        await engine.EvaluateAsync(reading);

        Assert.Empty(await db.Alerts.ToListAsync());
    }

    [Fact]
    public async Task MultipleRules_CanAllTrigger()
    {
        var (db, engine) = await CreateEngine("Integ_MultiRule");
        var deviceId = Guid.NewGuid();

        db.Devices.Add(new Device { Id = deviceId, Name = "Truck-001", Type = "Truck", Status = DeviceStatus.Online, CreatedAt = DateTime.UtcNow });
        db.AlertRules.AddRange(
            new AlertRule { Id = Guid.NewGuid(), Name = "Speed 10", RuleType = AlertRuleType.Speed, SpeedThreshold = 10, DeviceType = "Truck", IsEnabled = true, CoolDownSeconds = 0, CreatedAt = DateTime.UtcNow },
            new AlertRule { Id = Guid.NewGuid(), Name = "Speed 20", RuleType = AlertRuleType.Speed, SpeedThreshold = 20, DeviceType = null, IsEnabled = true, CoolDownSeconds = 0, CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var reading = new TelemetryReading
        {
            Id = Guid.NewGuid(), DeviceId = deviceId, Speed = 60,
            Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow
        };
        await engine.EvaluateAsync(reading);

        var alerts = await db.Alerts.ToListAsync();
        Assert.Equal(2, alerts.Count);
    }

    [Fact]
    public async Task EvaluatorException_DoesNotPropagate()
    {
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("Integ_EvaluatorException").Options;
        var dbContext = new MineWatchDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));

        var failingEvaluator = new Mock<IRuleEvaluator>();
        failingEvaluator.Setup(e => e.RuleType).Returns(AlertRuleType.Speed);
        failingEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<AlertRule>(), It.IsAny<TelemetryReading>()))
            .ThrowsAsync(new Exception("evaluator crashed"));

        var engine = new AlertEngine(factory.Object, [failingEvaluator.Object],
            Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        dbContext.AlertRules.Add(
            new AlertRule { Id = Guid.NewGuid(), Name = "Speed 10", RuleType = AlertRuleType.Speed, SpeedThreshold = 10, DeviceType = null, IsEnabled = true, CoolDownSeconds = 0, CreatedAt = DateTime.UtcNow }
        );
        await dbContext.SaveChangesAsync();

        var reading = new TelemetryReading { Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), Speed = 60, Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow };
        // Should not throw
        await engine.EvaluateAsync(reading);

        // No alerts persisted since evaluator threw
        Assert.Empty(await dbContext.Alerts.ToListAsync());
    }
}
