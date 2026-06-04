using Microsoft.Extensions.Configuration;
using TruckMocker.Models;
using TruckMocker.Services;

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var simulation = new SimulationConfig
{
    VehicleCount = int.TryParse(config["Simulation:VehicleCount"], out var vc) ? vc : 5,
    PointsPerVehicle = 300,
    FrequencyHz = 1,
    AvgSpeedMps = 30,
    Bounds = new[]
    {
        (-32.265450, 116.023386),
        (-32.265093, 116.024874),
        (-32.267994, 116.024709),
        (-32.266975, 116.026851),
    }
};

var mqttConfig = new MqttConfig
{
    Server = config["Mqtt:Server"] ?? "localhost",
    Port = int.TryParse(config["Mqtt:Port"], out var port) ? port : 1883
};

// 2. Generate trajectory
var generator = new TrajectoryGenerator(simulation);
var records = generator.Generate();
Console.WriteLine($"Generated {records.Count} records for {simulation.VehicleCount} vehicles");

// 3. Publish MQTT
await using var publisher = new MqttPublisher(mqttConfig);
await publisher.ConnectAsync();
Console.WriteLine($"Connected to MQTT broker at {mqttConfig.Server}:{mqttConfig.Port}");

// 4. Send loop
var grouped = records.GroupBy(r => r.VehicleNo).ToList();
var totalSent = 0;

var tasks = grouped.Select(async vehicleGroup =>
{
    var vehicleId = vehicleGroup.Key;
    var sent = 0;

    foreach (var record in vehicleGroup.OrderBy(r => r.Timestamp))
    {
        await publisher.PublishAsync(vehicleId, record);
        sent++;
        Interlocked.Add(ref totalSent, 1);

        if (simulation.FrequencyHz > 0)
            await Task.Delay(1000 / simulation.FrequencyHz);
    }

    Console.WriteLine($"[{vehicleId}] sent {sent} messages");
    return sent;
}).ToList();

await Task.WhenAll(tasks);
Console.WriteLine($"Sent {totalSent} messages to broker");
