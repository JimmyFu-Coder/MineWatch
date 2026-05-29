using MineWatch.Worker.Services;

namespace MineWatch.Api.Tests;

public class TelemetryParserTests
{

    [Fact]
    public void Parse_WithValidJson_ReturnsCorrectReading()
    {

        var payload = """{"vehicle_no":"TRUCK-001","timestamp":"2026-05-15T10:00:00Z","lat":-32.265,"lon":116.023,"speed_mps":30.0,"heading":90.0}""";

        var result = TelemetryParser.Parse(payload);

        Assert.Equal("TRUCK-001", result.VehicleNo);
        Assert.Equal(DateTime.Parse("2026-05-15T10:00:00Z").ToUniversalTime(), result.Timestamp);
        Assert.Equal(-32.265, result.Lat);
        Assert.Equal(116.023, result.Lon);
        Assert.Equal(30.0, result.Speed);
        Assert.Equal(90.0, result.Heading);
    }
}
