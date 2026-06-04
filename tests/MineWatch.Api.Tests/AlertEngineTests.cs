using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Services.AlertEngine;
using MineWatch.Worker.Services.AlertEngine.Evaluators;
using MineWatch.Worker.Services.Notifications;
using Moq;

namespace MineWatch.Api.Tests;

public class AlertEngineTests
{
    private static Mock<IDbContextFactory<MineWatchDbContext>> CreateDbFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // seed alert rules
        using var seedCtx = new MineWatchDbContext(options);
        if (!seedCtx.AlertRules.Any())
        {
            seedCtx.AlertRules.Add(new AlertRule
            {
                Id = Guid.NewGuid(),
                Name = "Speed Test Rule",
                RuleType = AlertRuleType.Speed,
                Severity = AlertSeverity.High,
                SpeedThreshold = 40,
                DeviceType = null,
                CoolDownSeconds = 300,
                IsEnabled = true,
                CreatedAt = DateTime.UtcNow
            });
            seedCtx.SaveChanges();
        }

        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));
        return factory;
    }

    private static TelemetryReading CreateReading(double speed = 60, Guid? deviceId = null) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = deviceId ?? Guid.NewGuid(),
        Speed = speed,
        Lat = -32.0,
        Lon = 116.0,
        Timestamp = DateTime.UtcNow
    };

    [Fact]
    public async Task EvaluateAsync_RuleMatches_CreatesAlert()
    {
        var dbFactory = CreateDbFactory("AlertEngine_Trigger");
        var evaluators = new List<IRuleEvaluator> { new SpeedRuleEvaluator(Mock.Of<ILogger<SpeedRuleEvaluator>>()) };
        var engine = new AlertEngine(dbFactory.Object, evaluators, Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        var reading = CreateReading(speed: 60);
        await engine.EvaluateAsync(reading);

        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("AlertEngine_Trigger").Options;
        using var ctx = new MineWatchDbContext(options);
        var alerts = await ctx.Alerts.ToListAsync();
        Assert.Single(alerts);
        Assert.Equal(AlertStatus.Active, alerts[0].Status);
    }

    [Fact]
    public async Task EvaluateAsync_NoRuleMatch_NoAlert()
    {
        var dbFactory = CreateDbFactory("AlertEngine_NoTrigger");
        var evaluators = new List<IRuleEvaluator> { new SpeedRuleEvaluator(Mock.Of<ILogger<SpeedRuleEvaluator>>()) };
        var engine = new AlertEngine(dbFactory.Object, evaluators, Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        var reading = CreateReading(speed: 20);
        await engine.EvaluateAsync(reading);

        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("AlertEngine_NoTrigger").Options;
        using var ctx = new MineWatchDbContext(options);
        var alerts = await ctx.Alerts.ToListAsync();
        Assert.Empty(alerts);
    }

    [Fact]
    public async Task EvaluateAsync_EvaluatorThrows_DoesNotPropagate()
    {
        var dbFactory = CreateDbFactory("AlertEngine_Exception");
        var failingEvaluator = new Mock<IRuleEvaluator>();
        failingEvaluator.Setup(e => e.RuleType).Returns(AlertRuleType.Speed);
        failingEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<AlertRule>(), It.IsAny<TelemetryReading>()))
            .ThrowsAsync(new Exception("evaluator crashed"));

        var engine = new AlertEngine(dbFactory.Object, [failingEvaluator.Object], Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        var reading = CreateReading();
        // should not throw
        await engine.EvaluateAsync(reading);
    }

    [Fact]
    public async Task EvaluateAsync_DisabledRule_Skipped()
    {
        var dbName = "AlertEngine_Disabled";
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName).Options;

        using (var seedCtx = new MineWatchDbContext(options))
        {
            seedCtx.AlertRules.Add(new AlertRule
            {
                Id = Guid.NewGuid(),
                Name = "Disabled Rule",
                RuleType = AlertRuleType.Speed,
                SpeedThreshold = 10,
                IsEnabled = false,
                CreatedAt = DateTime.UtcNow
            });
            seedCtx.SaveChanges();
        }

        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));

        var evaluators = new List<IRuleEvaluator> { new SpeedRuleEvaluator(Mock.Of<ILogger<SpeedRuleEvaluator>>()) };
        var engine = new AlertEngine(factory.Object, evaluators, Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        var reading = CreateReading(speed: 60);
        await engine.EvaluateAsync(reading);

        using var ctx = new MineWatchDbContext(options);
        Assert.Empty(await ctx.Alerts.ToListAsync());
    }

    [Fact]
    public async Task EvaluateAsync_MultipleRulesMatching_CreatesMultipleAlerts()
    {
        var dbName = "AlertEngine_Multiple";
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName).Options;

        using (var seedCtx = new MineWatchDbContext(options))
        {
            seedCtx.AlertRules.AddRange(
                new AlertRule
                {
                    Id = Guid.NewGuid(),
                    Name = "Speed 10",
                    RuleType = AlertRuleType.Speed,
                    SpeedThreshold = 10,
                    DeviceType = null,
                    IsEnabled = true,
                    CoolDownSeconds = 0,
                    CreatedAt = DateTime.UtcNow
                },
                new AlertRule
                {
                    Id = Guid.NewGuid(),
                    Name = "Speed 20",
                    RuleType = AlertRuleType.Speed,
                    SpeedThreshold = 20,
                    DeviceType = null,
                    IsEnabled = true,
                    CoolDownSeconds = 0,
                    CreatedAt = DateTime.UtcNow
                }
            );
            seedCtx.SaveChanges();
        }

        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));

        var evaluators = new List<IRuleEvaluator> { new SpeedRuleEvaluator(Mock.Of<ILogger<SpeedRuleEvaluator>>()) };
        var engine = new AlertEngine(factory.Object, evaluators, Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        var reading = CreateReading(speed: 60);
        await engine.EvaluateAsync(reading);

        using var ctx = new MineWatchDbContext(options);
        var alerts = await ctx.Alerts.ToListAsync();
        Assert.Equal(2, alerts.Count);
    }

    [Fact]
    public async Task EvaluateAsync_CooldownActive_SkipsAlert()
    {
        var dbName = "AlertEngine_Cooldown";
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName).Options;

        using (var seedCtx = new MineWatchDbContext(options))
        {
            seedCtx.AlertRules.Add(new AlertRule
            {
                Id = Guid.NewGuid(), Name = "Speed 10", RuleType = AlertRuleType.Speed,
                SpeedThreshold = 10, DeviceType = null, IsEnabled = true, CoolDownSeconds = 3600,
                CreatedAt = DateTime.UtcNow
            });
            seedCtx.SaveChanges();
        }

        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));

        var evaluators = new List<IRuleEvaluator> { new SpeedRuleEvaluator(Mock.Of<ILogger<SpeedRuleEvaluator>>()) };
        var engine = new AlertEngine(factory.Object, evaluators, Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        var deviceId = Guid.NewGuid();
        var reading1 = CreateReading(speed: 60, deviceId);
        await engine.EvaluateAsync(reading1);

        var reading2 = CreateReading(speed: 80, deviceId);
        await engine.EvaluateAsync(reading2);

        using var ctx = new MineWatchDbContext(options);
        var alerts = await ctx.Alerts.ToListAsync();
        Assert.Single(alerts); // second trigger blocked by cooldown
    }

    [Fact]
    public async Task EvaluateAsync_DeviceTypeMismatch_SkipsAlert()
    {
        var dbName = "AlertEngine_DeviceTypeMismatch";
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        var deviceId = Guid.NewGuid();

        using (var seedCtx = new MineWatchDbContext(options))
        {
            seedCtx.Devices.Add(new Device
            {
                Id = deviceId, Name = "Excavator-001", Type = "Excavator",
                Status = DeviceStatus.Online, CreatedAt = DateTime.UtcNow
            });
            seedCtx.AlertRules.Add(new AlertRule
            {
                Id = Guid.NewGuid(), Name = "Truck Speed", RuleType = AlertRuleType.Speed,
                SpeedThreshold = 10, DeviceType = "Truck", IsEnabled = true, CoolDownSeconds = 0,
                CreatedAt = DateTime.UtcNow
            });
            seedCtx.SaveChanges();
        }

        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));

        var evaluators = new List<IRuleEvaluator> { new SpeedRuleEvaluator(Mock.Of<ILogger<SpeedRuleEvaluator>>()) };
        var engine = new AlertEngine(factory.Object, evaluators, Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        var reading = new TelemetryReading
        {
            Id = Guid.NewGuid(), DeviceId = deviceId, Speed = 60,
            Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow
        };
        await engine.EvaluateAsync(reading);

        using var ctx = new MineWatchDbContext(options);
        Assert.Empty(await ctx.Alerts.ToListAsync());
    }

    [Fact]
    public async Task EvaluateAsync_DeviceTypeMatches_TriggersAlert()
    {
        var dbName = "AlertEngine_DeviceTypeMatch";
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName).Options;
        var deviceId = Guid.NewGuid();

        using (var seedCtx = new MineWatchDbContext(options))
        {
            seedCtx.Devices.Add(new Device
            {
                Id = deviceId, Name = "Truck-001", Type = "Truck",
                Status = DeviceStatus.Online, CreatedAt = DateTime.UtcNow
            });
            seedCtx.AlertRules.Add(new AlertRule
            {
                Id = Guid.NewGuid(), Name = "Truck Speed", RuleType = AlertRuleType.Speed,
                SpeedThreshold = 10, DeviceType = "Truck", IsEnabled = true, CoolDownSeconds = 0,
                CreatedAt = DateTime.UtcNow
            });
            seedCtx.SaveChanges();
        }

        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));

        var evaluators = new List<IRuleEvaluator> { new SpeedRuleEvaluator(Mock.Of<ILogger<SpeedRuleEvaluator>>()) };
        var engine = new AlertEngine(factory.Object, evaluators, Mock.Of<INotificationPublisher>(), Mock.Of<ILogger<AlertEngine>>());

        var reading = new TelemetryReading
        {
            Id = Guid.NewGuid(), DeviceId = deviceId, Speed = 60,
            Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow
        };
        await engine.EvaluateAsync(reading);

        using var ctx = new MineWatchDbContext(options);
        Assert.Single(await ctx.Alerts.ToListAsync());
    }
}
