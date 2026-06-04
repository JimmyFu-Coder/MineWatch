using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Services.AlertEngine.Evaluators;
using Moq;

namespace MineWatch.Api.Tests;

public class SpeedRuleEvaluatorTests
{
    private readonly SpeedRuleEvaluator _evaluator = new(Mock.Of<ILogger<SpeedRuleEvaluator>>());

    private static TelemetryReading CreateReading(double speed) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        Speed = speed,
        Lat = -32.0,
        Lon = 116.0,
        Timestamp = DateTime.UtcNow
    };

    private static AlertRule CreateSpeedRule(double threshold) => new()
    {
        Id = Guid.NewGuid(),
        RuleType = AlertRuleType.Speed,
        SpeedThreshold = threshold
    };

    [Fact]
    public async Task EvaluateAsync_SpeedExceedsThreshold_ReturnsAlert()
    {
        var rule = CreateSpeedRule(40);
        var reading = CreateReading(60);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.NotNull(alert);
        Assert.Equal(rule.Id, alert!.RuleId);
        Assert.Equal(reading.DeviceId, alert.DeviceId);
        Assert.Equal(AlertStatus.Active, alert.Status);
        Assert.Contains("60", alert.Message);
    }

    [Fact]
    public async Task EvaluateAsync_SpeedBelowThreshold_ReturnsNull()
    {
        var rule = CreateSpeedRule(40);
        var reading = CreateReading(30);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.Null(alert);
    }

    [Fact]
    public async Task EvaluateAsync_SpeedEqualsThreshold_ReturnsNull()
    {
        var rule = CreateSpeedRule(40);
        var reading = CreateReading(40);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.Null(alert);
    }

    [Fact]
    public void RuleType_IsSpeed()
    {
        Assert.Equal(AlertRuleType.Speed, _evaluator.RuleType);
    }
}
