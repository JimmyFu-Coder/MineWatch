output "db_connection_secret_arn" {
  description = "ARN of the DB connection string secret"
  value       = aws_secretsmanager_secret.db_connection.arn
}

output "jwt_key_secret_arn" {
  description = "ARN of the JWT key secret"
  value       = aws_secretsmanager_secret.jwt_key.arn
}