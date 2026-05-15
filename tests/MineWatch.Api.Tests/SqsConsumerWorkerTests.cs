using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MineWatch.Api.Configuration;
using MineWatch.Api.Services;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;
using Moq;
using System.Text.Json;

namespace MineWatch.Api.Tests;

public class SqsConsumerWorkerTests
{
    private readonly Mock<IAmazonSQS> _sqsMock;
    private readonly SqsConfig _sqsConfig;
    private readonly IDbContextFactory<MineWatchDbContext> _dbContextFactory;
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

        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;
        _dbContextFactory = new TestDbContextFactory(options);
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
    public async Task ExecuteAsync_ValidMessages_WritesToDbAndDeletesFromSqs()
    {
        // Arrange — 准备测试数据
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

        // Setup: 第一次返回消息，第二次抛 OperationCanceledException 终止循环
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

        // Act — 执行
        var worker = new TestableSqsConsumerWorker(
            _sqsMock.Object, _sqsConfig, _dbContextFactory, _loggerMock.Object);

        await worker.RunExecuteAsync(CancellationToken.None);

        // Assert — 验证数据库
        await using var db = _dbContextFactory.CreateDbContext();
        var saved = await db.TelemetryReadings.ToListAsync();
        Assert.Single(saved);
        Assert.Equal(reading.VehicleNo, saved[0].VehicleNo);

        // Assert — 验证 SQS 删除
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
    IDbContextFactory<MineWatchDbContext> dbContextFactory,
    ILogger<SqsConsumerWorker> logger) : SqsConsumerWorker(sqsClient, sqsConfig, dbContextFactory, logger)
{
    public Task RunExecuteAsync(CancellationToken ct) => ExecuteAsync(ct);
}

internal class TestDbContextFactory(DbContextOptions<MineWatchDbContext> options) :
    IDbContextFactory<MineWatchDbContext>
{
    public MineWatchDbContext CreateDbContext() => new(options);
}
