# Sprint Plan

## Sprint 1: Quality & Architecture (Completed 2026-05-29)

**Goal:** Fix all bugs, introduce microservice architecture, add observability stack.

### Completed

- [x] Bug fixes: AuthController config-driven credentials, SqsConsumerWorker pipeline connected
- [x] Microservice split: API + Worker + Contracts, separate Dockerfiles, docker-compose updated
- [x] Observability: Serilog structured JSON logs, ASP.NET Core health checks, Prometheus metrics
- [x] CI: build + format + test on push to master
- [x] Rate limiting (100 req/min/IP) + CORS

**Decision records:** [ADR-001](adr/001-deployment-target.md) — ECS Fargate for both services

---

## Sprint 2: Core Business Features (Completed 2026-06-04)

**Goal:** Build the features that make the system usable — alerts, real-time data, user management.

### Day 1: Alert Engine — Data Model & Migration ✅

| Task | Deliverable |
|------|-------------|
| Create `AlertRule` entity with enums | AlertRuleType (Speed/GeoFence/Idle), AlertSeverity, threshold, cooldown, device scope, enabled flag |
| Create `Alert` entity with enums | AlertStatus (Active/Acknowledged/Resolved), trigger metadata (lat/lon/speed), FK to rule + device + reading |
| Update `MineWatchDbContext` | DbSet<AlertRule>, DbSet<Alert>, indexes on Status/DeviceId/TriggeredAt/IsEnabled |
| Run EF Core migration | AddAlertSystem migration |
| Update `DbSeeder` | 3 seed rules: speed 40km/h, restricted zone circle (Perth CBD), idle 5min |

---

### Day 2: Alert Engine — Core Logic ✅

| Task | Deliverable |
|------|-------------|
| Create `GeoHelper` | Haversine distance, point-in-circle, point-in-polygon, GeoFenceSpec JSON parsing |
| Create `IAlertEngine` + `AlertEngine` | Evaluate speed/geo/idle rules, rule cache (30s TTL), idle state tracking, cooldown |
| Create `IRuleEvaluator` + 3 evaluators | SpeedRuleEvaluator, GeoFenceRuleEvaluator, IdleRuleEvaluator |
| Wire into `TelemetryBatchWriter` | Call EvaluateAsync after DB write, exception isolation |
| Register in DI | AddSingleton<IAlertEngine>, AddSingleton<IRuleEvaluator> x3 |
| 52 unit tests | Speed (4), GeoFence (7), Idle (5), AlertEngine (8), GeoHelper (9), Notification (4+5) |

---

### Day 3: Alert Engine — API Endpoints ✅

| Task | Deliverable |
|------|-------------|
| Create `AlertService` | Rule CRUD, alert query/filter, acknowledge, resolve |
| Create `AlertsController` | 8 endpoints: rules CRUD (5) + alerts query/acknowledge/resolve (3), role-based auth |
| Create `TelemetryController` | GET latest position, GET historical trail (paginated) |
| Register in DI | AddScoped<IAlertService, AlertService>() |

---

### Day 4: SignalR Real-Time Push ✅

| Task | Deliverable |
|------|-------------|
| Create `TelemetryHub` | SignalR hub with SubscribeVehicle/UnsubscribeVehicle group methods |
| Create `NotificationPublisher` | Worker publishes telemetry + alert notifications to SQS |
| Create `NotificationWorker` | API BackgroundService consumes SQS → pushes via SignalR (TelemetryUpdate, AlertReceived) |
| Create `NotificationMessage` contract | Shared DTO with Type (telemetry/alert) + Payload |

---

### Day 5: Notification Pipeline Integration ✅

| Task | Deliverable |
|------|-------------|
| Wire NotificationPublisher into Worker | PublishTelemetryAsync + PublishAlertAsync after alert evaluation |
| Wire NotificationWorker into API | SQS consumer → SignalR dispatch |
| Integration tests | NotificationPipelineIntegrationTests (telemetry flow + alert flow) |

---

### Day 6: ASP.NET Identity ✅

| Task | Deliverable |
|------|-------------|
| Add IdentityDbContext | MineWatchDbContext inherits IdentityDbContext<IdentityUser> |
| Run EF Core migration | AddIdentity migration (AspNetUsers, Roles, Claims tables) |
| Rewrite AuthController | /api/auth/register (with role), /api/auth/login (JWT with role claims) |
| Update Program.cs | AddIdentity + JWT Bearer as default auth scheme |
| Move JWT config | Jwt:Key removed from appsettings.json, set via env vars |
| Seed roles + admin | DbSeeder creates Admin/Operator/Viewer roles + admin user (Admin@123) |
| Role-based auth on controllers | Admin on write endpoints, any auth on read endpoints |
| Integration tests | IdentityIntegrationTests (register, login, duplicate, invalid, role assignment) |

---

### Day 7: Integration Tests ✅

| Test Suite | Tests | Coverage |
|------------|-------|----------|
| AlertEngineIntegrationTests | 6 | Speed trigger, GeoFence trigger, cooldown, device type filter, multiple rules, exception isolation |
| IdentityIntegrationTests | 5 | Register, duplicate user, login valid/invalid, role assignment |
| NotificationPipelineIntegrationTests | 2 | Telemetry flow (SQS → SignalR), alert flow (SQS → SignalR) |

---

### Day 8: Docker Compose Verification & Documentation ✅

| Task | Deliverable |
|------|-------------|
| Fix Docker build issues | Worker aspnet base image, TruckMocker env vars, AWS credentials for LocalStack |
| Fix runtime issues | CORS policy, JWT auth scheme priority, auto-register devices, telemetry query |
| Create `verify-pipeline.sh` | One-command verification: 8 checks, all PASS |
| Update README.md | Architecture diagram, all endpoints, updated tech stack and structure |
| Update ROADMAP.md | Phase 2 marked complete |
| `dotnet format` | All files pass `--verify-no-changes` |

---

### Sprint 2 Summary

| Metric | Result |
|--------|--------|
| Unit tests | 52 passing |
| Integration tests | 13 passing |
| Docker services | 6/6 running |
| Alert rules | 3 (Speed, GeoFence, Idle) |
| Alerts triggered (demo) | 1000+ |
| API endpoints | 17 + SignalR hub |
| Auth | Identity + JWT + 3 roles |

---

## Final Project Architecture

```
┌──────────────┐     ┌────────────────────────────────┐     ┌──────────────────────┐
│  TruckMocker  │────>│  MineWatch.Worker               │────>│  PostgreSQL           │
│  (Simulator)  │ MQTT│  - MQTT Subscriber              │     │  - Devices            │
└──────────────┘     │  - SQS Bootstrap + Consumer      │     │  - TelemetryReadings  │
                     │  - TelemetryBatchWriter          │     │  - AlertRules         │
                     │  - AlertEngine (3 evaluators)    │     │  - Alerts             │
                     │  - NotificationPublisher         │     │  - Users (Identity)   │
                     └────────┬─────────────────────────┘     └──────────────────────┘
                              │ SQS notifications
                     ┌────────┴─────────────────────────┐
                     │  MineWatch.Api                    │
                     │  - REST API (17 endpoints)        │
                     │  - SignalR Hub (real-time push)   │
                     │  - Auth (Identity + JWT + Roles)  │
                     │  - Swagger UI                     │
                     │  - Prometheus metrics             │
                     │  - Health Checks                  │
                     └──────────────────────────────────┘
```
