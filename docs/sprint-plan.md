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

## Sprint 2: Core Business Features

**Goal:** Build the features that make the system usable — alerts, real-time data, user management.

### Day 1: Alert Engine — Data Model & Migration

The highest-value feature. Mine vehicle monitoring without alerts is just data storage.

| Task | Files | Deliverable |
|------|-------|-------------|
| Create `AlertRule` entity with enums | `Infrastructure/Entities/AlertRule.cs` | AlertRuleType (Speed/GeoFence/Idle), AlertSeverity, threshold, cooldown, device scope, enabled flag |
| Create `Alert` entity with enums | `Infrastructure/Entities/Alert.cs` | AlertStatus (Active/Acknowledged/Resolved), trigger metadata (lat/lon/speed), FK to rule + device + reading |
| Update `MineWatchDbContext` | `Infrastructure/Data/MineWatchDbContext.cs` | DbSet<AlertRule>, DbSet<Alert>, indexes on Status/DeviceId/TriggeredAt/IsEnabled |
| Run EF Core migration | `Infrastructure/Migrations/` | AddAlertSystem migration |
| Update `DbSeeder` | `Infrastructure/Data/DbSeeder.cs` | 3 seed rules: speed 120km/h, restricted zone circle, idle 5min |

**Done when:** `dotnet ef database update` creates AlertRules + Alerts tables, seed data visible.

---

### Day 2: Alert Engine — Core Logic

| Task | Files | Deliverable |
|------|-------|-------------|
| Create `GeoHelper` | `Api/Services/AlertEngine/GeoHelper.cs` | Haversine distance, point-in-polygon, GeoFenceSpec JSON parsing |
| Create `IAlertEngine` + `AlertEngine` | `Api/Services/AlertEngine/AlertEngine.cs` | Evaluate speed/geo/idle rules, rule cache (30s TTL), idle state tracking, cooldown |
| Wire into `SqsConsumerWorker` | `Worker/Services/SqsConsumerWorker.cs` | Call EvaluateAsync after DB write, wrapped in try-catch |
| Register in DI | `Api/Program.cs` | AddSingleton<IAlertEngine, AlertEngine>() |

**Done when:** TruckMocker sends telemetry → AlertEngine evaluates rules → Alert records created in DB when rules trigger.

---

### Day 3: Alert Engine — API Endpoints

| Task | Files | Deliverable |
|------|-------|-------------|
| Create `AlertDTOs` | `Api/DTOs/AlertDTOs.cs` | CreateRule, UpdateRule, RuleResponse, AlertResponse, AcknowledgeRequest |
| Create `IAlertService` + `AlertService` | `Api/Services/AlertService.cs` | Rule CRUD, alert query (filter by status/device/rule), acknowledge, resolve |
| Create `AlertsController` | `Api/Controllers/AlertsController.cs` | 9 endpoints: rules CRUD (5) + alerts query/acknowledge/resolve (4) |
| Register in DI | `Api/Program.cs` | AddScoped<IAlertService, AlertService>() |

**Done when:** All 9 endpoints work via Swagger. Create rule → trigger alert → query → acknowledge → resolve.

---

### Day 4: Telemetry Query API

| Task | Files | Deliverable |
|------|-------|-------------|
| Create `TelemetryController` | `Api/Controllers/TelemetryController.cs` | Query endpoints |
| `GET /api/telemetry/latest` | Filter by vehicleNo | Latest vehicle position |
| `GET /api/telemetry/history` | Paginated time-range query | Historical trajectory |
| Create `TelemetryDTOs` | `Api/DTOs/TelemetryDTOs.cs` | LatestPositionResponse, HistoryResponse |

**Done when:** Query latest position and historical trail for a vehicle via API.

---

### Day 5: SignalR Real-Time Push

| Task | Files | Deliverable |
|------|-------|-------------|
| Create `TelemetryHub` | `Api/Hubs/TelemetryHub.cs` | WebSocket endpoint for live telemetry |
| Wire Worker → API notification | `Worker/Services/SqsConsumerWorker.cs` | Worker publishes to SNS after write, API subscribes via SQS |
| Front-end can subscribe | `TelemetryHub` | Client receives live position updates |

**Done when:** Browser connects via WebSocket, receives live telemetry as TruckMocker publishes.

---

### Day 6: ASP.NET Identity

| Task | Files | Deliverable |
|------|-------|-------------|
| Add ASP.NET Identity with EF Core | `Program.cs` | User registration + login + hashed passwords |
| Rewrite AuthController | `Controllers/AuthController.cs` | /api/auth/register, /api/auth/login, /api/auth/refresh |
| Add role-based authorization | `Program.cs`, Controllers | Admin/Operator/Viewer roles on write endpoints |
| Move JWT config to User Secrets | `appsettings.Development.json` | No secrets in source code |

**Done when:** Register → Login → Get token → Access protected endpoint. No hardcoded credentials.

---

### Day 7: Alert Engine Integration Tests

| Task | Files | Deliverable |
|------|-------|-------------|
| Create `MineWatch.IntegrationTests` project | `tests/MineWatch.IntegrationTests/` | xUnit + WebApplicationFactory + InMemory DB |
| Create test infrastructure | `CustomWebApplicationFactory`, `TestAuthHandler` | Auth bypass + InMemory DB |
| AlertEngine integration tests | `AlertEngineIntegrationTests.cs` | Speed/geo/idle trigger + no-trigger, cooldown, disabled rule, multiple rules |
| AlertsController e2e tests | `AlertsControllerTests.cs` | Rule CRUD, alert query, acknowledge, resolve |
| Update CI | `.github/workflows/ci.yml` | Run integration tests |

**Done when:** 16+ test cases all passing in CI.

---

### Day 8: Documentation & Polish

| Task | Deliverable |
|------|-------------|
| Update README.md | Architecture diagram, endpoints, updated structure |
| Update ROADMAP.md | Mark completed items |
| Create CHANGELOG.md | Structured release notes |
| `dotnet format` | Consistent code style |
| End-to-end verification | TruckMocker → Worker → alerts → API query all works |

---

### Sprint 2 Milestone Deliverables

| Deliverable | Verification |
|-------------|-------------|
| Alert engine | Create rule → trigger alert → query → acknowledge → resolve |
| Real-time telemetry | SignalR client receives live updates |
| Telemetry query | Latest position + historical trail API |
| User auth | Register → Login → Role-based access |
| Integration tests | 16+ test cases in CI |
| Documentation | README, ROADMAP, CHANGELOG accurate |

---

## Final Project Architecture

```
┌──────────────┐     ┌────────────────────┐     ┌──────────────────────┐
│  TruckMocker  │────>│  MineWatch.Worker   │────>│  PostgreSQL           │
│  (Simulator)  │ MQTT│  - MQTT Subscriber  │     │  - Devices            │
└──────────────┘     │  - SQS Consumer     │     │  - TelemetryReadings  │
                     │  - Batch Writer     │     │  - AlertRules         │
                     │  - Alert Engine     │     │  - Alerts             │
                     │  - Prometheus       │     │  - Users (Identity)   │
                     └────────┬───────────┘     └──────────────────────┘
                              │ SNS/SQS
                     ┌────────┴───────────┐
                     │  MineWatch.Api      │
                     │  - REST API         │
                     │  - SignalR Hub      │
                     │  - Auth (Identity)  │
                     │  - Prometheus       │
                     │  - Health Checks    │
                     └────────────────────┘
```
