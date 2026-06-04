
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;
using MineWatch.Worker.Services.Notifications;

namespace MineWatch.Worker.Services.AlertEngine;

// TODO: Rule cache subscription — replace 30s polling with PostgreSQL NOTIFY/LISTEN or MQTT
public class AlertEngine(
    IDbContextFactory<MineWatchDbContext> dbContextFactory,
    IEnumerable<IRuleEvaluator> evaluators,
    INotificationPublisher notificationPublisher,
    ILogger<AlertEngine> logger) : IAlertEngine
{
    private List<AlertRule> _cachedRules = [];
    private DateTime _rulesLoadedAt = DateTime.MinValue;
    private readonly TimeSpan _rulesCacheTtl = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, DateTime> _lastAlertByRuleDevice = new();

    // Device type cache: deviceId -> device type string
    private readonly ConcurrentDictionary<Guid, string?> _deviceTypeCache = new();
    private DateTime _devicesLoadedAt = DateTime.MinValue;
    private readonly TimeSpan _deviceCacheTtl = TimeSpan.FromMinutes(5);

    public async Task EvaluateAsync(TelemetryReading reading)
    {
        try
        {
            var rules = await GetRulesAsync();
            var alerts = new List<Alert>();

            foreach (var rule in rules)
            {
                if (!await MatchesDeviceTypeAsync(rule, reading.DeviceId))
                    continue;

                var key = $"{rule.Id}_{reading.DeviceId}";
                if (IsInCooldown(key, rule.CoolDownSeconds))
                    continue;

                var evaluator = evaluators.FirstOrDefault(e => e.RuleType == rule.RuleType);
                if (evaluator == null)
                    continue;

                var alert = await evaluator.EvaluateAsync(rule, reading);
                if (alert != null)
                {
                    _lastAlertByRuleDevice[key] = DateTime.UtcNow;
                    alerts.Add(alert);
                }
            }

            if (alerts.Count > 0)
                await PersistAlertsAsync(alerts);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Alert evaluation failed for reading {ReadingId}", reading.Id);
        }
    }

    private async Task<List<AlertRule>> GetRulesAsync()
    {
        if (DateTime.UtcNow - _rulesLoadedAt < _rulesCacheTtl && _cachedRules.Count > 0)
            return _cachedRules;

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        _cachedRules = await dbContext.AlertRules.Where(r => r.IsEnabled).ToListAsync();
        _rulesLoadedAt = DateTime.UtcNow;
        return _cachedRules;
    }

    private async Task<bool> MatchesDeviceTypeAsync(AlertRule rule, Guid deviceId)
    {
        if (rule.DeviceType == null)
            return true;

        var deviceType = await GetDeviceTypeAsync(deviceId);
        return deviceType == rule.DeviceType;
    }

    private async Task<string?> GetDeviceTypeAsync(Guid deviceId)
    {
        if (_deviceTypeCache.TryGetValue(deviceId, out var cachedType))
            return cachedType;

        // Refresh entire cache if TTL expired
        if (DateTime.UtcNow - _devicesLoadedAt > _deviceCacheTtl)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync();
            var devices = await dbContext.Devices.ToDictionaryAsync(d => d.Id, d => d.Type);
            foreach (var (id, devType) in devices)
                _deviceTypeCache[id] = devType;
            _devicesLoadedAt = DateTime.UtcNow;

            return _deviceTypeCache.TryGetValue(deviceId, out var cached) ? cached : null;
        }

        // Cache not expired but device not found — single lookup
        await using var ctx = await dbContextFactory.CreateDbContextAsync();
        var deviceType = await ctx.Devices
            .Where(d => d.Id == deviceId)
            .Select(d => d.Type)
            .FirstOrDefaultAsync();
        _deviceTypeCache[deviceId] = deviceType;
        return deviceType;
    }

    private bool IsInCooldown(string key, int cooldownSeconds)
    {
        if (cooldownSeconds <= 0)
            return false;
        if (!_lastAlertByRuleDevice.TryGetValue(key, out var lastAlert))
            return false;
        return DateTime.UtcNow - lastAlert < TimeSpan.FromSeconds(cooldownSeconds);
    }

    private async Task PersistAlertsAsync(List<Alert> alerts)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Alerts.AddRange(alerts);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Persisted {Count} alerts", alerts.Count);

        foreach (var alert in alerts)
        {
            await notificationPublisher.PublishAlertAsync(alert);
        }
    }
}
