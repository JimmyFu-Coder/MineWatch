using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MineWatch.Api.Configuration;
using MineWatch.Api.Hubs;
using MineWatch.Api.Services;
using MineWatch.Contracts;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Services.Notifications;
using Moq;

namespace MineWatch.IntegrationTests;

public class NotificationPipelineIntegrationTests
{
    [Fact]
    public async Task Publisher_SendsMessage_ThenWorker_PushesToSignalR()
    {
        // 1. Setup: mock SQS, capture sent message
        var queueUrl = "https://sqs/test-queue";
        var capturedMessages = new List<string>();

        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((req, _) => capturedMessages.Add(req.MessageBody))
            .ReturnsAsync(new SendMessageResponse());

        var config = new NotificationPublisherConfig { QueueUrl = queueUrl };
        var publisher = new NotificationPublisher(sqs.Object, config, Mock.Of<ILogger<NotificationPublisher>>());

        // 2. Publisher sends telemetry notification
        var reading = new TelemetryReading
        {
            Id = Guid.NewGuid(), DeviceId = Guid.NewGuid(), Speed = 45,
            Lat = -32, Lon = 116, Timestamp = DateTime.UtcNow,
            VehicleNo = "TRUCK-001", Heading = 90, CreatedAt = DateTime.UtcNow
        };
        await publisher.PublishTelemetryAsync(reading);

        // 3. Verify SQS received the message
        Assert.Single(capturedMessages);
        var notification = JsonSerializer.Deserialize<NotificationMessage>(capturedMessages[0]);
        Assert.NotNull(notification);
        Assert.Equal("telemetry", notification!.Type);

        // 4. Setup Worker to consume the captured message
        var hubClients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubClients.Setup(h => h.All).Returns(clientProxy.Object);

        var hubContext = new Mock<IHubContext<TelemetryHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var workerSqs = new Mock<IAmazonSQS>();
        workerSqs.SetupSequence(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages = new List<Message>
                {
                    new() { MessageId = "1", ReceiptHandle = "h1", Body = capturedMessages[0] }
                }
            })
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [] });
        workerSqs.Setup(s => s.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        var workerConfig = new NotificationConfig { QueueUrl = queueUrl, PollIntervalSeconds = 1 };
        var worker = new NotificationWorker(workerSqs.Object, workerConfig, hubContext.Object, Mock.Of<ILogger<NotificationWorker>>());

        // 5. Run worker briefly
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        try { await worker.StartAsync(cts.Token); } catch (OperationCanceledException) { }

        // 6. Verify SignalR received the push
        clientProxy.Verify(c => c.SendCoreAsync("TelemetryUpdate",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlertNotification_FlowsThroughPipeline()
    {
        var queueUrl = "https://sqs/test-queue";
        var capturedMessages = new List<string>();

        var sqs = new Mock<IAmazonSQS>();
        sqs.Setup(s => s.SendMessageAsync(It.IsAny<SendMessageRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendMessageRequest, CancellationToken>((req, _) => capturedMessages.Add(req.MessageBody))
            .ReturnsAsync(new SendMessageResponse());

        var publisher = new NotificationPublisher(sqs.Object,
            new NotificationPublisherConfig { QueueUrl = queueUrl }, Mock.Of<ILogger<NotificationPublisher>>());

        var alert = new Alert
        {
            Id = Guid.NewGuid(), RuleId = Guid.NewGuid(), DeviceId = Guid.NewGuid(),
            TelemetryReadingId = Guid.NewGuid(), Status = AlertStatus.Active,
            Message = "Speed exceeded", TriggerSpeed = 60
        };
        await publisher.PublishAlertAsync(alert);

        Assert.Single(capturedMessages);
        var notification = JsonSerializer.Deserialize<NotificationMessage>(capturedMessages[0]);
        Assert.Equal("alert", notification!.Type);

        // Verify Worker pushes via AlertReceived
        var hubClients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hubClients.Setup(h => h.All).Returns(clientProxy.Object);
        var hubContext = new Mock<IHubContext<TelemetryHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var workerSqs = new Mock<IAmazonSQS>();
        workerSqs.SetupSequence(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse
            {
                Messages = [new Message { MessageId = "1", ReceiptHandle = "h1", Body = capturedMessages[0] }]
            })
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [] });
        workerSqs.Setup(s => s.DeleteMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteMessageResponse());

        var worker = new NotificationWorker(workerSqs.Object,
            new NotificationConfig { QueueUrl = queueUrl, PollIntervalSeconds = 1 },
            hubContext.Object, Mock.Of<ILogger<NotificationWorker>>());

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        try { await worker.StartAsync(cts.Token); } catch (OperationCanceledException) { }

        clientProxy.Verify(c => c.SendCoreAsync("AlertReceived",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
