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
