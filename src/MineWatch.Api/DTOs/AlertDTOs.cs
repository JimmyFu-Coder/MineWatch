namespace MineWatch.Api.DTOs;

public record CreateRuleRequest(
    string Name,
    string RuleType,
    string Severity,
    double? SpeedThreshold,
    double? IdleSpeedThreshold,
    double? IdleDurationSeconds,
    string? DeviceType,
    string? GeoFenceSpec,
    int CoolDownSeconds);

public record UpdateRuleRequest(
    string? Name,
    string? Severity,
    double? SpeedThreshold,
    double? IdleSpeedThreshold,
    double? IdleDurationSeconds,
    string? DeviceType,
    string? GeoFenceSpec,
    int? CoolDownSeconds,
    bool? IsEnabled);

public record RuleResponse(
    Guid Id,
    string Name,
    string RuleType,
    string Severity,
    double? SpeedThreshold,
    double? IdleSpeedThreshold,
    double? IdleDurationSeconds,
    string? DeviceType,
    string? GeoFenceSpec,
    int CoolDownSeconds,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AlertResponse(
    Guid Id,
    Guid RuleId,
    string RuleName,
    Guid DeviceId,
    string DeviceName,
    Guid TelemetryReadingId,
    string Status,
    string Message,
    double? TriggerLat,
    double? TriggerLon,
    double? TriggerSpeed,
    DateTime TriggeredAt,
    string? AcknowledgedBy,
    DateTime? AcknowledgedAt,
    DateTime? ResolvedAt);

public record AcknowledgeRequest(string AcknowledgedBy);
