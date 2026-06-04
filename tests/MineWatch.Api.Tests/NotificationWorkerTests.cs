using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MineWatch.Api.Configuration;
using MineWatch.Api.Hubs;
using MineWatch.Api.Services;
using MineWatch.Contracts;
using Moq;

namespace MineWatch.Api.Tests;

public class NotificationWorkerTests
{
    private static (Mock<IAmazonSQS> sqs, Mock<IHubContext<TelemetryHub>> hub, NotificationConfig config, NotificationWorker worker) CreateWorker()
    {
        var sqs = new Mock<IAmazonSQS>();
        var hub = new Mock<IHubContext<TelemetryHub>>();
        var config = new NotificationConfig { QueueUrl = "https://sqs/notifications", PollIntervalSeconds = 1 };
        var logger = Mock.Of<ILogger<NotificationWorker>>();
        var worker = new NotificationWorker(sqs.Object, config, hub.Object, logger);
        return (sqs, hub, config, worker);
    }

    private static Message CreateMessage(NotificationMessage notification) => new()
    {
        MessageId = Guid.NewGuid().ToString(),
        ReceiptHandle = "handle",
        Body = JsonSerializer.Serialize(notification)
    };

    [Fact]
    public async Task ExecuteAsync_TelemetryMessage_SendsTelemetryUpdate()
    {
        var (sqs, hub, _, worker) = CreateWorker();
        var clientProxy = new Mock<IClientProxy>();
        hub.Setup(h => h.Clients.All).Returns(clientProxy.Object);

        var notification = new NotificationMessage { Type = "telemetry", Payload = """{"speed":60}""" };
        sqs.SetupSequence(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [CreateMessage(notification)] })
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [] });

        // Use cancellation to stop the loop after one iteration
        var cts = new CancellationTokenSource();
        // After first receive returns empty, the loop will continue — cancel after short delay
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        { await worker.StartAsync(cts.Token); }
        catch (OperationCanceledException) { }

        clientProxy.Verify(c => c.SendCoreAsync("TelemetryUpdate",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AlertMessage_SendsAlertReceived()
    {
        var (sqs, hub, _, worker) = CreateWorker();
        var clientProxy = new Mock<IClientProxy>();
        hub.Setup(h => h.Clients.All).Returns(clientProxy.Object);

        var notification = new NotificationMessage { Type = "alert", Payload = """{"message":"Speed exceeded"}""" };
        sqs.SetupSequence(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [CreateMessage(notification)] })
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [] });

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        { await worker.StartAsync(cts.Token); }
        catch (OperationCanceledException) { }

        clientProxy.Verify(c => c.SendCoreAsync("AlertReceived",
            It.Is<object[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidJson_NoSignalRSend()
    {
        var (sqs, hub, _, worker) = CreateWorker();
        var clientProxy = new Mock<IClientProxy>();
        hub.Setup(h => h.Clients.All).Returns(clientProxy.Object);

        var badMessage = new Message
        {
            MessageId = "bad",
            ReceiptHandle = "handle",
            Body = "not valid json"
        };
        sqs.SetupSequence(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [badMessage] })
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [] });

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        { await worker.StartAsync(cts.Token); }
        catch (OperationCanceledException) { }

        // Bad JSON causes exception, no SignalR push
        clientProxy.Verify(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NullNotification_SkipsAndDeletes()
    {
        var (sqs, hub, _, worker) = CreateWorker();
        var clientProxy = new Mock<IClientProxy>();
        hub.Setup(h => h.Clients.All).Returns(clientProxy.Object);

        // NotificationMessage with null type triggers the default case (warning log)
        var notification = new NotificationMessage { Type = "unknown_type", Payload = "test" };
        sqs.SetupSequence(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [CreateMessage(notification)] })
            .ReturnsAsync(new ReceiveMessageResponse { Messages = [] });

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        { await worker.StartAsync(cts.Token); }
        catch (OperationCanceledException) { }

        // Unknown type: no SignalR push, but message gets deleted
        clientProxy.Verify(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Never);
        sqs.Verify(s => s.DeleteMessageAsync("https://sqs/notifications", "handle", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyQueueUrl_DoesNotPoll()
    {
        var (sqs, hub, config, worker) = CreateWorker();
        config.QueueUrl = "";

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(3));
        try
        { await worker.StartAsync(cts.Token); }
        catch (OperationCanceledException) { }

        sqs.Verify(s => s.ReceiveMessageAsync(It.IsAny<ReceiveMessageRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
