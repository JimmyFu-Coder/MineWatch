using System.Text.Json;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Worker.Services;

public static class TelemetryParser
{
    public static  TelemetryReading Parse(string payload)
    {
        var json = JsonSerializer.Deserialize<JsonElement>(payload);
        return new TelemetryReading
        {
            Id = Guid.NewGuid(),
            VehicleNo = json.GetProperty("vehicle_no").GetString()!,
            Timestamp = json.GetProperty("timestamp").GetDateTime(),
            Lat = json.GetProperty("lat").GetDouble(),
            Lon = json.GetProperty("lon").GetDouble(),
            Speed = json.GetProperty("speed_mps").GetDouble(),
            Heading = json.GetProperty("heading").GetDouble(),
            CreatedAt = DateTime.UtcNow
        };
    }
}