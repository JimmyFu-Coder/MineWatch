using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MineWatch.Infrastructure.Entities;
using MQTTnet;
using MQTTnet.Protocol;

namespace MineWatch.Api.Services;

public class MqttSubscriberService( Channel<TelemetryReading> channel, ILogger<MqttSubscriberService> logger, IConfiguration config): BackgroundService
{
    private IMqttClient? _client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(
                config["Mqtt:Server"] ?? "localhost",
                int.Parse(config["Mqtt:Port"] ?? "1883"))
            .Build();
        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(options, stoppingToken);
                    var subscribeOptions = new MqttClientFactory().CreateSubscribeOptionsBuilder()
                        .WithTopicFilter("devices/+/telemetry")
                        .Build();
                    await _client.SubscribeAsync(subscribeOptions, stoppingToken);

                }
            }
            catch(Exception ex)
            {
                logger.LogError(ex, "MQTT client failed, retrying in 5s");
                await Task.Delay(5000, stoppingToken);
                continue;
            }
            
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task HandleMessageAsync(MqttApplicationMessageReceivedEventArgs eventArgs)
    {
        try
        {
            var reading = TelemetryParser.Parse(Encoding.UTF8.GetString(eventArgs.ApplicationMessage.Payload.ToArray()));
            await channel.Writer.WriteAsync(reading);
            logger.LogInformation("Received telemetry from {VehicleNo}", reading.VehicleNo);
        } catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process MQTT message");
        }
    }
}