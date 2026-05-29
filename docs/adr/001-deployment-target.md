# ADR-001: ECS Fargate for API and Worker services

**Status:** Accepted
**Date:** 2026-05-29
**Scope:** Infrastructure

## Decision

Both MineWatch.Api and MineWatch.Worker deploy to ECS Fargate. No Lambda usage in initial architecture.

## Context

After splitting the monolith into API (request-driven) and Worker (MQTT/SQS background processor), both services need a deployment target. API is request-driven and technically Lambda-compatible. Worker holds a persistent MQTT connection and requires always-on execution.

## Reasoning

| | ECS Fargate | Lambda |
|---|---|---|
| **Worker** | Sustains MQTT TCP connection natively. | Cannot hold persistent connections. Would require rearchitecting to IoT Core rules engine. |
| **API** | Always warm. ~$16/mo at 0.25 vCPU. | Cheaper at low traffic (~$0 idle). Cold start ~2s on .NET 9, mitigated by SnapStart. |

Single deployment target for both services reduces operational complexity. Fargate cost ($32/mo total) is acceptable at current scale.

## Consequences

- Worker is locked to container deployment — no serverless migration path without replacing MQTT with IoT Core event routing.
- API can migrate to Lambda + API Gateway when cost justifies. Migration path: swap ECS task for Lambda function, add `LambdaEntryPoint`, enable SnapStart. No application code changes required beyond entry point.
- Both services share the same Docker/CI pipeline, reducing build and deployment overhead.
