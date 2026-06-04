namespace MineWatch.Api.Configuration;

public class NotificationConfig
{
    public string QueueUrl { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 5;
}
