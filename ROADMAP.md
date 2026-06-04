# MineWatch Product Roadmap

## Phase 1: Core Monitoring Platform (Completed)

Real-time fleet telemetry ingestion with device management.

### Milestone 1.1 — API Foundation
- [x] ASP.NET Core Web API project setup
- [x] JWT authentication
- [x] Device CRUD endpoints
- [x] Global exception handling middleware
- [x] Swagger / OpenAPI documentation

**Deliverables:** REST API with auth, device management, API docs

### Milestone 1.2 — Data Layer
- [x] PostgreSQL with EF Core
- [x] Device and TelemetryReading entities
- [x] Database migrations
- [x] IDbContextFactory pattern for background services

**Deliverables:** Persistent data layer, migration pipeline

### Milestone 1.3 — Telemetry Pipeline
- [x] MQTT subscriber (BackgroundService)
- [x] Telemetry message parsing
- [x] AWS SQS integration with DLQ
- [x] SQS consumer worker (BackgroundService)
- [x] Channel-based batch writer with retry logic

**Deliverables:** End-to-end telemetry flow: MQTT → SQS → batch → PostgreSQL

### Milestone 1.4 — Simulation & DevOps
- [x] TruckMocker GPS simulator
- [x] Docker Compose for local development
- [x] GitHub Actions CI pipeline
- [x] Unit tests for core services

**Deliverables:** Local development environment, CI, test suite

---

## Phase 2: Real-Time Dashboard & Alerting (Completed)

Make telemetry data visible and actionable.

### Milestone 2.1 — Real-Time Data API
- [x] Telemetry query endpoints (latest position, historical trail)
- [x] SignalR WebSocket hub for live position updates
- [x] Geo-fence zone management via alert rules API

**Deliverables:** Queryable telemetry API, real-time push to clients

### Milestone 2.2 — Alert Engine

**Rule Types:**
- [x] Speed Threshold — trigger when vehicle speed exceeds limit
- [x] Geo-Fence Breach — trigger when vehicle enters/leaves restricted zone (circle or polygon)
- [x] Idle Timeout — trigger when vehicle is stationary beyond threshold

**Rule Management:**
- [x] Device-type-scoped rules (`DeviceType == null` matches all)
- [x] Enable / disable rules without deletion
- [x] Severity levels: Low / Medium / High / Critical
- [x] Cooldown period per rule to prevent alert storms

**Alert Lifecycle:**
- [x] Active → Acknowledged → Resolved status flow
- [x] Record trigger location (lat/lon), speed, timestamp, and triggering telemetry reading ID

**Data Model:**
- [x] `AlertRule` entity — rule type, threshold, severity, device scope, cooldown, enabled flag
- [x] `Alert` entity — linked to rule + device + telemetry reading, status, message, trigger metadata
- [x] EF Core migration with indexes on `Alert.Status`, `Alert.DeviceId`, `Alert.TriggeredAt`

**API Endpoints:**
- [x] `POST   /api/alerts/rules` — create alert rule
- [x] `GET    /api/alerts/rules` — list rules (paginated)
- [x] `GET    /api/alerts/rules/{id}` — get rule detail
- [x] `PUT    /api/alerts/rules/{id}` — update rule
- [x] `DELETE /api/alerts/rules/{id}` — delete rule
- [x] `GET    /api/alerts` — query alerts (filter by status / deviceId / ruleId, paginated)
- [x] `PUT    /api/alerts/{id}/acknowledge` — acknowledge alert
- [x] `PUT    /api/alerts/{id}/resolve` — resolve alert

**Pipeline Integration:**
- [x] `AlertEngine` evaluates rules against each incoming `TelemetryReading` after DB write
- [x] In-memory rule cache (30s TTL)
- [x] In-memory idle state tracking (per-device)
- [x] Alert evaluation failure does not block telemetry ingestion

**Seed Data:**
- [x] 3 default rules on first run: speed limit 40 km/h, restricted zone (circle near Perth CBD), idle 5 min

**Data Flow:**
```
TruckMocker → MQTT → SQS → SqsConsumerWorker
                                    │
                            (1) Write TelemetryReading to DB
                            (2) AlertEngine.EvaluateAsync(reading)
                                    │
                            Write Alert records to DB
```

**Deliverables:** Configurable alert rules, automatic evaluation on telemetry ingestion, queryable alert API, alert lifecycle management

### Milestone 2.3 — Notification Pipeline
- [x] Worker publishes telemetry + alert notifications to SQS
- [x] API NotificationWorker consumes SQS and pushes via SignalR
- [x] `INotificationPublisher` interface with `NotificationPublisher` implementation

**Deliverables:** Notification pipeline from Worker to SignalR clients

### Milestone 2.4 — Authentication & Authorization
- [x] ASP.NET Identity with `IdentityDbContext`
- [x] User registration with role assignment
- [x] JWT Bearer tokens with role claims
- [x] Role-based authorization: Admin, Operator, Viewer
- [x] Seeded admin user (admin/Admin@123)
- [x] No hardcoded credentials in source code

**Deliverables:** Production-ready auth with user management and role-based access

### Milestone 2.5 — Integration Tests & Verification
- [x] `MineWatch.IntegrationTests` project (xUnit + InMemory DB)
- [x] AlertEngine integration tests (speed, geo-fence, idle, cooldown, device type, disabled, multiple rules, exception isolation)
- [x] Identity integration tests (register, login, duplicate, role assignment)
- [x] Notification pipeline integration tests (telemetry flow, alert flow)
- [x] Docker Compose full-pipeline verification script (`verify-pipeline.sh`)

**Deliverables:** 52 unit tests + 13 integration tests, end-to-end verification

---

## Phase 3: Web Dashboard (Planned)

### Milestone 3.1 — Frontend Application
- [ ] React / Blazor front-end
- [ ] Live fleet map with vehicle positions
- [ ] Alert list and acknowledgment UI
- [ ] Device management interface
- [ ] Rule management UI

**Deliverables:** Operational dashboard for fleet monitoring

---

## Phase 4: Production Hardening & Scale (Planned)

### Milestone 4.1 — Infrastructure
- [ ] Terraform / CDK IaC for AWS deployment
- [ ] Container orchestration (ECS / EKS)
- [ ] Database backup and disaster recovery
- [ ] Auto-scaling and load testing

**Deliverables:** Cloud deployment pipeline, scalable infrastructure

### Milestone 4.2 — Reliability
- [ ] Structured logging (Serilog → CloudWatch)
- [ ] Metrics and monitoring (Prometheus / Grafana dashboards)
- [ ] Graceful shutdown and data durability guarantees
- [ ] Health checks with comprehensive dependencies

**Deliverables:** Production observability stack

### Milestone 4.3 — Advanced Features
- [ ] Audit logging
- [ ] API key management for device authentication
- [ ] Secrets management (AWS Secrets Manager)
- [ ] Time-series aggregation (hourly/daily summaries)
- [ ] Scheduled report generation

**Deliverables:** Production-ready security and reporting
