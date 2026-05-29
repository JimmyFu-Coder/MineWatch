namespace TruckMocker.Models;

public class SimulationConfig
{
    public int VehicleCount { get; init; } = 5;
    public int PointsPerVehicle { get; init; } = 300;
    public int FrequencyHz { get; init; } = 1;
    public double AvgSpeedMps { get; init; } = 30;
    public (double lat, double lon)[] Bounds { get; init; } = Array.Empty<(double, double)>();
}