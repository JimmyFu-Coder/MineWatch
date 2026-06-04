using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MineWatch.Api.Configuration;
using MineWatch.Api.Hubs;
using MineWatch.Contracts;

namespace MineWatch.Api.Services;

public class NotificationWorker(
    IAmazonSQS sqsClient,
    NotificationConfig notificationConfig,
    IHubContext<TelemetryHub> hubContext,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationWorker started, listening on {QueueUrl}", notificationConfig.QueueUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (string.IsNullOrEmpty(notificationConfig.QueueUrl))
                {
                    await Task.Delay(TimeSpan.FromSeconds(notificationConfig.PollIntervalSeconds), stoppingToken);
                    continue;
                }

                var request = new ReceiveMessageRequest
                {
                    QueueUrl = notificationConfig.QueueUrl,
                    MaxNumberOfMessages = 10,
                    WaitTimeSeconds = 20
                };

                var response = await sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (var message in response.Messages)
                {
                    await ProcessMessageAsync(message, stoppingToken);
                    await sqsClient.DeleteMessageAsync(notificationConfig.QueueUrl, message.ReceiptHandle, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing notification messages");
                await Task.Delay(TimeSpan.FromSeconds(notificationConfig.PollIntervalSeconds), stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken ct)
    {
        var notification = JsonSerializer.Deserialize<NotificationMessage>(message.Body);
        if (notification == null)
        {
            logger.LogWarning("Failed to deserialize notification message {MessageId}", message.MessageId);
            return;
        }

        switch (notification.Type)
        {
            case "telemetry":
                await hubContext.Clients.All.SendAsync("TelemetryUpdate", notification.Payload, ct);
                break;
            case "alert":
                await hubContext.Clients.All.SendAsync("AlertReceived", notification.Payload, ct);
                break;
            default:
                logger.LogWarning("Unknown notification type: {Type}", notification.Type);
                break;
        }
    }
}
