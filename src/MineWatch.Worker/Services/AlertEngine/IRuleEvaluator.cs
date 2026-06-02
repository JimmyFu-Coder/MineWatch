namespace MineWatch.Worker.Services.AlertEngine;
using MineWatch.Infrastructure.Entities;

public interface IRuleEvaluator
{
    AlertRuleType RuleType { get; }
    Task<Alert?> EvaluateAsync(AlertRule rule, TelemetryReading reading);
}
