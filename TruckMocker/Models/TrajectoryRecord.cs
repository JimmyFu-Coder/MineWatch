using CsvHelper.Configuration.Attributes;

namespace TruckMocker.Models;

public class TrajectoryRecord
{
    [Name("vehicle_no")]
    public string VehicleNo { get; set; }

    [Name("timestamp")]
    public DateTime Timestamp { get; set; }

    [Name("lat")]
    public double Lat { get; set; }

    [Name("lon")]
    public double Lon { get; set; }

    [Name("speed_mps")]
    public double Speed { get; set; }

    [Name("heading")]
    public double Heading { get; set; }
}