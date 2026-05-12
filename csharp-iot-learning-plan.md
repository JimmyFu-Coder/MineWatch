# C# 后端开发学习计划：IoT 实时监控平台

> **目标**：通过构建一个矿山设备实时监控平台，系统掌握 C# 高并发、微服务、DDD 和 AWS 部署，最终用于 C# 后端开发岗位求职。
>
> **周期**：约 30 周 | **每周投入**：10–15 小时 | **方向**：IoT / 实时监控

---

## 目录

1. [项目概述](#项目概述)
2. [技术栈总览](#技术栈总览)
3. [本地开发环境策略](#本地开发环境策略)
4. [Sprint 1 — 设备管理 + 遥测摄取（第 1–5 周）](#sprint-1--设备管理--遥测摄取第-15-周)
5. [Sprint 2A — 消息队列 + 告警引擎（第 6–10 周）](#sprint-2a--消息队列--告警引擎第-610-周)
6. [Sprint 2B — 实时推送 + 缓存 + 可观测性（第 11–15 周）](#sprint-2b--实时推送--缓存--可观测性第-1115-周)
7. [Sprint 3 — 高并发压力测试（第 16–19 周）](#sprint-3--高并发压力测试第-1619-周)
8. [Sprint 4 — DDD 重构 + 微服务拆分（第 20–25 周）](#sprint-4--ddd-重构--微服务拆分第-2025-周)
9. [Sprint 5 — AWS 生产化部署（第 26–30 周）](#sprint-5--aws-生产化部署第-2630-周)
10. [AWS 费用规划](#aws-费用规划)
11. [面试资产清单](#面试资产清单)

---

## 项目概述

**项目名称**：MineWatch — 矿山设备实时监控平台

**背景**：贴近 Raptor OS 的实际工作场景，三个独立微服务构成完整系统：

| 微服务 | 职责 |
|--------|------|
| 设备管理服务 | 设备注册、证书管理、元数据 CRUD |
| 遥测摄取服务 | 接收 MQTT 数据流，高并发写入 |
| 告警通知服务 | 阈值检测、规则引擎、多渠道通知 |

**最终可展示的面试话术**：

> "我在 AWS 上独立设计并交付了一个生产级 IoT 监控平台，涵盖 AWS IoT Core / SQS / SNS / Amazon MQ / ECS Fargate / RDS，全套 Terraform IaC 管理，GitHub Actions CI/CD 自动化部署。经过真实压力测试，遥测摄取吞吐达到 Xk msg/s，并据此做了 Channel 管道优化和数据库批量写入调优。"

---

## 技术栈总览

### 后端核心

- **语言 / 框架**：C# 13 · .NET 9 · ASP.NET Core Web API
- **ORM**：Entity Framework Core + PostgreSQL Provider
- **并发**：`Channel<T>` · TPL · `IAsyncEnumerable` · `SemaphoreSlim`
- **架构**：Clean Architecture · DDD · CQRS + MediatR
- **测试**：xUnit · Moq · TestContainers · WebApplicationFactory

### 消息队列（分层使用）

| 服务 | 用途 | 本地替代 |
|------|------|----------|
| Amazon SQS | 遥测数据异步缓冲，IoT Core 规则引擎推送 | LocalStack |
| Amazon SNS | 告警一对多广播（邮件 / SQS / Lambda） | LocalStack |
| Amazon MQ (RabbitMQ) | 微服务间事件总线，AMQP 语义 | Docker RabbitMQ |
| MassTransit | .NET 消息框架，屏蔽底层 broker 差异 | 同上 |

### AWS 基础设施

- **IoT 接入**：AWS IoT Core（MQTT over TLS，Thing 证书）
- **容器**：ECS Fargate + ECR
- **数据库**：RDS PostgreSQL（生产）/ Docker PostgreSQL（本地）
- **缓存**：ElastiCache Redis（生产）/ Docker Redis（本地）
- **监控**：CloudWatch + X-Ray
- **网关**：Amazon API Gateway
- **IaC**：Terraform

### CI/CD

- GitHub Actions → ECR Push → ECS Rolling Deploy
- OIDC 无密钥认证，蓝绿部署，PR 触发自动部署到 Staging

### 压力测试

- **工具**：k6（HTTP 压测）+ 自定义 MQTT publisher（MQTT 压测）
- **目标**：发现真实瓶颈，量化优化效果，产出可展示的压测报告

---

## 本地开发环境策略

**核心原则**：Sprint 1–4 全程本地开发，零云费用；Sprint 5 一次性上 AWS。

### 本地 ↔ AWS 对照表

| 组件 | 本地（Docker） | 生产（AWS） | 切换成本 |
|------|---------------|-------------|----------|
| PostgreSQL | `postgres:16` | RDS PostgreSQL | 连接字符串替换 |
| Redis | `redis:7` | ElastiCache | 连接字符串替换 |
| RabbitMQ | `rabbitmq:3-management` | Amazon MQ | MassTransit transport 配置 |
| SQS / SNS | LocalStack | Amazon SQS / SNS | endpoint URL 替换 |
| MQTT Broker | EMQX Docker | AWS IoT Core | 证书逻辑需适配（建议早期接入） |

### Docker Compose 启动命令

```bash
# 启动全部本地依赖
docker compose up -d

# 服务包含：PostgreSQL · Redis · RabbitMQ · EMQX · LocalStack
```

### AWS IoT Core 说明

> **建议 Sprint 1 即接入真实 IoT Core**，原因：
> - IoT Core 免费层：每月 250,000 条消息，**12 个月有效**（从账号创建日起算）
> - 设备证书认证逻辑与本地 EMQX 有差异，早接入早踩坑
> - 2025 年 7 月 15 日后新建账号可获最高 $200 免费 credits

---

## Sprint 1 — 设备管理 + 遥测摄取（第 1–5 周）

**目标**：搭建可运行的设备管理 API + MQTT 遥测摄取服务，数据持久化到 PostgreSQL。

**为什么压缩到 5 周**：CRUD + MQTT 订阅 + 批量写入是基础功能，7 周太宽松。省出的时间留给后续更重的 Sprint。

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 1 周 | 项目脚手架 + EF Core + Device 实体 | Solution 结构 · Docker Compose · 数据库迁移 |
| 第 2 周 | 设备 CRUD API + JWT Auth + Swagger | 完整 RESTful API · Swagger 可测试 |
| 第 3 周 | AWS IoT Core 接入 + MQTT 订阅后台服务 | `MqttSubscriberService : BackgroundService` · 消息可接收 |
| 第 4 周 | `Channel<T>` 批量写入 + 背压控制 | 遥测数据高效入库 · SemaphoreSlim 限流 |
| 第 5 周 | 单元测试 + 健康检查 + README + GitHub 推送 | xUnit 覆盖率 > 70% · Sprint 1 收尾 |

### 交付物

- [x] 设备管理 API（CRUD + JWT + Swagger）
- [ ] MQTT 订阅服务通过 AWS IoT Core 接收遥测数据
- [ ] `Channel<T>` 批量写入 PostgreSQL
- [ ] xUnit 单元测试覆盖率 > 70%
- [ ] README + 架构图

---

## Sprint 2A — 消息队列 + 告警引擎（第 6–10 周）

**目标**：引入 SQS 替代直连 MQTT 写库，实现告警规则引擎 + SNS 通知。

**为什么先做这个**：消息队列是架构的核心变更——把"MQTT 直连写库"变成"MQTT → SQS → Worker 消费写库"。先把数据管道稳定了，再加实时推送和缓存才有意义。

### 学习内容

- **SQS**：消费者模式 · 可见性超时 · 死信队列 · .NET AWS SDK
- **SNS**：Topic · Subscription · 多协议推送
- **Outbox Pattern**：消息不丢失保障
- **告警规则引擎**：阈值检测 · 规则配置 · 告警触发

### 消息队列架构

```
AWS IoT Core
    │  规则引擎路由
    ▼
Amazon SQS ──► .NET Worker Service ──► PostgreSQL（写遥测）
                    │
                    ▼ 阈值超限
               告警规则引擎
                    │
                    ▼
              Amazon SNS Topic
               ├── 邮件订阅
               ├── SQS 订阅 ──► 日志服务
               └── Lambda 订阅
```

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 6 周 | LocalStack 本地环境 + SQS 基础消费者 | LocalStack Docker 配置 · Worker Service 从 SQS 消费消息 |
| 第 7 周 | IoT Core 规则引擎 → SQS 路由 | 端到端：设备 → IoT Core → SQS → Worker 消费写库 |
| 第 8 周 | 告警规则引擎 | 阈值检测逻辑 · 告警记录持久化 · 可配置规则 |
| 第 9 周 | SNS 通知 + Outbox Pattern | 告警 → SNS → 邮件 · Outbox 保障消息不丢 |
| 第 10 周 | 集成测试 + 收尾 | LocalStack 集成测试 · SQS 死信队列验证 |

### 交付物

- [ ] 完整消息链路：IoT Core → SQS → Worker Service → PostgreSQL
- [ ] 告警规则引擎 + SNS 多渠道通知
- [ ] Outbox Pattern 落地
- [ ] LocalStack 集成测试

---

## Sprint 2B — 实时推送 + 缓存 + 可观测性（第 11–15 周）

**目标**：引入 SignalR 实时推送、Redis 缓存、MassTransit 事件总线、结构化日志。

**为什么放第二步**：有了稳定的数据管道和告警，再加实时推送和缓存是锦上添花，顺序自然。

### 学习内容

- **SignalR**：Hub · Redis Backplane · 客户端订阅
- **Redis**：设备状态缓存 · 告警去重 · 滑动窗口限流
- **Amazon MQ + MassTransit**：微服务间事件总线 · 消费者配置
- **可观测性**：Serilog 结构化日志 · CloudWatch Metrics

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 11 周 | SignalR Hub + 简单 React 前端演示页 | 遥测数据实时推送到浏览器 |
| 第 12 周 | Redis Backplane + 设备状态缓存 | SignalR 横向扩展 · 最新设备状态从 Redis 读取 |
| 第 13 周 | 告警去重 + 滑动窗口限流 | Redis 实现告警去重 · API 限流中间件 |
| 第 14 周 | Amazon MQ + MassTransit 事件总线 | Docker RabbitMQ · 微服务间事件发布/订阅 |
| 第 15 周 | Serilog + CloudWatch Metrics + 收尾 | 结构化日志 · 关键指标埋点 · Sprint 2 完整收尾 |

### 交付物

- [ ] SignalR 实时推送 + 简单前端演示
- [ ] Redis 缓存最新设备状态 + 告警去重
- [ ] MassTransit 事件总线（RabbitMQ）
- [ ] Serilog 结构化日志 + CloudWatch Metrics

---

## Sprint 3 — 高并发压力测试（第 16–19 周）

**目标**：用真实压力把系统逼到极限，发现瓶颈，量化优化效果，产出可展示的压测报告。

**为什么单独成 Sprint**：之前压测挤在 Sprint 2 最后一行，根本来不及做。压测不只是"跑个 benchmark"——它是发现问题、验证架构设计是否有效的关键环节。单独 4 周，从施压 → 发现问题 → 优化 → 验证，形成完整闭环。

### 学习内容

- **压测工具**：k6（HTTP）· 自定义 MQTT publisher（MQTT）
- **性能分析**：dotnet-trace · dotnet-counters · EF Core 日志分析
- **数据库调优**：索引优化 · 批量写入策略 · 连接池配置
- **瓶颈分析**：CPU · 内存 · IO · 锁竞争 · Channel 积压

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 16 周 | 搭建压测环境 + 基线测试 | k6 脚本 · MQTT 压测工具 · 基线性能数据 |
| 第 17 周 | 施压 + 瓶颈定位 | 用 dotnet-trace / counters 定位瓶颈 · 记录问题清单 |
| 第 18 周 | 针对性优化 | 数据库索引 · Channel 配置调优 · 批量大小调整 · 连接池 |
| 第 19 周 | 优化后复测 + 压测报告 | 优化前后对比数据 · 压测报告（可用于面试） |

### 压测场景

| 场景 | 工具 | 指标 |
|------|------|------|
| MQTT 遥测涌入 | 自定义 publisher（100+ 设备） | 写入吞吐（msg/s）· 端到端延迟 · 丢失率 |
| 设备 API 并发读写 | k6 | RPS · P50/P95/P99 延迟 · 错误率 |
| 告警风暴 | 模拟批量阈值超限 | 告警处理延迟 · SNS 推送成功率 |
| Channel 积压恢复 | 突发流量后观察恢复时间 | 积压消化时间 · 内存使用 |

### 交付物

- [ ] k6 压测脚本（可重复运行）
- [ ] MQTT 压测工具
- [ ] 压测报告：基线数据 → 瓶颈分析 → 优化措施 → 优化后对比
- [ ] 面试可用的话术："通过压测发现 X 瓶颈，用 Y 方案优化，吞吐从 A 提升到 B"

---

## Sprint 4 — DDD 重构 + 微服务拆分（第 20–25 周）

**目标**：将单体服务按 DDD 拆分为三个独立微服务，引入 Clean Architecture + CQRS。

**为什么放在压测之后**：压测让你对系统的性能特征和数据流有了深刻理解。先压测再重构，你才知道哪些地方值得拆、哪些地方性能敏感不能乱动。而不是盲目地为了 DDD 而 DDD。

### 学习内容

- **DDD 战术模式**：聚合根 · 值对象 · 领域事件 · 仓储 · 领域服务
- **限界上下文划分**：设备管理 BC / 遥测采集 BC / 告警通知 BC
- **CQRS + MediatR**：Command Handler · Query Handler · Pipeline Behavior
- **Clean Architecture 分层**：Domain → Application → Infrastructure → API

### DDD 核心概念对照（IoT 场景）

| DDD 概念 | 在本项目中的体现 |
|----------|----------------|
| 聚合根 | `Device`（含设备状态、连接历史） |
| 值对象 | `DeviceLocation` · `TelemetryReading` · `AlertThreshold` |
| 领域事件 | `DeviceConnectedEvent` · `ThresholdBreachedEvent` |
| 仓储 | `IDeviceRepository` · `ITelemetryRepository` |
| 限界上下文 | 设备管理 / 遥测采集 / 告警通知（三个独立微服务） |

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 20–21 周 | DDD 战术设计 + Domain 层 | 识别聚合根、值对象、领域事件 · 纯 Domain 层无依赖 |
| 第 22–23 周 | Clean Architecture 分层 + CQRS | Domain → Application → Infrastructure → API 分层 · MediatR Handler |
| 第 24 周 | 微服务独立部署 | 三个服务独立项目 · 各自 Dockerfile · 独立运行 |
| 第 25 周 | 集成测试 + ADR 文档 | TestContainers 集成测试 · 架构决策记录 · DDD 架构图 |

### 交付物

- [ ] 三个微服务按 Clean Architecture 分层
- [ ] CQRS + MediatR 全面改造
- [ ] DDD 架构图（含限界上下文划分）
- [ ] ADR 架构决策记录
- [ ] 集成测试覆盖率 > 70%

---

## Sprint 5 — AWS 生产化部署（第 26–30 周）

**目标**：将三个微服务完整部署到 AWS，Terraform IaC 管理全套资源，GitHub Actions 自动化 CI/CD。

**为什么压缩到 4 周（原 6 周）**：你已有 AWS 认证 + Terraform + GitHub Actions 经验，这部分是你的强项，不需要 6 周。省出的 2 周给了前面的压测 Sprint。

### AWS 架构总览

```
Internet
    │
Amazon API Gateway（JWT Authorizer · Rate Limiting）
    │
Application Load Balancer
    ├── ECS Fargate — 设备管理服务
    ├── ECS Fargate — 遥测摄取服务
    └── ECS Fargate — 告警通知服务
         │
    ┌────┴────────────────────────────────┐
    │                                     │
Amazon MQ          RDS PostgreSQL         ElastiCache Redis
(RabbitMQ)         (Multi-AZ)             (集群模式)
    │
Amazon SQS ◄── AWS IoT Core ◄── 设备（MQTT over TLS）
    │
Amazon SNS
    ├── 邮件
    └── Lambda
         │
CloudWatch + X-Ray（监控 · 追踪 · 告警）
```

### Terraform 资源清单

```hcl
# 需要用 Terraform 管理的 AWS 资源
module "networking"   # VPC · Subnets · Security Groups · NAT Gateway
module "iot_core"     # IoT Core · Thing · Policy · Rule
module "messaging"    # SQS · SNS · Amazon MQ
module "compute"      # ECS Cluster · Task Definitions · Services · ALB
module "database"     # RDS PostgreSQL · Parameter Group · Subnet Group
module "cache"        # ElastiCache Redis · Subnet Group
module "gateway"      # API Gateway · JWT Authorizer · Stages
module "monitoring"   # CloudWatch Dashboards · Alarms · X-Ray
module "cicd"         # ECR · IAM Roles · OIDC Provider
```

### GitHub Actions CI/CD Pipeline

```yaml
# 触发条件：PR merge 到 main
jobs:
  build-and-test:     # dotnet build + dotnet test
  docker-build-push:  # Docker Build → ECR Push（OIDC 无密钥）
  deploy-staging:     # ECS Rolling Update → 健康检查 Gate
  deploy-production:  # 手动审批 → ECS Blue/Green Deploy
```

### 每周任务

| 周次 | 任务 | 产出 |
|------|------|------|
| 第 26 周 | Terraform 搭基础设施 | VPC · RDS · ElastiCache · SQS · SNS · Amazon MQ · ECS Cluster |
| 第 27 周 | ECS Fargate 部署三个服务 + API Gateway | Task Definition · Service · ALB · 端到端联调 |
| 第 28 周 | GitHub Actions CI/CD + CloudWatch | OIDC + ECR + ECS Rolling Update · Dashboard + X-Ray |
| 第 29–30 周 | 项目文档 + 面试准备 | 架构图 · 成本估算 · 压测报告整理 · STAR 故事 |

### 交付物

- [ ] 完整 Terraform 代码（`terraform apply` 一键起全套环境）
- [ ] GitHub Actions 全自动 CI/CD Pipeline
- [ ] CloudWatch Dashboard 截图
- [ ] X-Ray 分布式追踪链路截图
- [ ] AWS 架构图（可用于面试展示）

---

## AWS 费用规划

### 开发阶段（Sprint 1–4）：接近零费用

| 服务 | 本地替代 | 费用 |
|------|----------|------|
| PostgreSQL | Docker | $0 |
| Redis | Docker | $0 |
| RabbitMQ | Docker | $0 |
| SQS / SNS | LocalStack | $0 |
| MQTT Broker | EMQX Docker / IoT Core 免费层 | $0 |

### 部署阶段（Sprint 5）：1–2 个月

| 服务 | 预估月费（AUD） | 备注 |
|------|---------------|------|
| ECS Fargate（3 服务） | ~15–20 | 0.5 vCPU · 1GB 各服务 |
| RDS db.t3.micro | ~25–30 | Single-AZ 开发用 |
| ElastiCache t3.micro | ~15 | |
| Amazon MQ mq.t3.micro | ~20 | |
| SQS / SNS | ~1–2 | 开发量级 |
| AWS IoT Core | $0 | 免费层 250k msg/月 |
| CloudWatch | ~3–5 | |
| **合计** | **~80–95** | **2 个月约 AUD 160–190** |

> **IoT Core 免费层注意**：12 个月有效期从**账号创建日**开始计算，不是从开始使用 IoT Core 的日期。2025 年 7 月 15 日后新建账号可获最高 $200 credits。

### 节省费用的建议

1. 部署完成、截图留档后立即 `terraform destroy` 销毁环境
2. RDS 使用 Single-AZ（开发演示用，不需要 Multi-AZ）
3. ECS Task 配置最小规格（0.5 vCPU · 1GB），演示时按需启动

---

## 面试资产清单

完成全部 Sprint 后，你将拥有：

### GitHub 仓库内容

- [ ] 三个微服务独立仓库，Clean Architecture 分层
- [ ] Terraform IaC（一键部署全套 AWS 环境）
- [ ] GitHub Actions CI/CD Pipeline（OIDC + ECR + ECS）
- [ ] Docker Compose 本地开发环境
- [ ] xUnit 单元测试 + TestContainers 集成测试
- [ ] ADR 架构决策记录文档
- [ ] 完整 README（含架构图、技术选型说明）
- [ ] 压测报告（基线 → 瓶颈 → 优化 → 对比）

### 可量化的面试数据

- 遥测摄取吞吐量（压测报告，优化前后对比）
- 设备 API 并发性能（k6 报告，P50/P95/P99 延迟）
- 单元测试 + 集成测试覆盖率（目标 > 70%）
- CloudWatch Dashboard 截图（真实 AWS 监控数据）
- X-Ray 分布式追踪链路截图

### 面试时可讲的故事（STAR 格式）

1. **高并发**：通过压测发现 Channel 积压瓶颈，调整批量大小和消费者并发数，将遥测吞吐从 X 提升到 Y
2. **消息队列**：SQS + Outbox Pattern 解决 IoT 数据丢失问题的设计思路
3. **压力测试**：用 k6 + 自定义 MQTT 工具模拟 100+ 设备并发，发现并解决了 Z 问题
4. **DDD 重构**：在压测验证性能基线后，识别三个限界上下文，把单体拆成微服务
5. **AWS 部署**：Terraform IaC + ECS Fargate + GitHub Actions 全自动化的实现细节

---

## 参考资源

### 书籍

- 《实现领域驱动设计》— Vaughn Vernon（DDD 圣经）
- 《.NET 微服务：容器化 .NET 应用架构》— Microsoft 官方免费电子书

### 官方文档

- [ASP.NET Core 文档](https://docs.microsoft.com/aspnet/core)
- [AWS IoT Core 开发者指南](https://docs.aws.amazon.com/iot/latest/developerguide)
- [MassTransit 文档](https://masstransit.io/documentation)
- [Terraform AWS Provider](https://registry.terraform.io/providers/hashicorp/aws)
- [k6 文档](https://k6.io/docs/)

### 工具

- **k6**：HTTP 压力测试工具（开源，Grafana 生态）
- **LocalStack**：本地模拟 AWS 服务（`localstack/localstack` Docker 镜像）
- **TestContainers**：集成测试用真实容器（`Testcontainers` NuGet 包）
- **dotnet-trace / dotnet-counters**：.NET 性能分析工具

---

*最后更新：2026 年 5 月*
