using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MineWatch.Api.Services;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;
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

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenBatchSizeReached_WritesAllToDatabase()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        var readings = Enumerable.Range(0, 5).Select(_ => CreateReading()).ToList();
        foreach (var r in readings)
            await channel.Writer.WriteAsync(r);
        channel.Writer.Complete();

        var dbContextFactory = CreateMockDbContextFactory("BatchTest_Full");
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, logger.Object,
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
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, logger.Object,
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
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, logger.Object,
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
        // Arrange — 12 条消息，batchSize=5，应该分 3 批：5+5+2                                
        var channel = Channel.CreateBounded<TelemetryReading>(1000);
        var readings = Enumerable.Range(0, 12).Select(i =>
            CreateReading($"TRUCK-{i:D3}")).ToList();
        foreach (var r in readings)
            await channel.Writer.WriteAsync(r);
        channel.Writer.Complete();

        var dbContextFactory = CreateMockDbContextFactory("BatchTest_Multiple");
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, logger.Object,
            batchSize: 5, batchTimeout: TimeSpan.FromMilliseconds(300));

        // Act                                                                                 
        await writer.StartAsync(CancellationToken.None);
        await Task.Delay(2000);

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
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, logger.Object,
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

        // 第一次创建 DbContext 失败，第二次成功
        var dbContextFactory = CreateFailingMockDbContextFactory("BatchTest_RetrySuccess", failCount: 1);
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, logger.Object,
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
        // 不 Complete，让 writer 在重试期间 channel 仍然活着

        // 所有 3 次重试都失败
        var dbContextFactory = CreateFailingMockDbContextFactory("BatchTest_RetryFail", failCount: 10);
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, logger.Object,
            batchSize: 100, batchTimeout: TimeSpan.FromMilliseconds(200));

        await writer.StartAsync(CancellationToken.None);

        // 等待 writer 因重试耗尽而崩溃
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.ExecuteTask!);
        Assert.Equal("Simulated DB failure", ex.Message);
        dbContextFactory.Verify(                                                           
            f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()),                    
            Times.Exactly(2));
    }

    [Fact(Timeout = 5000)]
    public async Task ExecuteAsync_WhenChannelClosedAfterOneItem_WritesSingleItem()
    {
        var channel = Channel.CreateBounded<TelemetryReading>(1000);    
        await channel.Writer.WriteAsync(CreateReading("TRUCK-001"));                   
        channel.Writer.Complete();  
        var dbContextFactory = CreateMockDbContextFactory("BatchTest_OneItem_Cancelled");
        var logger = new Mock<ILogger<TelemetryBatchWriter>>();
        var writer = new TelemetryBatchWriter(
            channel, dbContextFactory.Object, logger.Object,
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
            Times.Exactly(3));
    }
}