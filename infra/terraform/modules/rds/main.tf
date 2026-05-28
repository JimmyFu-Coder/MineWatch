resource "aws_db_subnet_group" "main" {
  name       = "${var.name_prefix}-db-subnet"
  subnet_ids = var.subnet_ids

  tags = merge(var.tags, { Name = "${var.name_prefix}-db-subnet-group" })
}

resource "aws_rds_cluster" "aurora" {
  cluster_identifier              = "${var.name_prefix}-aurora"
  engine                          = "aurora-postgresql"
  engine_version                  = "16.6"
  database_name                   = var.db_name
  master_username                 = var.username
  master_password                 = var.password
  manage_master_user_password     = false

  vpc_security_group_ids          = var.security_group_ids
  db_subnet_group_name            = aws_db_subnet_group.main.name

  storage_encrypted               = true
  deletion_protection             = true
  skip_final_snapshot             = false
  final_snapshot_identifier       = "${var.name_prefix}-final-snapshot"

  backup_retention_period         = var.backup_retention_period

  serverlessv2_scaling_configuration {
    min_capacity = var.min_capacity
    max_capacity = var.max_capacity
  }

  tags = merge(var.tags, { Name = "${var.name_prefix}-aurora-cluster" })
}

resource "aws_rds_cluster_instance" "main" {
  cluster_identifier  = aws_rds_cluster.aurora.id
  identifier          = "${var.name_prefix}-aurora-instance"
  instance_class      = "db.serverless"
  engine              = aws_rds_cluster.aurora.engine
  engine_version      = aws_rds_cluster.aurora.engine_version
  publicly_accessible = false

  tags = merge(var.tags, { Name = "${var.name_prefix}-aurora-instance" })
}
