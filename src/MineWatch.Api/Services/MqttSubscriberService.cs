using System.Buffers;
using System.Text;
using System.Text.Json;
using Amazon.SQS;
using MineWatch.Api.Configuration;
using MQTTnet;


namespace MineWatch.Api.Services;

public class MqttSubscriberService( IAmazonSQS sqsClient, IConfiguration config , ILogger<MqttSubscriberService> logger,  SqsConfig sqsConfig): BackgroundService
{
    private IMqttClient? _client;
    private CancellationToken _stoppingToken;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        _client = factory.CreateMqttClient();
        _stoppingToken = stoppingToken;  
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(
                config["Mqtt:Server"] ?? "localhost",
                int.Parse(config["Mqtt:Port"] ?? "1883"))
            .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V311) 
            .Build();
        _client.ApplicationMessageReceivedAsync += HandleMessageAsync;

        logger.LogInformation("MqttSubscriberService starting, attempting connect...");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    logger.LogInformation("Connecting to MQTT broker at {Server}:{Port}...",
                        config["Mqtt:Server"] ?? "localhost",
                        config["Mqtt:Port"] ?? "1883");
                    await _client.ConnectAsync(options, stoppingToken);
                    logger.LogInformation("MQTT connected, subscribing...");
                    var subscribeOptions = new MqttClientFactory().CreateSubscribeOptionsBuilder()
                        .WithTopicFilter("devices/+/telemetry")
                        .Build();
                    await _client.SubscribeAsync(subscribeOptions, stoppingToken);
                    logger.LogInformation("MQTT subscribed to devices/+/telemetry");
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
            var json = JsonSerializer.Serialize(reading);                                      
            await sqsClient.SendMessageAsync(sqsConfig.QueueUrl, json, _stoppingToken);
            logger.LogInformation("Received telemetry from {VehicleNo}", reading.VehicleNo);
        } catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process MQTT message");
        }
    }
}