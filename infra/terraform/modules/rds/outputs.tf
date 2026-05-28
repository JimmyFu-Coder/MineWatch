output "endpoint" {
  description = "RDS instance endpoint (host:port)"
  value       = aws_db_instance.postgres.endpoint
}

output "db_name" {
  description = "Name of the default database"
  value       = aws_db_instance.postgres.db_name
}

output "connection_string" {
  description = "Full Npgsql connection string"
  value       = "Host=${aws_db_instance.postgres.endpoint};Database=${var.db_name};Username=${var.username};Password=${var.password}"
  sensitive   = true
}
