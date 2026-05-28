output "alb_dns_name" {
  description = "DNS name of the ALB (API endpoint)"
  value       = module.alb.dns_name
}

output "api_ecr_repository_url" {
  description = "ECR repository URL for the API image"
  value       = module.ecr_api.repository_url
}

output "mosquitto_ecr_repository_url" {
  description = "ECR repository URL for the Mosquitto image"
  value       = module.ecr_mosquitto.repository_url
}

output "rds_endpoint" {
  description = "RDS PostgreSQL endpoint"
  value       = module.rds.endpoint
}

output "sqs_queue_url" {
  description = "URL of the main SQS queue"
  value       = module.sqs.queue_url
}