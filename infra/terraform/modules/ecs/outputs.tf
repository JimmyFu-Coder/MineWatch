output "cluster_name" {
  description = "Name of the ECS cluster"
  value       = aws_ecs_cluster.main.name
}

output "mosquitto_service_discovery_arn" {
  description = "ARN of the Mosquitto Cloud Map service discovery"
  value       = aws_service_discovery_service.mosquitto.arn
}
