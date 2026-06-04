using System.Text.Json;
using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Worker.Services.AlertEngine.Evaluators;

public class GeoFenceRuleEvaluator(ILogger<GeoFenceRuleEvaluator> logger) : IRuleEvaluator
{
    public AlertRuleType RuleType => AlertRuleType.GeoFence;

    public Task<Alert?> EvaluateAsync(AlertRule rule, TelemetryReading reading)
    {
        if (string.IsNullOrEmpty(rule.GeoFenceSpec))
            return Task.FromResult<Alert?>(null);

        var spec = JsonSerializer.Deserialize<GeoFenceSpec>(rule.GeoFenceSpec,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (spec == null)
            return Task.FromResult<Alert?>(null);

        bool isViolated = spec.Type switch
        {
            "circle" => EvaluateCircle(spec, reading),
            "polygon" => EvaluatePolygon(spec, reading),
            _ => false
        };

        if (!isViolated)
            return Task.FromResult<Alert?>(null);

        var direction = spec.Mode == "outside" ? "left" : "entered";
        logger.LogDebug("GeoFence violation: device {DeviceId} {Direction} zone '{Zone}'",
            reading.DeviceId, direction, rule.Name);

        return Task.FromResult<Alert?>(new Alert
        {
            Id = Guid.NewGuid(),
            RuleId = rule.Id,
            DeviceId = reading.DeviceId,
            TelemetryReadingId = reading.Id,
            Status = AlertStatus.Active,
            Message = $"Vehicle {direction} geo-fence zone '{rule.Name}'",
            TriggerLat = reading.Lat,
            TriggerLon = reading.Lon,
            TriggerSpeed = reading.Speed,
            TriggeredAt = DateTime.UtcNow
        });
    }

    private static bool EvaluateCircle(GeoFenceSpec spec, TelemetryReading reading)
    {
        if (spec.Center is not { Length: 2 })
            return false;

        var inside = GeoHelper.IsInCircle(reading.Lat, reading.Lon,
            spec.Center[0], spec.Center[1], spec.Radius);

        return spec.Mode == "outside" ? !inside : inside;
    }

    private static bool EvaluatePolygon(GeoFenceSpec spec, TelemetryReading reading)
    {
        if (spec.Points == null || spec.Points.Length < 3)
            return false;

        var inside = GeoHelper.IsInPolygon(reading.Lat, reading.Lon, spec.Points);
        return spec.Mode == "outside" ? !inside : inside;
    }
}

public class GeoFenceSpec
{
    public string? Type { get; set; }
    public string? Mode { get; set; }
    public double[]? Center { get; set; }
    public double Radius { get; set; }
    public double[][]? Points { get; set; }
}
