using MineWatch.Api.Configuration;
using Amazon.SQS;  

namespace MineWatch.Api.Services;

public class SqsBootstrapService(
    IAmazonSQS sqsClient,
    IConfiguration config,
    SqsConfig sqsConfig,
    ILogger<SqsBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var dlqName = config["Sqs:DLQName"]!;                                              
        var queueName = config["Sqs:QueueName"]!;                                          
        var maxReceiveCount = int.Parse(config["Sqs:MaxReceiveCount"]!);  
        
        var dlqResponse = await sqsClient.CreateQueueAsync(dlqName, cancellationToken);    
        sqsConfig.DlqUrl = dlqResponse.QueueUrl;                                           
        logger.LogInformation("Created DLQ: {DlqUrl}", sqsConfig.DlqUrl);   
        
        var dlqAttributes = await sqsClient.GetQueueAttributesAsync(                       
            sqsConfig.DlqUrl,                                                              
            new List<string> { "QueueArn" },                                               
            cancellationToken);                                                            
        var dlqArn = dlqAttributes.QueueARN;  
        
        var queueResponse = await sqsClient.CreateQueueAsync(queueName, cancellationToken);
        sqsConfig.QueueUrl = queueResponse.QueueUrl;                                       
        logger.LogInformation("Created main queue: {QueueUrl}", sqsConfig.QueueUrl);       
        
        var redrivePolicy =                                                                
            $"{{\"maxReceiveCount\":{maxReceiveCount},\"deadLetterTargetArn\":\"{dlqArn}\"}}"; 
        await sqsClient.SetQueueAttributesAsync(                  
            sqsConfig.QueueUrl,                                                            
            new Dictionary<string, string>                                                 
            {                                                                              
                { "RedrivePolicy", redrivePolicy }                                         
            },                                                                             
            cancellationToken);                                                            
        logger.LogInformation("Set redrive policy: maxReceiveCount={MaxReceiveCount}",     
            maxReceiveCount); 
    }
    
    public Task StopAsync(CancellationToken cancellationToken) =>  Task.CompletedTask;
}