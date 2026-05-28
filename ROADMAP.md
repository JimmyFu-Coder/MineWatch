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

## Phase 2: Real-Time Dashboard & Alerting

Make telemetry data visible and actionable.

### Milestone 2.1 — Real-Time Data API
- [ ] Telemetry query endpoints (latest position, historical trail)
- [ ] SignalR WebSocket hub for live position updates
- [ ] Geo-fence zone management API

**Deliverables:** Queryable telemetry API, real-time push to clients

### Milestone 2.2 — Alert Engine
- [ ] Rule-based alert system (speed threshold, geo-fence breach, idle timeout)
- [ ] Alert storage and query API
- [ ] Notification channels (email via SES, SMS via SNS)

**Deliverables:** Configurable alert rules, multi-channel notifications

### Milestone 2.3 — Web Dashboard
- [ ] React / Blazor front-end
- [ ] Live fleet map with vehicle positions
- [ ] Alert list and acknowledgment UI
- [ ] Device management interface

**Deliverables:** Operational dashboard for fleet monitoring

---

## Phase 3: Analytics & Reporting

Business intelligence from fleet data.

### Milestone 3.1 — Data Aggregation
- [ ] Time-series aggregation service (hourly/daily summaries)
- [ ] Distance traveled and fuel consumption estimation
- [ ] Utilization reports (active vs idle time)

**Deliverables:** Aggregated metrics API

### Milestone 3.2 — Reporting
- [ ] Scheduled report generation (PDF/Excel)
- [ ] Report delivery via email
- [ ] Custom date range queries and exports

**Deliverables:** Automated reporting pipeline

### Milestone 3.3 — Advanced Analytics
- [ ] Heat map of vehicle activity
- [ ] Predictive maintenance indicators
- [ ] Trend analysis and anomaly detection

**Deliverables:** Analytics dashboard with insights

---

## Phase 4: Production Hardening & Scale

### Milestone 4.1 — Security
- [ ] ASP.NET Identity with role-based access control
- [ ] API key management for device authentication
- [ ] Audit logging
- [ ] Secrets management (AWS Secrets Manager)

**Deliverables:** Production-ready auth and security

### Milestone 4.2 — Reliability
- [ ] Health checks with comprehensive dependencies
- [ ] Structured logging (Serilog → CloudWatch)
- [ ] Metrics and monitoring (Prometheus / Grafana)
- [ ] Graceful shutdown and data durability guarantees

**Deliverables:** Production observability stack

### Milestone 4.3 — Infrastructure
- [ ] Terraform / CDK IaC for AWS deployment
- [ ] Container orchestration (ECS / EKS)
- [ ] Database backup and disaster recovery
- [ ] Auto-scaling and load testing

**Deliverables:** Cloud deployment pipeline, scalable infrastructure
