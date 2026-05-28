#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TF_DIR="$SCRIPT_DIR/../terraform"

echo "=== MineWatch Dev Start ==="
echo ""

# 1. Ensure persist layer exists
if [ ! -f "$TF_DIR/persist/persist.tfstate" ]; then
    echo "[1/3] Persist layer not found. Running terraform apply..."
    cd "$TF_DIR/persist"
    terraform init
    terraform apply -auto-approve
else
    echo "[1/3] Persist layer exists. Skipping."
fi

# 2. Start Aurora if stopped
echo "[2/3] Starting Aurora cluster..."
cd "$TF_DIR/persist"
CLUSTER_ID=$(terraform output -raw aurora_cluster_identifier 2>/dev/null || echo "")

if [ -n "$CLUSTER_ID" ]; then
    STATUS=$(aws rds describe-db-clusters \
        --db-cluster-identifier "$CLUSTER_ID" \
        --query 'DBClusters[0].Status' \
        --output text 2>/dev/null || echo "not-found")

    if [ "$STATUS" = "stopped" ]; then
        echo "  Aurora is stopped. Starting..."
        aws rds start-db-cluster --db-cluster-identifier "$CLUSTER_ID" >/dev/null
        echo "  Waiting for Aurora to become available..."
        aws rds wait db-cluster-available --db-cluster-identifier "$CLUSTER_ID"
        echo "  Aurora is running."
    elif [ "$STATUS" = "available" ]; then
        echo "  Aurora is already running."
    else
        echo "  Aurora status: $STATUS"
    fi
else
    echo "  Could not determine cluster ID. Skip."
fi

# 3. Apply compute layer
echo "[3/3] Applying compute layer (ALB + ECS)..."
cd "$TF_DIR/compute"
terraform init
terraform apply -auto-approve

# Done
ALB_DNS=$(terraform output -raw alb_dns_name 2>/dev/null || echo "N/A")
echo ""
echo "=== MineWatch is running ==="
echo "  API endpoint: http://$ALB_DNS"
echo "  Aurora:       running"
echo ""
echo "Run './dev-stop.sh' when done to shut down."
