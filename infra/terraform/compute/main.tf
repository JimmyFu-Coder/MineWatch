# Read persist layer outputs
data "terraform_remote_state" "persist" {
  backend = "local"

  config = {
    path = "${path.module}/../persist/persist.tfstate"
  }
}

# --- Load Balancer ---
module "alb" {
  source = "../modules/alb"

  name_prefix       = local.name_prefix
  tags              = local.common_tags
  vpc_id            = data.terraform_remote_state.persist.outputs.vpc_id
  subnet_ids        = data.terraform_remote_state.persist.outputs.public_subnet_ids
  security_group_id = data.terraform_remote_state.persist.outputs.alb_security_group_id
}

# --- Compute ---
module "ecs" {
  source = "../modules/ecs"

  name_prefix                 = local.name_prefix
  tags                        = local.common_tags
  vpc_id                      = data.terraform_remote_state.persist.outputs.vpc_id
  public_subnet_ids           = data.terraform_remote_state.persist.outputs.public_subnet_ids
  api_security_group_id       = data.terraform_remote_state.persist.outputs.api_security_group_id
  mosquitto_security_group_id = data.terraform_remote_state.persist.outputs.mosquitto_security_group_id
  api_execution_role_arn      = data.terraform_remote_state.persist.outputs.ecs_execution_role_arn
  api_task_role_arn           = data.terraform_remote_state.persist.outputs.api_task_role_arn
  api_ecr_url                 = data.terraform_remote_state.persist.outputs.api_ecr_url
  mosquitto_ecr_url           = data.terraform_remote_state.persist.outputs.mosquitto_ecr_url
  api_image_tag               = var.api_image_tag
  mosquitto_image_tag         = var.mosquitto_image_tag
  sqs_queue_url               = data.terraform_remote_state.persist.outputs.sqs_queue_url
  sqs_dlq_url                 = data.terraform_remote_state.persist.outputs.sqs_dlq_url
  db_connection_secret_arn    = data.terraform_remote_state.persist.outputs.db_connection_secret_arn
  jwt_key_secret_arn          = data.terraform_remote_state.persist.outputs.jwt_key_secret_arn
  aws_region                  = var.aws_region
  alb_target_group_arn        = module.alb.target_group_arn
  service_discovery_namespace = "minewatch.local"
}
