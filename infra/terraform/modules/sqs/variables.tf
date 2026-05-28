variable "name_prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}

variable "queue_name" {
  description = "Name of the main SQS queue"
  type        = string
}

variable "dlq_name" {
  description = "Name of the dead-letter queue"
  type        = string
}

variable "visibility_timeout_seconds" {
  description = "Visibility timeout for the main queue"
  type        = number
  default     = 30
}

variable "max_receive_count" {
  description = "Number of receives before a message is moved to the DLQ"
  type        = number
  default     = 3
}

variable "message_retention_seconds" {
  description = "Message retention period for the DLQ in seconds"
  type        = number
  default     = 1209600
}
