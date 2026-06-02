using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Worker.Services.AlertEngine.Evaluators;

public class IdleRuleEvaluator(ILogger<IdleRuleEvaluator> logger) : IRuleEvaluator
{

    private readonly ConcurrentDictionary<Guid, DateTime> _lastActiveTime = new();

    public AlertRuleType RuleType => AlertRuleType.Idle;

    public Task<Alert?> EvaluateAsync(AlertRule rule, TelemetryReading reading)
    {
        if (rule.IdleSpeedThreshold == null || rule.IdleDurationSeconds == null)
            return Task.FromResult<Alert?>(null);

        if (reading.Speed > rule.IdleSpeedThreshold
            || !_lastActiveTime.TryGetValue(reading.DeviceId, out var lastActive))
        {
            _lastActiveTime[reading.DeviceId] = DateTime.UtcNow;
            return Task.FromResult<Alert?>(null);
        }

        double idleSeconds = (DateTime.UtcNow - lastActive).TotalSeconds;
        if (idleSeconds < rule.IdleDurationSeconds)
            return Task.FromResult<Alert?>(null);

        logger.LogDebug("Idle timeout: device {DeviceId} idle for {Seconds:F0}s > {Threshold}s",
            reading.DeviceId, idleSeconds, rule.IdleDurationSeconds);

        return Task.FromResult<Alert?>(new Alert
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            DeviceId = reading.DeviceId,
            TelemetryReadingId = reading.Id,
            Status = AlertStatus.Active,
            Message = $"Vehicle idle for {(int)idleSeconds / 60} min (threshold: {(int)rule.IdleDurationSeconds / 60} min)",
            TriggerLat = reading.Lat,
            TriggerLon = reading.Lon,
            TriggerSpeed = reading.Speed,
            TriggeredAt = DateTime.UtcNow
        });
    }
}
