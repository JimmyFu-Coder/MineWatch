resource "aws_sqs_queue" "dlq" {
  name                      = var.dlq_name
  message_retention_seconds = var.message_retention_seconds

  tags = merge(var.tags, { Name = "${var.name_prefix}-${var.dlq_name}" })
}

resource "aws_sqs_queue" "main" {
  name                       = var.queue_name
  visibility_timeout_seconds = var.visibility_timeout_seconds
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq.arn
    maxReceiveCount     = var.max_receive_count
  })

  tags = merge(var.tags, { Name = "${var.name_prefix}-${var.queue_name}" })
}
