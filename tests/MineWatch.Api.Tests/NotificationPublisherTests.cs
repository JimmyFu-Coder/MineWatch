using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Services.Notifications;
using Moq;

namespace MineWatch.Api.Tests;

public class NotificationPublisherTests
{
    private static TelemetryReading CreateReading() => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        Speed = 30,
        Lat = -32,
        Lon = 116,
        Timestamp = DateTime.UtcNow,
        VehicleNo = "TRUCK-001",
        Heading = 90,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task PublishTelemetryAsync_EmptyQueueUrl_DoesNotSend()
    {
        var sqs = new Mock<IAmazonSQS>();
        var config = new NotificationPublisherConfig { QueueUrl = "" };
        var publisher = new NotificationPublisher(sqs.Object, config, Mock.Of<ILogger<NotificationPublisher>>());

        await publisher.PublishTelemetryAsync(CreateReading());

        sqs.Verify(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishTelemetryAsync_SendsCorrectNotification()
    {
        var sqs = new Mock<IAmazonSQS>();
        var config = new NotificationPublisherConfig { QueueUrl = "https://sqs/test-queue" };
        var publisher = new NotificationPublisher(sqs.Object, config, Mock.Of<ILogger<NotificationPublisher>>());

        var reading = CreateReading();
        await publisher.PublishTelemetryAsync(reading);

        sqs.Verify(s => s.SendMessageAsync(
            It.Is<SendMessageRequest>(r =>
                r.QueueUrl == "https://sqs/test-queue" &&
                DeserializeNotification(r.MessageBody).Type == "telemetry"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAlertAsync_SendsCorrectNotification()
    {
        var sqs = new Mock<IAmazonSQS>();
        var config = new NotificationPublisherConfig { QueueUrl = "https://sqs/test-queue" };
        var publisher = new NotificationPublisher(sqs.Object, config, Mock.Of<ILogger<NotificationPublisher>>());

        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            RuleId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            TelemetryReadingId = Guid.NewGuid(),
            Status = AlertStatus.Active,
            Message = "Speed exceeded"
        };
        await publisher.PublishAlertAsync(alert);

        sqs.Verify(s => s.SendMessageAsync(
            It.Is<SendMessageRequest>(r =>
                DeserializeNotification(r.MessageBody).Type == "alert"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_SqsThrows_DoesNotPropagate()
    {
        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSQSException("network error"));
        var config = new NotificationPublisherConfig { QueueUrl = "https://sqs/test-queue" };
        var publisher = new NotificationPublisher(sqs.Object, config, Mock.Of<ILogger<NotificationPublisher>>());

        // should not throw
        await publisher.PublishTelemetryAsync(CreateReading());
    }

    private static Contracts.NotificationMessage DeserializeNotification(string body) =>
        JsonSerializer.Deserialize<Contracts.NotificationMessage>(body)!;
}
