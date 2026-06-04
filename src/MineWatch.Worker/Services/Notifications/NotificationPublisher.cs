using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using MineWatch.Contracts;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Worker.Services.Notifications;

public interface INotificationPublisher
{
    Task PublishTelemetryAsync(TelemetryReading reading);
    Task PublishAlertAsync(Alert alert);
}

public class NotificationPublisher(
    IAmazonSQS sqsClient,
    NotificationPublisherConfig config,
    ILogger<NotificationPublisher> logger) : INotificationPublisher
{
    public async Task PublishTelemetryAsync(TelemetryReading reading)
    {
        await PublishAsync("telemetry", reading);
    }

    public async Task PublishAlertAsync(Alert alert)
    {
        await PublishAsync("alert", alert);
    }

    private async Task PublishAsync(string type, object payload)
    {
        if (string.IsNullOrEmpty(config.QueueUrl))
            return;

        try
        {
            var notification = new NotificationMessage
            {
                Type = type,
                Payload = JsonSerializer.Serialize(payload)
            };

            await sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = config.QueueUrl,
                MessageBody = JsonSerializer.Serialize(notification)
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish {Type} notification", type);
        }
    }
}

public class NotificationPublisherConfig
{
    public string QueueUrl { get; set; } = string.Empty;
}
