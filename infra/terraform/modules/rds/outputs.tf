output "endpoint" {
  description = "Aurora cluster writer endpoint (host:port)"
  value       = aws_rds_cluster.aurora.endpoint
}

output "db_name" {
  description = "Name of the default database"
  value       = aws_rds_cluster.aurora.database_name
}

output "connection_string" {
  description = "Full Npgsql connection string"
  value       = "Host=${aws_rds_cluster.aurora.endpoint};Database=${aws_rds_cluster.aurora.database_name};Username=${var.username};Password=${var.password}"
  sensitive   = true
}
