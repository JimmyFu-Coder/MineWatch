# Infrastructure

AWS infrastructure managed by Terraform, container images built with Docker.

## Architecture

```
Internet → ALB (port 80) → ECS Fargate (API) → RDS PostgreSQL
                                     ↕
                             ECS Fargate (Mosquitto MQTT broker)
                                     ↕
                                 AWS SQS (+ DLQ)
```

All sensitive values (DB credentials, JWT key) are stored in Secrets Manager and injected into ECS tasks at startup.

## Directory Structure

```
infra/
├── terraform/          # AWS infrastructure as code
│   ├── backend.tf      # Terraform state configuration
│   ├── providers.tf    # AWS provider
│   ├── variables.tf    # Input variables
│   ├── locals.tf       # Computed values
│   ├── vpc.tf          # VPC, subnets, routing
│   ├── security_groups.tf
│   ├── rds.tf          # PostgreSQL
│   ├── sqs.tf          # Message queues
│   ├── ecr.tf          # Container registries
│   ├── ecs.tf          # ECS cluster, tasks, services
│   ├── alb.tf          # Load balancer
│   ├── iam.tf          # Roles and policies
│   ├── secrets.tf      # Secrets Manager
│   ├── outputs.tf      # Useful outputs
│   └── terraform.tfvars # Variable values (gitignored)
├── docker/
│   └── mosquitto/      # Custom Mosquitto image
└── README.md
```

## Prerequisites

- Terraform >= 1.5
- AWS CLI configured with appropriate credentials
- Docker

## Deploy

```bash
cd infra/terraform

# Initialize
terraform init

# Review plan
terraform plan

# Apply
terraform apply
```

After infrastructure is ready, build and push images:

```bash
# API
docker build -t minewatch-api ../../ -f ../../src/MineWatch.Api/Dockerfile
docker tag minewatch-api:latest <ecr-uri>/minewatch-api:latest
docker push <ecr-uri>/minewatch-api:latest

# Mosquitto
docker build -t minewatch-mosquitto ../docker/mosquitto/
docker tag minewatch-mosquitto:latest <ecr-uri>/minewatch-mosquitto:latest
docker push <ecr-uri>/minewatch-mosquitto:latest
```

## Estimated Cost

~$54/month in ap-southeast-2 (Sydney) with Fargate Spot.
