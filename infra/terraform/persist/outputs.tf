# --- Outputs consumed by compute layer ---
# These are read by compute via terraform_remote_state

output "vpc_id" {
  value = module.vpc.vpc_id
}

output "public_subnet_ids" {
  value = module.vpc.public_subnet_ids
}

output "private_subnet_ids" {
  value = module.vpc.private_subnet_ids
}

output "api_security_group_id" {
  value = aws_security_group.api.id
}

output "mosquitto_security_group_id" {
  value = aws_security_group.mosquitto.id
}

output "alb_security_group_id" {
  value = aws_security_group.alb.id
}

output "api_ecr_url" {
  value = module.ecr_api.repository_url
}

output "mosquitto_ecr_url" {
  value = module.ecr_mosquitto.repository_url
}

output "sqs_queue_url" {
  value = module.sqs.queue_url
}

output "sqs_dlq_url" {
  value = module.sqs.dlq_url
}

output "db_connection_secret_arn" {
  value = module.secrets.db_connection_secret_arn
}

output "jwt_key_secret_arn" {
  value = module.secrets.jwt_key_secret_arn
}

output "ecs_execution_role_arn" {
  value = module.iam.ecs_execution_role_arn
}

output "api_task_role_arn" {
  value = module.iam.api_task_role_arn
}

output "aurora_cluster_identifier" {
  value = "${local.name_prefix}-aurora"
}

output "rds_endpoint" {
  description = "Aurora Serverless v2 PostgreSQL endpoint"
  value       = module.rds.endpoint
}
