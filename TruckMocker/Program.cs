using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using MQTTnet;

// ============ 配置参数 ============
var config = new SimulationConfig
{
    VehicleCount = 5,
    PointsPerVehicle = 300,
    FrequencyHz = 1,
    AvgSpeedMps = 30,
    Bounds = new[]
    {
        (-32.265450, 116.023386),  // SW
        (-32.265093, 116.024874),  // SE
        (-32.267994, 116.024709),  // NE
        (-32.266975, 116.026851),  // NW
    }
};

var mqttConfig = new MqttConfig
{
    Server = "localhost",
    Port = 1883
};

// ============ 生成轨迹 ============
var generator = new TrajectoryGenerator(config);
var records = generator.Generate();

Console.WriteLine($"Generated {records.Count} records for {config.VehicleCount} vehicles");

// ============ 连接 MQTT broker ============
var mqttFactory = new MqttClientFactory();
using var mqttClient = mqttFactory.CreateMqttClient();

var mqttOptions = new MqttClientOptionsBuilder()
    .WithTcpServer(mqttConfig.Server, mqttConfig.Port)
    .Build();

await mqttClient.ConnectAsync(mqttOptions);
Console.WriteLine($"Connected to MQTT broker at {mqttConfig.Server}:{mqttConfig.Port}");

// ============ 发布消息（并行） ============
var grouped = records.GroupBy(r => r.VehicleNo).ToList();
var totalSent = 0;

var publishTasks = grouped.Select(async vehicleGroup =>
{
    var vehicleId = vehicleGroup.Key;
    var topic = vehicleId;
    var vehicleSent = 0;

    foreach (var record in vehicleGroup.OrderBy(r => r.Timestamp))
    {
        var payload = new
        {
            vehicle_no = record.VehicleNo,
            timestamp = record.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            lat = record.Lat,
            lon = record.Lon,
            speed_mps = Math.Round(record.Speed, 2),
            heading = Math.Round(record.Heading, 2)
        };

        var json = JsonSerializer.Serialize(payload);
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .Build();

        await mqttClient.PublishAsync(message);
        vehicleSent++;
        Interlocked.Add(ref totalSent, 1);

        if (config.FrequencyHz > 0)
        {
            await Task.Delay(1000 / config.FrequencyHz);
        }
    }

    Console.WriteLine($"[{vehicleId}] sent {vehicleSent} messages");
    return vehicleSent;
}).ToList();

await Task.WhenAll(publishTasks);

Console.WriteLine($"Sent {totalSent} messages to broker");

Console.WriteLine($"Sent {totalSent} messages to broker");
await mqttClient.DisconnectAsync();

// ============ 类型定义（必须在可执行代码之后） ============
public record SimulationConfig
{
    public int VehicleCount { get; init; } = 5;
    public int PointsPerVehicle { get; init; } = 300;
    public int FrequencyHz { get; init; } = 1;
    public double AvgSpeedMps { get; init; } = 30;
    public (double lat, double lon)[] Bounds { get; init; } = Array.Empty<(double, double)>();
}

public record MqttConfig
{
    public string Server { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
}

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
        var corners = _config.Bounds.Select(b => (lat: b.Item1, lon: b.Item2)).ToArray();

        for (int v = 0; v < _config.VehicleCount; v++)
        {
            var vehicleId = $"VEHICLE_{v + 1:D3}";
            var vehicleSeed = random.Next(1000);

            for (int p = 0; p < _config.PointsPerVehicle; p++)
            {
                var timestamp = startTime.AddSeconds(p * _config.FrequencyHz);
                var (baseLat, baseLon, heading) = GetLoopPosition(p, _config.PointsPerVehicle, corners);
                var jitter = GetJitter(vehicleSeed, p);
                var lat = baseLat + jitter.lat;
                var lon = baseLon + jitter.lon;
                var speed = _config.AvgSpeedMps * (0.8 + random.NextDouble() * 0.4);

                records.Add(new TrajectoryRecord
                {
                    VehicleNo = vehicleId,
                    Timestamp = timestamp,
                    Lat = lat,
                    Lon = lon,
                    Speed = speed,
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

        var lat = from.lat + (to.lat - from.lat) * progress;
        var lon = from.lon + (to.lon - from.lon) * progress;

        var dLat = to.lat - from.lat;
        var dLon = to.lon - from.lon;
        var heading = (Math.Atan2(dLon, dLat) * 180 / Math.PI + 360) % 360;

        return (lat, lon, heading);
    }

    private (double lat, double lon) GetJitter(int vehicleSeed, int pointIndex)
    {
        var jitterRandom = new Random(vehicleSeed + pointIndex);
        var latJitter = (jitterRandom.NextDouble() - 0.5) * 0.00005;
        var lonJitter = (jitterRandom.NextDouble() - 0.5) * 0.00005;
        return (latJitter, lonJitter);
    }

    public void WriteCsv(List<TrajectoryRecord> records, string outputPath)
    {
        using var writer = new StreamWriter(outputPath);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
        csv.WriteRecords(records);
    }
}

public interface IEventInjector
{
    void Inject(int vehicleIndex, int pointIndex, ref double? lat, ref double? lon, ref double? speed, ref double? heading);
}