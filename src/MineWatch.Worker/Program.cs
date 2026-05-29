using System.Threading.Channels;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Configuration;
using MineWatch.Worker.Services;

var builder = Host.CreateDefaultBuilder(args);                                     
                                                                                     
  builder.ConfigureServices((context, services) =>                                   
  {                                                         
      services.AddDbContextFactory<MineWatchDbContext>(options =>                    
                                                                                     
  options.UseNpgsql(context.Configuration.GetConnectionString("DefaultConnection")));
                                                                                     
      services.AddDefaultAWSOptions(context.Configuration.GetAWSOptions());          
      services.AddAWSService<IAmazonSQS>();                 
      services.AddSingleton<SqsConfig>();                                            
      services.AddSingleton(Channel.CreateBounded<TelemetryReading>(new              
  BoundedChannelOptions(1000)                                                        
      {                                                                              
          FullMode = BoundedChannelFullMode.DropOldest                               
      }));                                                                           
   
      services.AddHostedService<SqsBootstrapService>();                              
      services.AddHostedService<MqttSubscriberService>();   
      services.AddHostedService<SqsConsumerWorker>();                                
      services.AddHostedService<TelemetryBatchWriter>();                             
  });                                                                                
                                                                                     
  var host = builder.Build();                                                        
  await host.RunAsync();
