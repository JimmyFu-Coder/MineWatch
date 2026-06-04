using Microsoft.EntityFrameworkCore;
using MineWatch.Api.DTOs;
using MineWatch.Infrastructure.Data;
using MineWatch.Infrastructure.Entities;

namespace MineWatch.Api.Services;

public interface IAlertService
{
    // Rule CRUD
    Task<RuleResponse> CreateRuleAsync(CreateRuleRequest request);
    Task<RuleResponse?> UpdateRuleAsync(Guid id, UpdateRuleRequest request);
    Task<RuleResponse?> GetRuleAsync(Guid id);
    Task<(List<RuleResponse> Items, int Total)> GetRulesAsync(int page, int pageSize);
    Task<bool> DeleteRuleAsync(Guid id);

    // Alert operations
    Task<(List<AlertResponse> Items, int Total)> GetAlertsAsync(int page, int pageSize, AlertStatus? status, Guid? deviceId, Guid? ruleId);
    Task<AlertResponse?> AcknowledgeAlertAsync(Guid id, AcknowledgeRequest request);
    Task<AlertResponse?> ResolveAlertAsync(Guid id);
}

public class AlertService(MineWatchDbContext context) : IAlertService
{
    public async Task<RuleResponse> CreateRuleAsync(CreateRuleRequest request)
    {
        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            RuleType = Enum.Parse<AlertRuleType>(request.RuleType),
            Severity = Enum.Parse<AlertSeverity>(request.Severity),
            SpeedThreshold = request.SpeedThreshold,
            IdleSpeedThreshold = request.IdleSpeedThreshold,
            IdleDurationSeconds = request.IdleDurationSeconds,
            DeviceType = request.DeviceType,
            GeoFenceSpec = request.GeoFenceSpec,
            CoolDownSeconds = request.CoolDownSeconds,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
        context.AlertRules.Add(rule);
        await context.SaveChangesAsync();
        return ToRuleResponse(rule);
    }

    public async Task<RuleResponse?> UpdateRuleAsync(Guid id, UpdateRuleRequest request)
    {
        var rule = await context.AlertRules.FindAsync(id);
        if (rule == null) return null;

        rule.Name = request.Name ?? rule.Name;
        rule.Severity = request.Severity != null ? Enum.Parse<AlertSeverity>(request.Severity) : rule.Severity;
        rule.SpeedThreshold = request.SpeedThreshold ?? rule.SpeedThreshold;
        rule.IdleSpeedThreshold = request.IdleSpeedThreshold ?? rule.IdleSpeedThreshold;
        rule.IdleDurationSeconds = request.IdleDurationSeconds ?? rule.IdleDurationSeconds;
        rule.DeviceType = request.DeviceType ?? rule.DeviceType;
        rule.GeoFenceSpec = request.GeoFenceSpec ?? rule.GeoFenceSpec;
        rule.CoolDownSeconds = request.CoolDownSeconds ?? rule.CoolDownSeconds;
        rule.IsEnabled = request.IsEnabled ?? rule.IsEnabled;
        rule.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return ToRuleResponse(rule);
    }

    public async Task<RuleResponse?> GetRuleAsync(Guid id)
    {
        var rule = await context.AlertRules.FindAsync(id);
        return rule == null ? null : ToRuleResponse(rule);
    }

    public async Task<(List<RuleResponse> Items, int Total)> GetRulesAsync(int page, int pageSize)
    {
        var query = context.AlertRules.AsQueryable();
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => ToRuleResponse(r))
            .ToListAsync();
        return (items, total);
    }

    public async Task<bool> DeleteRuleAsync(Guid id)
    {
        var rule = await context.AlertRules.FindAsync(id);
        if (rule == null) return false;
        context.AlertRules.Remove(rule);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<(List<AlertResponse> Items, int Total)> GetAlertsAsync(
        int page, int pageSize, AlertStatus? status, Guid? deviceId, Guid? ruleId)
    {
        var query = context.Alerts.AsQueryable();
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);
        if (deviceId.HasValue) query = query.Where(a => a.DeviceId == deviceId.Value);
        if (ruleId.HasValue) query = query.Where(a => a.RuleId == ruleId.Value);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.TriggeredAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(context.AlertRules, a => a.RuleId, r => r.Id, (a, r) => new { Alert = a, RuleName = r.Name })
            .Join(context.Devices, x => x.Alert.DeviceId, d => d.Id, (x, d) => new AlertResponse(
                x.Alert.Id, x.Alert.RuleId, x.RuleName, x.Alert.DeviceId, d.Name,
                x.Alert.TelemetryReadingId, x.Alert.Status.ToString(), x.Alert.Message,
                x.Alert.TriggerLat, x.Alert.TriggerLon, x.Alert.TriggerSpeed,
                x.Alert.TriggeredAt, x.Alert.AcknowledgedBy, x.Alert.AcknowledgedAt, x.Alert.ResolvedAt))
            .ToListAsync();
        return (items, total);
    }

    public async Task<AlertResponse?> AcknowledgeAlertAsync(Guid id, AcknowledgeRequest request)
    {
        var alert = await context.Alerts.Include(a => a.Rule).Include(a => a.Device).FirstOrDefaultAsync(a => a.Id == id);
        if (alert == null || alert.Status != AlertStatus.Active) return null;

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedBy = request.AcknowledgedBy;
        alert.AcknowledgedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return ToAlertResponse(alert);
    }

    public async Task<AlertResponse?> ResolveAlertAsync(Guid id)
    {
        var alert = await context.Alerts.Include(a => a.Rule).Include(a => a.Device).FirstOrDefaultAsync(a => a.Id == id);
        if (alert == null || alert.Status == AlertStatus.Resolved) return null;

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return ToAlertResponse(alert);
    }

    private static RuleResponse ToRuleResponse(AlertRule r) => new(
        r.Id, r.Name, r.RuleType.ToString(), r.Severity.ToString(),
        r.SpeedThreshold, r.IdleSpeedThreshold, r.IdleDurationSeconds,
        r.DeviceType, r.GeoFenceSpec, r.CoolDownSeconds, r.IsEnabled,
        r.CreatedAt, r.UpdatedAt);

    private static AlertResponse ToAlertResponse(Alert a) => new(
        a.Id, a.RuleId, a.Rule.Name, a.DeviceId, a.Device.Name,
        a.TelemetryReadingId, a.Status.ToString(), a.Message,
        a.TriggerLat, a.TriggerLon, a.TriggerSpeed,
        a.TriggeredAt, a.AcknowledgedBy, a.AcknowledgedAt, a.ResolvedAt);
}
