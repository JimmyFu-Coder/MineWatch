output "alb_dns_name" {
  description = "DNS name of the ALB (API endpoint)"
  value       = module.alb.dns_name
}

output "api_ecr_repository_url" {
  description = "ECR repository URL for the API image"
  value       = data.terraform_remote_state.persist.outputs.api_ecr_url
}

output "mosquitto_ecr_repository_url" {
  description = "ECR repository URL for the Mosquitto image"
  value       = data.terraform_remote_state.persist.outputs.mosquitto_ecr_url
}

output "rds_endpoint" {
  description = "Aurora Serverless v2 PostgreSQL endpoint"
  value       = data.terraform_remote_state.persist.outputs.rds_endpoint
}

output "sqs_queue_url" {
  description = "URL of the main SQS queue"
  value       = data.terraform_remote_state.persist.outputs.sqs_queue_url
}
