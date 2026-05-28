using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using MineWatch.Api.Configuration;
using MineWatch.Infrastructure.Entities;
using System.Text.Json;
using System.Threading.Channels;

namespace MineWatch.Api.Services;

public class SqsConsumerWorker(
    IAmazonSQS sqsClient,
    SqsConfig sqsConfig,
    Channel<TelemetryReading> channel,
    ILogger<SqsConsumerWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = new ReceiveMessageRequest
                {
                    QueueUrl = sqsConfig.QueueUrl
                };

                var response = await sqsClient.ReceiveMessageAsync(request, stoppingToken);

                foreach (var message in response.Messages)
                {
                    var reading = JsonSerializer.Deserialize<TelemetryReading>(message.Body);
                    if (reading is null)
                    {
                        logger.LogWarning("Failed to deserialize message {MessageId}", message.MessageId);
                        continue;
                    }

                    await channel.Writer.WriteAsync(reading, stoppingToken);

                    await sqsClient.DeleteMessageAsync(new DeleteMessageRequest
                    {
                        QueueUrl = sqsConfig.QueueUrl,
                        ReceiptHandle = message.ReceiptHandle
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing SQS messages");
            }
        }
    }
}
