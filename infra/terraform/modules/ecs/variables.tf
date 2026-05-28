variable "name_prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}

variable "vpc_id" {
  description = "VPC ID for service discovery namespace"
  type        = string
}

variable "service_discovery_namespace" {
  description = "Private DNS namespace for service discovery (e.g. minewatch.local)"
  type        = string
}

variable "mosquitto_dns_name" {
  description = "DNS record name for the Mosquitto service (e.g. mosquitto)"
  type        = string
  default     = "mosquitto"
}

variable "public_subnet_ids" {
  description = "Public subnet IDs for ECS tasks"
  type        = list(string)
}

variable "api_security_group_id" {
  description = "Security group ID for the API task"
  type        = string
}

variable "mosquitto_security_group_id" {
  description = "Security group ID for the Mosquitto task"
  type        = string
}

variable "api_execution_role_arn" {
  description = "IAM execution role ARN for both ECS tasks (pull images, write logs)"
  type        = string
}

variable "api_task_role_arn" {
  description = "IAM task role ARN for the API container (SQS permissions)"
  type        = string
}

variable "api_ecr_url" {
  description = "ECR repository URL for the API image"
  type        = string
}

variable "mosquitto_ecr_url" {
  description = "ECR repository URL for the Mosquitto image"
  type        = string
}

variable "api_image_tag" {
  description = "Docker image tag for the API"
  type        = string
  default     = "latest"
}

variable "mosquitto_image_tag" {
  description = "Docker image tag for Mosquitto"
  type        = string
  default     = "latest"
}

variable "sqs_queue_url" {
  description = "URL of the main SQS queue"
  type        = string
}

variable "sqs_dlq_url" {
  description = "URL of the SQS dead-letter queue"
  type        = string
}

variable "db_connection_secret_arn" {
  description = "ARN of the Secrets Manager secret containing the DB connection string"
  type        = string
}

variable "jwt_key_secret_arn" {
  description = "ARN of the Secrets Manager secret containing the JWT signing key"
  type        = string
}

variable "aws_region" {
  description = "AWS region for CloudWatch log groups"
  type        = string
}

variable "alb_target_group_arn" {
  description = "ARN of the ALB target group for the API"
  type        = string
}
