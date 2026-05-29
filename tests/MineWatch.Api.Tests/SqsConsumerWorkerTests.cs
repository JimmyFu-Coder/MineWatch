using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Logging;
using MineWatch.Worker.Configuration;
using MineWatch.Worker.Services;
using MineWatch.Infrastructure.Entities;
using Moq;
using System.Text.Json;
using System.Threading.Channels;

namespace MineWatch.Api.Tests;

public class SqsConsumerWorkerTests
{
    private readonly Mock<IAmazonSQS> _sqsMock;
    private readonly SqsConfig _sqsConfig;
    private readonly Mock<ILogger<SqsConsumerWorker>> _loggerMock;

    public SqsConsumerWorkerTests()
    {
        _sqsMock = new Mock<IAmazonSQS>();
        _sqsConfig = new SqsConfig
        {
            QueueUrl = "http://test-queue",
            DlqUrl = "http://test-dlq"
        };
        _loggerMock = new Mock<ILogger<SqsConsumerWorker>>();
    }

    private static TelemetryReading CreateReading(string vehicleNo = "TRUCK-001")
    {
        return new TelemetryReading
        {
            Id = Guid.NewGuid(),
            VehicleNo = vehicleNo,
            Timestamp = DateTime.UtcNow,
            Lat = -32.265,
            Lon = 116.023,
            Speed = 30.0,
            Heading = 90.0,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task ExecuteAsync_ValidMessages_WritesToChannelAndDeletesFromSqs()
    {
        // Arrange
        var reading = CreateReading();
        var message = new Message
        {
            MessageId = "msg-1",
            ReceiptHandle = "handle-1",
            Body = JsonSerializer.Serialize(reading)
        };

        var response = new ReceiveMessageResponse
        {
            Messages = new List<Message> { message }
        };

        var callCount = 0;
        _sqsMock
            .Setup(s => s.ReceiveMessageAsync(
                It.IsAny<ReceiveMessageRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1) return response;
                throw new OperationCanceledException();
            });

        var channel = Channel.CreateBounded<TelemetryReading>(100);

        // Act
        var worker = new TestableSqsConsumerWorker(
            _sqsMock.Object, _sqsConfig, channel, _loggerMock.Object);

        await worker.RunExecuteAsync(CancellationToken.None);

        // Assert — verify reading was written to channel
        Assert.True(channel.Reader.TryRead(out var writtenReading));
        Assert.Equal(reading.VehicleNo, writtenReading.VehicleNo);

        // Assert — verify SQS deletion
        _sqsMock.Verify(s => s.DeleteMessageAsync(
            It.Is<DeleteMessageRequest>(r =>
                r.QueueUrl == _sqsConfig.QueueUrl &&
                r.ReceiptHandle == "handle-1"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

internal class TestableSqsConsumerWorker(
    IAmazonSQS sqsClient,
    SqsConfig sqsConfig,
    Channel<TelemetryReading> channel,
    ILogger<SqsConsumerWorker> logger) : SqsConsumerWorker(sqsClient, sqsConfig, channel, logger)
{
    public Task RunExecuteAsync(CancellationToken ct) => ExecuteAsync(ct);
}
