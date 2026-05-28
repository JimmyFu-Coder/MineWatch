resource "aws_secretsmanager_secret" "db_connection" {
  name                    = "${var.name_prefix}-db-connection"
  recovery_window_in_days = 7

  tags = merge(var.tags, { Name = "${var.name_prefix}-db-connection-secret" })
}

resource "aws_secretsmanager_secret_version" "db_connection" {
  secret_id     = aws_secretsmanager_secret.db_connection.id
  secret_string = var.db_connection_string
}

resource "aws_secretsmanager_secret" "jwt_key" {
  name                    = "${var.name_prefix}-jwt-key"
  recovery_window_in_days = 7

  tags = merge(var.tags, { Name = "${var.name_prefix}-jwt-key-secret" })
}

resource "aws_secretsmanager_secret_version" "jwt_key" {
  secret_id     = aws_secretsmanager_secret.jwt_key.id
  secret_string = var.jwt_secret_key
}