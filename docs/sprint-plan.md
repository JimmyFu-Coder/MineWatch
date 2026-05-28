# Sprint Plan

## Sprint 1: Quality & Architecture (Week 1)

**Goal:** Fix all bugs, introduce microservice architecture, add observability stack.

### Day 1-2: Bug Fixes & Code Quality

| Task | Files | Deliverable |
|------|-------|-------------|
| Fix ErrorResponse missing TraceId | `Middleware/ExceptionHandlingMiddleware.cs` | TraceId included in all error responses |
| Fix `DateTime.Now` → `DateTime.UtcNow` | `AuthController.cs` | UTC-consistent token expiry |
| Use `PageResponse<T>` in controller | `DevicesController.cs`, `DeviceDTOs.cs` | Paginated response matches DTO |
| Fix `TelemetryReading.DeviceId` nullable mismatch | `TelemetryReading.cs` + migration | Entity matches DB schema |
| Fix Seeder logic (enable in Development) | `Program.cs` | Seed data available locally |
| Register TelemetryBatchWriter or remove dead code | `Program.cs`, `SqsConsumerWorker.cs` | Batch writing actually used |
| Fix NameIdentifier claim (random GUID → meaningful ID) | `AuthController.cs` | Stable user identity in tokens |
| Remove empty placeholder test projects | `MineWatch.UnitTests/`, `MineWatch.Infrastructure.Tests/` | No dead test projects |

**Sprint 1 Deliverables — Day 1-2:**
- [ ] All code bugs fixed and verified by tests
- [ ] Zero dead code in the solution
- [ ] Consistent error response format with TraceId
- [ ] Paginated API responses using proper DTO

---

### Day 3-4: Microservice Split — Extract Worker Service

Split the monolithic API into two independently deployable services.

```
Before:                          After:
┌──────────────────────┐         ┌──────────────────┐     ┌─────────────────────┐
│  MineWatch.Api        │         │  MineWatch.Api    │     │  MineWatch.Worker    │
│  - Controllers        │   →     │  - Controllers    │     │  - MqttSubscriber    │
│  - Auth               │         │  - Auth           │     │  - SqsConsumer       │
│  - MqttSubscriber     │         │  - SignalR Hub    │     │  - TelemetryBatch    │
│  - SqsConsumer        │         │  - AlertEngine    │     │  - TelemetryParser   │
│  - TelemetryBatch     │         │  - DeviceService  │     │                     │
│  - TelemetryParser    │         └──────────────────┘     └─────────────────────┘
└──────────────────────┘                   │                        │
                                           └──── SQS ───────────────┘
```

| Task | Files | Deliverable |
|------|-------|-------------|
| Create `MineWatch.Worker` project | `src/MineWatch.Worker/` | New console app with Host builder |
| Create `MineWatch.Contracts` class library | `src/MineWatch.Contracts/` | Shared DTOs (TelemetryMessage, etc.) |
| Move BackgroundServices to Worker | MqttSubscriber, SqsConsumer, BatchWriter, Parser | Clean service boundary |
| Update API Program.cs | Remove all BackgroundService registrations | API only handles HTTP |
| Update solution file | `MineWatch.sln` | All 4 projects registered |
| Add Worker ECS task + ECR to Terraform | `infra/terraform/` | Worker deployable independently |

**Sprint 1 Deliverables — Day 3-4:**
- [ ] `MineWatch.Api` — REST API only, no background processing
- [ ] `MineWatch.Worker` — MQTT ingestion + SQS consumption + batch writing
- [ ] `MineWatch.Contracts` — shared message types
- [ ] Terraform supports deploying API and Worker as separate ECS services
- [ ] Both services have their own Dockerfile

---

### Day 5: Observability — Logging, Metrics, Health Checks

| Task | Files | Deliverable |
|------|-------|-------------|
| Add Serilog with JSON structured logging | `Program.cs` (both services) | Compact JSON logs to stdout |
| Add Serilog enrichers (ThreadId, MachineName, Environment) | `Program.cs` | Consistent log properties |
| Add OpenTelemetry Prometheus metrics endpoint | `Program.cs` (both services) | `/metrics` endpoint with request count, latency, SQS message count |
| Replace hand-rolled HealthController with `AddHealthChecks()` | `Program.cs`, delete `HealthController.cs` | Standard health check framework |
| Add custom health checks (PostgreSQL, MQTT connectivity, SQS) | `HealthChecks/` | `/health/ready` checks all dependencies |
| Add structured logging to all services | All service files | Log templates instead of string interpolation |

**Sprint 1 Deliverables — Day 5:**
- [ ] JSON structured logs via Serilog
- [ ] `/metrics` Prometheus endpoint on both services
- [ ] ASP.NET Core health checks framework with DB + MQTT + SQS probes
- [ ] ECS-compatible log output (stdout → CloudWatch)

---

### Day 6-7: Tests & CI/CD Upgrade

| Task | Files | Deliverable |
|------|-------|-------------|
| AuthController integration tests (WebApplicationFactory) | `MineWatch.Api.Tests/` | Login success/failure, token validation |
| DevicesController integration tests | `MineWatch.Api.Tests/` | CRUD + pagination + auth required |
| DeviceService unit tests | `MineWatch.Api.Tests/` | All CRUD operations + edge cases |
| TelemetryParser negative tests | `MineWatch.Api.Tests/` | Malformed JSON, missing fields, null values |
| Worker service tests | `MineWatch.Worker.Tests/` | MQTT/SQS pipeline tests |
| CI: add `dotnet format --verify-no-changes` | `.github/workflows/ci.yml` | Lint enforcement |
| CI: add code coverage collection | `.github/workflows/ci.yml` | Coverage report in CI |
| CI: add Docker build step | `.github/workflows/ci.yml` | Verify Dockerfile builds successfully |
| CI: add Terraform validate step | `.github/workflows/ci.yml` | Verify Terraform syntax |

**Sprint 1 Deliverables — Day 6-7:**
- [ ] Test coverage ≥ 50%
- [ ] Controller integration tests for all endpoints
- [ ] Negative/edge case tests for parsers and services
- [ ] CI pipeline: lint + build + test + coverage + Docker build + Terraform validate

---

### Sprint 1 Milestone Deliverables

| Deliverable | Verification |
|-------------|-------------|
| Zero code bugs | All existing issues resolved, no compiler warnings |
| Microservice architecture | API and Worker run as separate processes |
| Observability stack | Structured logs + Prometheus metrics + Health checks |
| CI/CD pipeline | Full pipeline passes on every push |
| Test coverage ≥ 50% | `dotnet test` with coverlet report |

---

## Sprint 2: Commercial Features (Week 2)

**Goal:** Add commercial-grade features that demonstrate production readiness.

### Day 8: API Versioning & Rate Limiting

| Task | Files | Deliverable |
|------|-------|-------------|
| Add API versioning (`Asp.Versioning.Http`) | `Program.cs`, Controllers | Routes: `/api/v1/devices` |
| Add Swagger multi-version support | `Program.cs` | Swagger UI shows v1 |
| Add rate limiting middleware | `Program.cs` | Fixed window limiter: 100 req/min per IP |
| Add CORS policy | `Program.cs` | Configured for frontend origin |

**Sprint 2 Deliverables — Day 8:**
- [ ] Versioned API routes (`/api/v1/...`)
- [ ] Swagger documentation per version
- [ ] Rate limiting enforced on all endpoints
- [ ] CORS configured

---

### Day 9: User Authentication System

| Task | Files | Deliverable |
|-------------|-------|-------------|
| Add ASP.NET Identity with EF Core | `Program.cs`, new `IdentityUser` | User registration + login + hashed passwords |
| Create AuthController (register, login, refresh) | `AuthController.cs` rewrite | `/api/v1/auth/register`, `/api/v1/auth/login` |
| Remove hardcoded admin/admin | `AuthController.cs` | No hardcoded credentials |
| Move JWT config to User Secrets (dev) + env vars (prod) | `appsettings.Development.json` | No secrets in source code |
| Add role-based authorization (Admin, Operator, Viewer) | `Program.cs`, Controllers | `[Authorize(Roles = "Admin")]` on write endpoints |

**Sprint 2 Deliverables — Day 9:**
- [ ] User registration and login with ASP.NET Identity
- [ ] Password hashing (bcrypt via Identity)
- [ ] JWT token with stable user ID and role claims
- [ ] Role-based access control (Admin/Operator/Viewer)
- [ ] Zero hardcoded credentials in source code

---

### Day 10: Telemetry Query API & SignalR

| Task | Files | Deliverable |
|------|-------|-------------|
| Create TelemetryController | `Controllers/TelemetryController.cs` | Query endpoints |
| `GET /api/v1/telemetry/latest` | Filter by vehicleNo, return latest position | Latest vehicle position |
| `GET /api/v1/telemetry/history` | Paginated time-range query | Historical trajectory |
| Create SignalR TelemetryHub | `Hubs/TelemetryHub.cs` | WebSocket endpoint |
| Wire Worker → SQS → API notification | Worker publishes to SNS topic, API subscribes | Real-time telemetry push to clients |

**Sprint 2 Deliverables — Day 10:**
- [ ] Telemetry query API (latest position + historical trajectory)
- [ ] SignalR hub for real-time telemetry streaming
- [ ] Paginated time-range queries with proper DTOs

---

### Day 11: Alert Engine

| Task | Files | Deliverable |
|------|-------|-------------|
| Create AlertRule entity | `Entities/AlertRule.cs` | Rule configuration (type, threshold, vehicleId) |
| Create Alert entity | `Entities/Alert.cs` | Alert record (rule, triggeredAt, status) |
| Create AlertEngine service | `Services/AlertEngine.cs` | Evaluates telemetry against rules |
| Wire AlertEngine into Worker pipeline | After telemetry write | Rules evaluated on every incoming reading |
| Create AlertsController | `Controllers/AlertsController.cs` | CRUD rules + query alerts |
| Add DB migration for AlertRule + Alert tables | `Migrations/` | New tables |

**Sprint 2 Deliverables — Day 11:**
- [ ] Alert rule CRUD API
- [ ] Automatic rule evaluation on telemetry ingestion
- [ ] Alert records stored and queryable
- [ ] Built-in rules: speed threshold, idle timeout, geo-fence breach

---

### Day 12: Documentation Update

| Task | Files | Deliverable |
|------|-------|-------------|
| Update README.md | Architecture diagram, new endpoints, updated structure | Accurate project overview |
| Update ROADMAP.md | Mark Sprint 1-2 items complete | Current progress visible |
| Update infra/README.md | Match actual module structure | No misleading file listing |
| Fix TruckMocker SPEC.md | Match actual implementation | Spec = reality |
| Create CHANGELOG.md | Sprint 1 + Sprint 2 changes | Version history |

**Sprint 2 Deliverables — Day 12:**
- [ ] All documentation matches actual code
- [ ] CHANGELOG with structured release notes
- [ ] README with microservice architecture diagram

---

### Day 13-14: Buffer & Final Polish

| Task | Deliverable |
|------|-------------|
| Fill remaining test gaps | Coverage pushes toward 60%+ |
| `dotnet format` entire solution | Consistent code style |
| End-to-end verification | All features work together |
| Final commit cleanup | Clean git history |

---

### Sprint 2 Milestone Deliverables

| Deliverable | Verification |
|-------------|-------------|
| API versioning | Swagger shows versioned routes |
| User auth system | Register → Login → Access protected endpoint |
| Real-time telemetry | SignalR client receives live updates |
| Alert engine | Create rule → trigger alert → query alert record |
| Production documentation | README, ROADMAP, CHANGELOG all accurate |

---

## Final Project Architecture

```
┌──────────────┐     ┌────────────────────┐     ┌──────────────────────┐
│  TruckMocker  │────>│  MineWatch.Worker   │────>│  PostgreSQL           │
│  (Simulator)  │ MQTT│  - MQTT Subscriber  │     │  - Devices            │
└──────────────┘     │  - SQS Consumer     │     │  - TelemetryReadings  │
                     │  - Batch Writer     │     │  - AlertRules         │
                     │  - Alert Engine     │     │  - Alerts             │
                     │  - Prometheus /metrics│    │  - Users (Identity)   │
                     └────────┬───────────┘     └──────────────────────┘
                              │ SQS
                     ┌────────┴───────────┐
                     │  AWS SQS + DLQ      │
                     └────────┬───────────┘
                              │
                     ┌────────┴───────────┐
                     │  MineWatch.Api      │
                     │  - REST API (v1)    │
                     │  - SignalR Hub      │
                     │  - Auth (Identity)  │
                     │  - Prometheus       │
                     │  - Health Checks    │
                     └────────────────────┘
```
