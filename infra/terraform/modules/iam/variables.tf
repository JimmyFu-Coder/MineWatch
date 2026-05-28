variable "name_prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}

variable "sqs_queue_arn" {
  description = "ARN of the main SQS queue for the API task policy"
  type        = string
}

variable "sqs_dlq_arn" {
  description = "ARN of the SQS dead-letter queue for the API task policy"
  type        = string
}

variable "secrets_arns" {
  description = "List of Secrets Manager secret ARNs the execution role needs to read"
  type        = list(string)
}