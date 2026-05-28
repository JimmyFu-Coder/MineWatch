variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "ap-southeast-2"
}

variable "project_name" {
  description = "Project name used as prefix for all resources"
  type        = string
  default     = "minewatch"
}

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "prod"
}

variable "db_username" {
  description = "RDS master username"
  type        = string
  default     = "minewatch"
}

variable "db_password" {
  description = "RDS master password"
  type        = string
  sensitive   = true
}

variable "jwt_secret_key" {
  description = "JWT signing key"
  type        = string
  sensitive   = true
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