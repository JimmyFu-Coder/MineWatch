# --- Networking ---
module "vpc" {
  source = "./modules/vpc"

  name_prefix = local.name_prefix
  azs         = local.azs
  tags        = local.common_tags
}

# --- Security Groups (cross-module, kept at root level) ---
resource "aws_security_group" "alb" {
  name_prefix = "${local.name_prefix}-alb-"
  vpc_id      = module.vpc.vpc_id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, { Name = "${local.name_prefix}-alb-sg" })
}

resource "aws_security_group" "api" {
  name_prefix = "${local.name_prefix}-api-"
  vpc_id      = module.vpc.vpc_id

  ingress {
    from_port       = 5211
    to_port         = 5211
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, { Name = "${local.name_prefix}-api-sg" })
}

resource "aws_security_group" "mosquitto" {
  name_prefix = "${local.name_prefix}-mosquitto-"
  vpc_id      = module.vpc.vpc_id

  ingress {
    from_port       = 1883
    to_port         = 1883
    protocol        = "tcp"
    security_groups = [aws_security_group.api.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, { Name = "${local.name_prefix}-mosquitto-sg" })
}

resource "aws_security_group" "rds" {
  name_prefix = "${local.name_prefix}-rds-"
  vpc_id      = module.vpc.vpc_id

  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.api.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(local.common_tags, { Name = "${local.name_prefix}-rds-sg" })
}

# --- Storage ---
module "ecr_api" {
  source = "./modules/ecr"

  name_prefix    = local.name_prefix
  repository_name = "minewatch-api"
  tags           = local.common_tags
}

module "ecr_mosquitto" {
  source = "./modules/ecr"

  name_prefix    = local.name_prefix
  repository_name = "minewatch-mosquitto"
  tags           = local.common_tags
}

# --- Database ---
module "rds" {
  source = "./modules/rds"

  name_prefix        = local.name_prefix
  tags               = local.common_tags
  subnet_ids         = module.vpc.private_subnet_ids
  security_group_ids = [aws_security_group.rds.id]
  username           = var.db_username
  password           = var.db_password
}

# --- Messaging ---
module "sqs" {
  source = "./modules/sqs"

  name_prefix = local.name_prefix
  tags        = local.common_tags
  queue_name  = "minewatch-telemetry"
  dlq_name    = "minewatch-telemetry-dlq"
}

# --- Secrets ---
module "secrets" {
  source = "./modules/secrets"

  name_prefix          = local.name_prefix
  tags                 = local.common_tags
  db_connection_string = module.rds.connection_string
  jwt_secret_key       = var.jwt_secret_key
}

# --- IAM ---
module "iam" {
  source = "./modules/iam"

  name_prefix   = local.name_prefix
  tags          = local.common_tags
  sqs_queue_arn = module.sqs.queue_arn
  sqs_dlq_arn   = module.sqs.dlq_arn
  secrets_arns  = [module.secrets.db_connection_secret_arn, module.secrets.jwt_key_secret_arn]
}

# --- Load Balancer ---
module "alb" {
  source = "./modules/alb"

  name_prefix       = local.name_prefix
  tags              = local.common_tags
  vpc_id            = module.vpc.vpc_id
  subnet_ids        = module.vpc.public_subnet_ids
  security_group_id = aws_security_group.alb.id
}

# --- Compute ---
module "ecs" {
  source = "./modules/ecs"

  name_prefix                 = local.name_prefix
  tags                        = local.common_tags
  vpc_id                      = module.vpc.vpc_id
  public_subnet_ids           = module.vpc.public_subnet_ids
  api_security_group_id       = aws_security_group.api.id
  mosquitto_security_group_id = aws_security_group.mosquitto.id
  api_execution_role_arn      = module.iam.ecs_execution_role_arn
  api_task_role_arn           = module.iam.api_task_role_arn
  api_ecr_url                 = module.ecr_api.repository_url
  mosquitto_ecr_url           = module.ecr_mosquitto.repository_url
  api_image_tag               = var.api_image_tag
  mosquitto_image_tag         = var.mosquitto_image_tag
  sqs_queue_url               = module.sqs.queue_url
  sqs_dlq_url                 = module.sqs.dlq_url
  db_connection_secret_arn    = module.secrets.db_connection_secret_arn
  jwt_key_secret_arn          = module.secrets.jwt_key_secret_arn
  aws_region                  = var.aws_region
  alb_target_group_arn        = module.alb.target_group_arn
  service_discovery_namespace = "minewatch.local"
}