# Alert Engine Design

## Overview

AlertEngine is the core orchestration layer of the alert system. It matches telemetry readings against alert rules and produces alert instances.

**Design principle:** AlertEngine only orchestrates — it delegates rule evaluation to injected `IRuleEvaluator` implementations, making the system extensible without modifying the engine itself.

## Architecture

```
TelemetryBatchWriter
       │
       │ after DB write
       ▼
  IAlertEngine.EvaluateAsync(reading)
       │
       ├─ 1. GetRulesAsync() ── in-memory cache (30s TTL) ── DB
       │
       ├─ 2. Device type filter ── rule.DeviceType == null → all devices, else match type
       │
       ├─ 3. Cooldown check ── same rule + device within CoolDownSeconds → skip
       │
       ├─ 4. Dispatch to IRuleEvaluator
       │     ├─ SpeedRuleEvaluator
       │     ├─ GeoFenceRuleEvaluator
       │     └─ IdleRuleEvaluator
       │
       ├─ 5. Collect alerts, batch write to DB
       │
       └─ 6. Exception isolation ── evaluation failure never blocks telemetry ingestion
```

## Components

### AlertEngine (Orchestrator)

| Responsibility | Description |
|----------------|-------------|
| Rule loading & caching | Load `IsEnabled` rules from DB with 30s in-memory TTL |
| Device type filtering | `DeviceType == null` matches all; otherwise only matching device types |
| Cooldown check | Same rule + device won't re-trigger within `CoolDownSeconds` |
| Evaluator dispatch | Find the `IRuleEvaluator` matching the rule's `RuleType` |
| Alert persistence | Collect evaluator results and batch-write to DB |
| Exception isolation | Entire `EvaluateAsync` wrapped in try-catch; failures logged, never thrown |

### IRuleEvaluator (Evaluator Interface)

```csharp
public interface IRuleEvaluator
{
    AlertRuleType RuleType { get; }
    Task<Alert?> EvaluateAsync(AlertRule rule, TelemetryReading reading);
}
```

Each evaluator does one thing: given a rule and a reading, return an `Alert` or `null`.

### Built-in Evaluators

| Evaluator | RuleType | Logic |
|-----------|----------|-------|
| SpeedRuleEvaluator | Speed | `reading.Speed > rule.Threshold` → trigger |
| GeoFenceRuleEvaluator | GeoFence | Parse `GeoFenceSpec` JSON, check if vehicle is inside the zone → trigger |
| IdleRuleEvaluator | Idle | Track per-device last movement time; stationary duration ≥ `Threshold` → trigger |

## Data Flow

```
TruckMocker → MQTT → SQS → SqsConsumerWorker → Channel<TelemetryReading>
                                                      │
                                              TelemetryBatchWriter
                                                      │
                                               Write TelemetryReading to DB
                                                      │
                                         AlertEngine.EvaluateAsync(reading)
                                                      │
                                              ┌───────┴───────┐
                                              │ Filter rules   │
                                              │ by device type │
                                              └───────┬───────┘
                                              ┌───────┴───────┐
                                              │ Cooldown check │
                                              │ + dispatch     │
                                              └───────┬───────┘
                                                  ┌───┴───--┐
                                          ┌───────┤evaluator├───────┐
                                          │       └───────--┘       │
                                     Alert or null            Alert or null
                                          │                         │
                                          └─────────┬───────────----┘
                                              ┌─────┴─────-┐
                                              │Write Alerts│
                                              └───────────-┘
```

## DI Registration

```csharp
// Worker/Program.cs
services.AddSingleton<IAlertEngine, AlertEngine>();
services.AddSingleton<IRuleEvaluator, SpeedRuleEvaluator>();
services.AddSingleton<IRuleEvaluator, GeoFenceRuleEvaluator>();
services.AddSingleton<IRuleEvaluator, IdleRuleEvaluator>();
```

To add a new rule type: implement `IRuleEvaluator` + register in DI. No changes to AlertEngine.

## Internal State

| State | Type | Description |
|-------|------|-------------|
| `_cachedRules` | `List<AlertRule>` | In-memory cache of enabled rules |
| `_rulesLoadedAt` | `DateTime` | Cache load timestamp, used for TTL check |
| `_lastAlertByRuleDevice` | `ConcurrentDictionary<string, DateTime>` | Key = `"{ruleId}_{deviceId}"`, Value = last trigger time, used for cooldown |

## Seed Data (Default Rules)

| Rule | Type | Threshold | Device Type | Cooldown |
|------|------|-----------|-------------|----------|
| Speed Limit - Trucks | Speed | 11.11 m/s (40 km/h) | Truck | 300s |
| Restricted Zone - Office Area | GeoFence | 300m radius circle | All | 0s |
| Idle Timeout - Trucks | Idle | 300s (5 min) | Truck | 600s |

## TODO

- [ ] Rule cache subscription — replace 30s polling with PostgreSQL NOTIFY/LISTEN or MQTT for real-time updates
- [ ] Device type lookup cache — avoid querying DB on every evaluation
- [ ] `MatchesDeviceType` sync `.Result` — refactor to async or use a separate cache
