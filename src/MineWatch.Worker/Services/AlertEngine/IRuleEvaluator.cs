using MineWatch.Infrastructure.Entities;

namespace MineWatch.Worker.Services.AlertEngine;

public interface IRuleEvaluator
{
    AlertRuleType RuleType { get; }
    Task<Alert?> EvaluateAsync(AlertRule rule, TelemetryReading reading);
}
