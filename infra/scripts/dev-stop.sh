#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TF_DIR="$SCRIPT_DIR/../terraform"

echo "=== MineWatch Dev Stop ==="
echo ""

# 1. Stop Aurora
echo "[1/2] Stopping Aurora cluster..."
cd "$TF_DIR/persist"
CLUSTER_ID=$(terraform output -raw aurora_cluster_identifier 2>/dev/null || echo "")

if [ -n "$CLUSTER_ID" ]; then
    STATUS=$(aws rds describe-db-clusters \
        --db-cluster-identifier "$CLUSTER_ID" \
        --query 'DBClusters[0].Status' \
        --output text 2>/dev/null || echo "not-found")

    if [ "$STATUS" = "available" ]; then
        echo "  Stopping Aurora..."
        aws rds stop-db-cluster --db-cluster-identifier "$CLUSTER_ID" >/dev/null
        echo "  Aurora stopped."
    elif [ "$STATUS" = "stopped" ]; then
        echo "  Aurora is already stopped."
    else
        echo "  Aurora status: $STATUS. Skipping."
    fi
else
    echo "  Could not determine cluster ID. Skip."
fi

# 2. Destroy compute layer
echo "[2/2] Destroying compute layer (ALB + ECS)..."
cd "$TF_DIR/compute"
terraform destroy -auto-approve

echo ""
echo "=== MineWatch stopped ==="
echo "  Aurora:       stopped (data preserved)"
echo "  ALB + ECS:    destroyed (no cost)"
echo "  Persist layer: intact (VPC, SQS, Secrets, ECR, IAM)"
echo ""
echo "Run './dev-start.sh' to resume. Aurora will restart with your data."
