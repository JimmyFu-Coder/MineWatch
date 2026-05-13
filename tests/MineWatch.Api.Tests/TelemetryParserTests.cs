using MineWatch.Api.Services;

namespace MineWatch.Api.Tests;

public class TelemetryParserTests
{
    [Fact]                                                                             
    public void Parse_WhenMissingField_ThrowsKeyNotFoundException()
    {
        var payload = """{"vehicle_no":"TRUCK-001"}""";
                                                                                     
        Assert.Throws<KeyNotFoundException>(() => TelemetryParser.Parse(payload));     
    } 
}