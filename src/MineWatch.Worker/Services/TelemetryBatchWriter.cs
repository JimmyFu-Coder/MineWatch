  using System.Threading.Channels;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Hosting;
  using Microsoft.Extensions.Logging;
  using MineWatch.Infrastructure.Data;
  using MineWatch.Infrastructure.Entities;                                                   
                                                                                             
  namespace MineWatch.Worker.Services;                                                          
                                                                                             
  public class TelemetryBatchWriter(                                                         
      Channel<TelemetryReading> channel,
      IDbContextFactory<MineWatchDbContext> dbContextFactory,                                
      ILogger<TelemetryBatchWriter> logger,                                                  
      int batchSize = 100,                                                                   
      TimeSpan? batchTimeout = null) : BackgroundService                                     
  {                                                                                          
      private readonly TimeSpan _batchTimeout = batchTimeout ?? TimeSpan.FromSeconds(1);     
                                                                                             
      protected override async Task ExecuteAsync(CancellationToken stoppingToken)            
      {                                                                                      
          logger.LogInformation("TelemetryBatchWriter started");

          while (!stoppingToken.IsCancellationRequested)
          {
              try
              {
                  var batch = new List<TelemetryReading>();
                  var reading = await channel.Reader.ReadAsync(stoppingToken);
                  batch.Add(reading);

                  using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                  cts.CancelAfter(_batchTimeout);

                  while (batch.Count < batchSize)
                  {
                      try
                      {
                          var next = await channel.Reader.ReadAsync(cts.Token);
                          batch.Add(next);
                      }
                      catch (OperationCanceledException)                                                 
                      {                                                                                  
                          break;                                                                         
                      }                                                                                  
                      catch (ChannelClosedException)                                                     
                      {                                                                                  
                          break;                                                                         
                      } 
                  }

                  await WriteBatchWithRetryAsync(batch);
              }
              catch (OperationCanceledException)
              {
                  break;
              }
              catch (ChannelClosedException)
              {                                                                                          
                  break;      
              }  
          }
      }

      private async Task WriteBatchWithRetryAsync(List<TelemetryReading> batch)
      {
          var maxRetries = 3;
          for (var i = 0; i < maxRetries; i++)
          {
              try
              {
                await WriteBatchAsync(batch);
                return;
              }
              catch (Exception e) when(i < maxRetries - 1)
              {
                  logger.LogError(e, "Writing telemetry readings, retrying ({Attempt}/{Max})", i+1, maxRetries);
              }
          }
      }
      private async Task WriteBatchAsync(List<TelemetryReading> batch)                       
      {           
          await using var dbContext = await dbContextFactory.CreateDbContextAsync();         
          dbContext.TelemetryReadings.AddRange(batch);                                       
          await dbContext.SaveChangesAsync();                                                
          logger.LogInformation("Wrote {Count} telemetry readings to database", batch.Count);
      }                                                                                      
  }                                                                                          
      