using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Services.AlertEngine.Evaluators;
using Moq;

namespace MineWatch.Api.Tests;

public class GeoFenceRuleEvaluatorTests
{
    private readonly GeoFenceRuleEvaluator _evaluator = new(Mock.Of<ILogger<GeoFenceRuleEvaluator>>());

    private static TelemetryReading CreateReading(double lat, double lon) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        Speed = 10,
        Lat = lat,
        Lon = lon,
        Timestamp = DateTime.UtcNow
    };

    private static AlertRule CreateCircleRule(double centerLat, double centerLon, double radius, string mode = "outside")
        => new()
        {
            Id = Guid.NewGuid(),
            RuleType = AlertRuleType.GeoFence,
            GeoFenceSpec = $$"""{"type":"circle","mode":"{{mode}}","center":[{{centerLat}},{{centerLon}}],"radius":{{radius}},"points":null}"""
        };

    [Fact]
    public async Task EvaluateAsync_OutsideMode_VehicleOutsideZone_ReturnsAlert()
    {
        // center at (-31.95, 115.86), radius 300m, reading far away
        var rule = CreateCircleRule(-31.95, 115.86, 300, "outside");
        var reading = CreateReading(-32.0, 116.0);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.NotNull(alert);
        Assert.Equal(rule.Id, alert!.RuleId);
        Assert.Contains("left", alert.Message);
    }

    [Fact]
    public async Task EvaluateAsync_OutsideMode_VehicleInsideZone_ReturnsNull()
    {
        // center at (-31.95, 115.86), radius 300m, reading at center
        var rule = CreateCircleRule(-31.95, 115.86, 300, "outside");
        var reading = CreateReading(-31.95, 115.86);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.Null(alert);
    }

    [Fact]
    public async Task EvaluateAsync_InsideMode_VehicleInsideZone_ReturnsAlert()
    {
        var rule = CreateCircleRule(-31.95, 115.86, 300, "inside");
        var reading = CreateReading(-31.95, 115.86);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.NotNull(alert);
        Assert.Contains("entered", alert!.Message);
    }

    [Fact]
    public async Task EvaluateAsync_InsideMode_VehicleOutsideZone_ReturnsNull()
    {
        var rule = CreateCircleRule(-31.95, 115.86, 300, "inside");
        var reading = CreateReading(-32.0, 116.0);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.Null(alert);
    }

    [Fact]
    public async Task EvaluateAsync_NullGeoFenceSpec_ReturnsNull()
    {
        var rule = new AlertRule { Id = Guid.NewGuid(), RuleType = AlertRuleType.GeoFence, GeoFenceSpec = null };
        var reading = CreateReading(-32.0, 116.0);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.Null(alert);
    }

    [Fact]
    public async Task EvaluateAsync_PolygonMode_VehicleInside_ReturnsAlert()
    {
        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            RuleType = AlertRuleType.GeoFence,
            GeoFenceSpec = """{"type":"polygon","mode":"inside","center":null,"radius":0,"points":[[-32.0,116.0],[-31.9,116.0],[-31.9,116.1],[-32.0,116.1]]}"""
        };
        // point inside the polygon
        var reading = CreateReading(-31.95, 116.05);

        var alert = await _evaluator.EvaluateAsync(rule, reading);

        Assert.NotNull(alert);
    }

    [Fact]
    public void RuleType_IsGeoFence()
    {
        Assert.Equal(AlertRuleType.GeoFence, _evaluator.RuleType);
    }
}
