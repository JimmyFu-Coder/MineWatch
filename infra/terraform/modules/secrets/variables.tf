variable "name_prefix" {
  description = "Resource name prefix"
  type        = string
}

variable "tags" {
  description = "Common tags applied to all resources"
  type        = map(string)
  default     = {}
}

variable "db_connection_string" {
  description = "PostgreSQL connection string to store in Secrets Manager"
  type        = string
  sensitive   = true
}

variable "jwt_secret_key" {
  description = "JWT signing key to store in Secrets Manager"
  type        = string
  sensitive   = true
}