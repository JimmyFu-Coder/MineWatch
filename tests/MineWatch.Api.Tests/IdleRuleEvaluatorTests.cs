using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Services.AlertEngine.Evaluators;
using Moq;

namespace MineWatch.Api.Tests;

public class IdleRuleEvaluatorTests
{
    private static TelemetryReading CreateReading(double speed, Guid? deviceId = null) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = deviceId ?? Guid.NewGuid(),
        Speed = speed,
        Lat = -32.0,
        Lon = 116.0,
        Timestamp = DateTime.UtcNow
    };

    private static AlertRule CreateIdleRule(double idleSpeedThreshold = 2, double idleDurationSeconds = 300)
        => new()
        {
            Id = Guid.NewGuid(),
            RuleType = AlertRuleType.Idle,
            IdleSpeedThreshold = idleSpeedThreshold,
            IdleDurationSeconds = idleDurationSeconds
        };

    [Fact]
    public async Task EvaluateAsync_FirstReading_RecordsTimeNoAlert()
    {
        var evaluator = new IdleRuleEvaluator(Mock.Of<ILogger<IdleRuleEvaluator>>());
        var rule = CreateIdleRule();
        var reading = CreateReading(0);

        var alert = await evaluator.EvaluateAsync(rule, reading);

        Assert.Null(alert);
    }

    [Fact]
    public async Task EvaluateAsync_StillWithinDuration_NoAlert()
    {
        var evaluator = new IdleRuleEvaluator(Mock.Of<ILogger<IdleRuleEvaluator>>());
        var rule = CreateIdleRule(idleDurationSeconds: 300);
        var deviceId = Guid.NewGuid();

        // first reading - records start time
        var reading1 = CreateReading(0, deviceId);
        await evaluator.EvaluateAsync(rule, reading1);

        // second reading immediately after - not enough time elapsed
        var reading2 = CreateReading(0, deviceId);
        var alert = await evaluator.EvaluateAsync(rule, reading2);

        Assert.Null(alert);
    }

    [Fact]
    public async Task EvaluateAsync_MovingAboveIdleThreshold_ResetsTimer()
    {
        var evaluator = new IdleRuleEvaluator(Mock.Of<ILogger<IdleRuleEvaluator>>());
        var rule = CreateIdleRule();
        var deviceId = Guid.NewGuid();

        // first reading - idle
        var reading1 = CreateReading(0, deviceId);
        await evaluator.EvaluateAsync(rule, reading1);

        // second reading - moving, resets timer
        var reading2 = CreateReading(10, deviceId);
        await evaluator.EvaluateAsync(rule, reading2);

        // third reading - idle again, timer just reset, no alert
        var reading3 = CreateReading(0, deviceId);
        var alert = await evaluator.EvaluateAsync(rule, reading3);

        Assert.Null(alert);
    }

    [Fact]
    public async Task EvaluateAsync_NullThresholds_ReturnsNull()
    {
        var evaluator = new IdleRuleEvaluator(Mock.Of<ILogger<IdleRuleEvaluator>>());
        var rule = new AlertRule { Id = Guid.NewGuid(), RuleType = AlertRuleType.Idle, IdleSpeedThreshold = null, IdleDurationSeconds = null };
        var reading = CreateReading(0);

        var alert = await evaluator.EvaluateAsync(rule, reading);

        Assert.Null(alert);
    }

    [Fact]
    public void RuleType_IsIdle()
    {
        var evaluator = new IdleRuleEvaluator(Mock.Of<ILogger<IdleRuleEvaluator>>());
        Assert.Equal(AlertRuleType.Idle, evaluator.RuleType);
    }
}
