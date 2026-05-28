resource "aws_ecs_cluster" "main" {
  name = "${var.name_prefix}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = merge(var.tags, { Name = "${var.name_prefix}-cluster" })
}

resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${var.name_prefix}-api"
  retention_in_days = 30

  tags = merge(var.tags, { Name = "${var.name_prefix}-api-logs" })
}

resource "aws_cloudwatch_log_group" "mosquitto" {
  name              = "/ecs/${var.name_prefix}-mosquitto"
  retention_in_days = 30

  tags = merge(var.tags, { Name = "${var.name_prefix}-mosquitto-logs" })
}

resource "aws_service_discovery_private_dns_namespace" "main" {
  name = var.service_discovery_namespace
  vpc  = var.vpc_id

  tags = merge(var.tags, { Name = "${var.name_prefix}-sd-namespace" })
}

resource "aws_service_discovery_service" "mosquitto" {
  name = var.mosquitto_dns_name

  dns_config {
    namespace_id = aws_service_discovery_private_dns_namespace.main.id

    dns_records {
      type = "A"
      ttl  = 10
    }
  }

  health_check_custom_config {
    failure_threshold = 1
  }
}

# --- Mosquitto ---

resource "aws_ecs_task_definition" "mosquitto" {
  family                   = "${var.name_prefix}-mosquitto"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = 256
  memory                   = 512
  execution_role_arn       = var.api_execution_role_arn

  container_definitions = jsonencode([
    {
      name      = "mosquitto"
      image     = "${var.mosquitto_ecr_url}:${var.mosquitto_image_tag}"
      essential = true
      portMappings = [{
        containerPort = 1883
        protocol      = "tcp"
      }]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.mosquitto.name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "mosquitto"
        }
      }
    }
  ])

  tags = merge(var.tags, { Name = "${var.name_prefix}-mosquitto-task" })
}

resource "aws_ecs_service" "mosquitto" {
  name            = "${var.name_prefix}-mosquitto"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.mosquitto.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.public_subnet_ids
    security_groups  = [var.mosquitto_security_group_id]
    assign_public_ip = true
  }

  service_registries {
    registry_arn = aws_service_discovery_service.mosquitto.arn
  }

  # Ensure cluster is ready before deploying service
  depends_on = [aws_ecs_cluster.main]
}

# --- API ---

resource "aws_ecs_task_definition" "api" {
  family                   = "${var.name_prefix}-api"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = 256
  memory                   = 512
  task_role_arn            = var.api_task_role_arn
  execution_role_arn       = var.api_execution_role_arn

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = "${var.api_ecr_url}:${var.api_image_tag}"
      essential = true
      portMappings = [{
        containerPort = 5211
        protocol      = "tcp"
      }]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_URLS", value = "http://+:5211" },
        { name = "AWS_REGION", value = var.aws_region },
        { name = "Mqtt__Server", value = "${var.mosquitto_dns_name}.${var.service_discovery_namespace}" },
        { name = "Mqtt__Port", value = "1883" },
        { name = "Sqs__QueueUrl", value = var.sqs_queue_url },
        { name = "Sqs__DlqUrl", value = var.sqs_dlq_url },
      ]

      secrets = [
        {
          name      = "ConnectionStrings__DefaultConnection"
          valueFrom = var.db_connection_secret_arn
        },
        {
          name      = "Jwt__Key"
          valueFrom = var.jwt_key_secret_arn
        },
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.api.name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "api"
        }
      }
    }
  ])

  tags = merge(var.tags, { Name = "${var.name_prefix}-api-task" })
}

resource "aws_ecs_service" "api" {
  name            = "${var.name_prefix}-api"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.public_subnet_ids
    security_groups  = [var.api_security_group_id]
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = var.alb_target_group_arn
    container_name   = "api"
    container_port   = 5211
  }

  health_check_grace_period_seconds = 60

  depends_on = [aws_ecs_cluster.main]
}
