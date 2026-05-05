using System.Text.Json;
using MQTTnet;
using TruckMocker.Models;

namespace TruckMocker.Services;

public class MqttPublisher : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly MqttConfig _config;
    public MqttPublisher(MqttConfig config)
    {
        _config = config;
        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();
    }
    public async Task ConnectAsync()
    {
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_config.Server, _config.Port)
            .Build();
        await _client.ConnectAsync(options);
    }
    public async Task PublishAsync(string topic, TrajectoryRecord record)
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

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(JsonSerializer.Serialize(payload))
            .Build();
        await _client.PublishAsync(message);
    }
    public async ValueTask DisposeAsync()
    {
        await _client.DisconnectAsync();
    }
}