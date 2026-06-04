using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Services;
using MineWatch.Worker.Services.AlertEngine;
using Moq;

namespace MineWatch.Api.Tests;

public class TelemetryBatchWriterTests
{
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

    private static Mock<IDbContextFactory<MineWatchDbContext>>
        CreateMockDbContextFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MineWatchDbContext(options));
        return factory;
    }

    private static Mock<IAlertEngine> CreateMockAlertEngine()
    {
        var engine = new Mock<IAlertEngine>();
        engine.Setup(e => e.EvaluateAsync(It.IsAny<TelemetryReading>()))
            .Returns(Task.CompletedTask);
        return engine;
    }

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenBatchSizeReached_WritesAllToDatabase()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        var readings = Enumerable.Range(0, 5).Select(_ => CreateReading()).ToList();
        foreach (var r in readings)
            await channel.Writer.WriteAsync(r);
        channel.Writer.Complete();

        var dbContextFactory = CreateMockDbContextFactory("BatchTest_Full");
        var alertEngine = CreateMockAlertEngine();
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, alertEngine.Object, logger.Object,
            batchSize: 5, batchTimeout: TimeSpan.FromMilliseconds(500));

        await writer.StartAsync(CancellationToken.None);
        await Task.Delay(1000);

        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("BatchTest_Full").Options;
        using var dbContext = new MineWatchDbContext(options);
        var count = await dbContext.TelemetryReadings.CountAsync();
        Assert.Equal(5, count);
    }

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenTimeout_WritesPartialBatch()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        await channel.Writer.WriteAsync(CreateReading());
        await channel.Writer.WriteAsync(CreateReading());
        channel.Writer.Complete();

        var dbContextFactory = CreateMockDbContextFactory("BatchTest_Timeout");
        var alertEngine = CreateMockAlertEngine();
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, alertEngine.Object, logger.Object,
            batchSize: 100, batchTimeout: TimeSpan.FromMilliseconds(200));

        await writer.StartAsync(CancellationToken.None);
        await Task.Delay(1000);

        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("BatchTest_Timeout").Options;
        using var dbContext = new MineWatchDbContext(options);
        var count = await dbContext.TelemetryReadings.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenChannelEmpty_DoesNotWrite()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        channel.Writer.Complete();

        var dbContextFactory = CreateMockDbContextFactory("BatchTest_Empty");
        var alertEngine = CreateMockAlertEngine();
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, alertEngine.Object, logger.Object,
            batchSize: 100, batchTimeout: TimeSpan.FromMilliseconds(100));

        await writer.StartAsync(CancellationToken.None);
        await Task.Delay(300);

        dbContextFactory.Verify(
            f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenMultipleBatches_WritesAllBatches()
    {
        // Arrange — 12 messages, batchSize=5, expect 3 batches: 5+5+2                                
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        var readings = Enumerable.Range(0, 12).Select(i =>
            CreateReading($"TRUCK-{i:D3}")).ToList();
        foreach (var r in readings)
            await channel.Writer.WriteAsync(r);
        channel.Writer.Complete();

        var dbContextFactory = CreateMockDbContextFactory("BatchTest_Multiple");
        var alertEngine = CreateMockAlertEngine();
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, alertEngine.Object, logger.Object,
            batchSize: 5, batchTimeout: TimeSpan.FromMilliseconds(300));

        // Act                                                                                 
        await writer.StartAsync(CancellationToken.None);
        await Task.Delay(3000);

        // Assert                                                                              
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("BatchTest_Multiple").Options;
        using var dbContext = new MineWatchDbContext(options);
        var saved = await dbContext.TelemetryReadings.ToListAsync();
        Assert.Equal(12, saved.Count);

        foreach (var r in readings)
        {
            Assert.Contains(saved, s => s.VehicleNo == r.VehicleNo);
        }
    }

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenCancelled_WritesAlreadyReadBatch()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        var readings = Enumerable.Range(0, 3).Select(i =>
            CreateReading($"TRUCK-{i:D3}")).ToList();
        foreach (var r in readings)
            await channel.Writer.WriteAsync(r);
        var cts = new CancellationTokenSource();
        var dbContextFactory = CreateMockDbContextFactory("BatchTest_Cancelled");
        var alertEngine = CreateMockAlertEngine();
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, alertEngine.Object, logger.Object,
            batchSize: 100, batchTimeout: TimeSpan.FromMilliseconds(300));
        await writer.StartAsync(cts.Token);
        await Task.Delay(800);
        cts.Cancel();
        await Task.Delay(800);
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("BatchTest_Cancelled").Options;
        using var dbContext = new MineWatchDbContext(options);
        var saved = await dbContext.TelemetryReadings.ToListAsync();
        Assert.Equal(3, saved.Count);
    }

    private static Mock<IDbContextFactory<MineWatchDbContext>>
        CreateFailingMockDbContextFactory(string dbName, int failCount)
    {
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var factory = new Mock<IDbContextFactory<MineWatchDbContext>>();
        var callCount = 0;
        factory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount <= failCount)
                    throw new InvalidOperationException("Simulated DB failure");
                return new MineWatchDbContext(options);
            });
        return factory;
    }

    [Fact(Timeout = 10000)]
    public async Task ExecuteAsync_WhenDbFailsOnFirstAttempt_RetriesAndSucceeds()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        await channel.Writer.WriteAsync(CreateReading());
        channel.Writer.Complete();

        // First DbContext creation fails, second succeeds
        var dbContextFactory = CreateFailingMockDbContextFactory("BatchTest_RetrySuccess", failCount: 1);
        var alertEngine = CreateMockAlertEngine();
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, alertEngine.Object, logger.Object,
            batchSize: 100, batchTimeout: TimeSpan.FromMilliseconds(200));

        await writer.StartAsync(CancellationToken.None);
        await Task.Delay(2000);

        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("BatchTest_RetrySuccess").Options;
        using var dbContext = new MineWatchDbContext(options);
        var saved = await dbContext.TelemetryReadings.ToListAsync();
        Assert.Single(saved);
    }

    [Fact(Timeout = 10000)]
    public async Task ExecuteAsync_WhenDbAlwaysFails_ThrowsAfterRetries()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        await channel.Writer.WriteAsync(CreateReading());

        var dbContextFactory = CreateFailingMockDbContextFactory("BatchTest_RetryFail", failCount: 10);
        var alertEngine = CreateMockAlertEngine();
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, alertEngine.Object, logger.Object,
            batchSize: 100, batchTimeout: TimeSpan.FromMilliseconds(200));

        await writer.StartAsync(CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.ExecuteTask!);
        Assert.Equal("Simulated DB failure", ex.Message);
    }

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenChannelClosedAfterOneItem_WritesSingleItem()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        await channel.Writer.WriteAsync(CreateReading("TRUCK-001"));
        channel.Writer.Complete();
        var dbContextFactory = CreateMockDbContextFactory("BatchTest_OneItem_Cancelled");
        var alertEngine = CreateMockAlertEngine();
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, alertEngine.Object, logger.Object,
            batchSize: 100, batchTimeout: TimeSpan.FromMilliseconds(1000));
        await writer.StartAsync(CancellationToken.None);
        await Task.Delay(800);
        var options = new DbContextOptionsBuilder<MineWatchDbContext>()
            .UseInMemoryDatabase("BatchTest_OneItem_Cancelled").Options;
        using var dbContext = new MineWatchDbContext(options);
        var saved = await dbContext.TelemetryReadings.ToListAsync();
        Assert.Single(saved);
        dbContextFactory.Verify(
            f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()),
            Times.Once());
    }
}
