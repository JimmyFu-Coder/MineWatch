using TruckMocker.Models;

namespace TruckMocker.Services;

public class TrajectoryGenerator
{
    private readonly SimulationConfig _config;

    public TrajectoryGenerator(SimulationConfig config)
    {
        _config = config;
    }
    public List<TrajectoryRecord> Generate()
    {
        var records = new List<TrajectoryRecord>();
        var random = new Random(42);
        var startTime = DateTime.UtcNow;
        var corners = _config.Bounds.Select(b => (b.Item1, b.Item2)).ToArray();

        for (int v = 0; v < _config.VehicleCount; v++)
        {
            var vehicleId = $"VEHICLE_{v + 1:D3}";
            var vehicleSeed = random.Next(1000);

            for (int p = 0; p < _config.PointsPerVehicle; p++)
            {
                var timestamp = startTime.AddSeconds(p * _config.FrequencyHz);
                var (baseLat, baseLon, heading) = GetLoopPosition(p, _config.PointsPerVehicle, corners);
                var jitter = GetJitter(vehicleSeed, p);

                records.Add(new TrajectoryRecord
                {
                    VehicleNo = vehicleId,
                    Timestamp = timestamp,
                    Lat = baseLat + jitter.lat,
                    Lon = baseLon + jitter.lon,
                    Speed = _config.AvgSpeedMps * (0.8 + random.NextDouble() * 0.4),
                    Heading = heading
                });
            }
        }
        return records;
    }
    private (double lat, double lon, double heading) GetLoopPosition(int pointIndex, int totalPoints, (double lat, double lon)[] corners)
    {
        var segmentLength = totalPoints / 4;
        var segment = pointIndex / segmentLength;
        var progress = (double)(pointIndex % segmentLength) / segmentLength;

        var from = corners[segment % 4];
        var to = corners[(segment + 1) % 4];

        return (
            from.lat + (to.lat - from.lat) * progress,
            from.lon + (to.lon - from.lon) * progress,
            (Math.Atan2(to.lon - from.lon, to.lat - from.lat) * 180 / Math.PI + 360) % 360
        );
    }
    private (double lat, double lon) GetJitter(int vehicleSeed, int pointIndex)
    {
        var jitterRandom = new Random(vehicleSeed + pointIndex);
        return (
            (jitterRandom.NextDouble() - 0.5) * 0.00005,
            (jitterRandom.NextDouble() - 0.5) * 0.00005
        );
    }
}