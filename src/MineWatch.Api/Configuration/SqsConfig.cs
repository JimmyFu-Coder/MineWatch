namespace MineWatch.Api.Configuration;

public class SqsConfig
{
    public string QueueUrl { get; set; } = string.Empty;  
    public string DlqUrl { get; set; } = string.Empty;

}