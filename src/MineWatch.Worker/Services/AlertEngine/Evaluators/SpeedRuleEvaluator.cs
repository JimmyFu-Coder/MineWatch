using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Worker.Services.AlertEngine.Evaluators;

public class SpeedRuleEvaluator(ILogger<SpeedRuleEvaluator> logger) : IRuleEvaluator
{
    public AlertRuleType RuleType => AlertRuleType.Speed;

    public Task<Alert?> EvaluateAsync(AlertRule rule, TelemetryReading reading)
    {
        if (reading.Speed <= rule.SpeedThreshold)
            return Task.FromResult<Alert?>(null);

        logger.LogDebug("Speed threshold exceeded: {Speed} > {Threshold} for device {DeviceId}",
            reading.Speed, rule.SpeedThreshold, reading.DeviceId);

        return Task.FromResult<Alert?>(new Alert
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            DeviceId = reading.DeviceId,
            TelemetryReadingId = reading.Id,
            Status = AlertStatus.Active,
            Message = $"Speed {reading.Speed:F1} km/h exceeded threshold {rule.SpeedThreshold:F1} km/h",
            TriggerLat = reading.Lat,
            TriggerLon = reading.Lon,
            TriggerSpeed = reading.Speed,
            TriggeredAt = DateTime.UtcNow
        });
    }
}
