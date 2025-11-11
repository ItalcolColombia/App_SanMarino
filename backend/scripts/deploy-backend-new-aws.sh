#!/bin/bash

# Script de despliegue para Backend San Marino - Nuevo AWS
# Account: 196080479890, Region: us-east-2

set -e

# ====== CONFIGURACIÓN ======
ACCOUNT_ID="196080479890"
REGION="us-east-2"
CLUSTER="devSanmarinoZoo"
SERVICE="sanmarino-back-task-service-75khncfa"
FAMILY="sanmarino-back-task"
CONTAINER="backend"
ECR_URI="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/sanmarino/zootecnia/granjas/backend"
TAG=$(date +%Y%m%d-%H%M)

echo "=================================="
echo "Despliegue Backend San Marino"
echo "=================================="
echo "Account ID: $ACCOUNT_ID"
echo "Región: $REGION"
echo "Cluster: $CLUSTER"
echo "Service: $SERVICE"
echo "Family: $FAMILY"
echo "Container: $CONTAINER"
echo "ECR URI: $ECR_URI"
echo "Tag: $TAG"
echo "=================================="
echo ""

# Función para log
log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1"
}

# 1) Login a ECR
log "1/6) Login a ECR..."
aws ecr get-login-password --region $REGION | docker login --username AWS --password-stdin $ECR_URI

# 2) Build
log "2/6) Building Docker image..."
docker build -t ${FAMILY}:${TAG} .
docker tag ${FAMILY}:${TAG} ${ECR_URI}:${TAG}
docker tag ${FAMILY}:${TAG} ${ECR_URI}:latest

# 3) Push
log "3/6) Pushing to ECR..."
docker push ${ECR_URI}:${TAG}
docker push ${ECR_URI}:latest

# 4) Obtener Task Definition actual y actualizar imagen
log "4/6) Actualizando Task Definition..."
aws ecs describe-task-definition --task-definition $FAMILY --region $REGION \
  --query 'taskDefinition.containerDefinitions' --output json > /tmp/containers.json

# Actualizar imagen en containers.json usando jq o sed
if command -v jq &> /dev/null; then
    # Si jq está disponible
    cat /tmp/containers.json | jq --arg img "${ECR_URI}:${TAG}" '.[0].image = $img' > /tmp/containers-updated.json
else
    # Fallback sin jq - usar sed
    sed "s|\"image\": \".*\"|\"image\": \"${ECR_URI}:${TAG}\"|" /tmp/containers.json > /tmp/containers-updated.json
fi

# Obtener metadata de la TD actual
aws ecs describe-task-definition --task-definition $FAMILY --region $REGION \
  --query 'taskDefinition.{cpu:cpu,memory:memory,networkMode:networkMode,taskRoleArn:taskRoleArn,executionRoleArn:executionRoleArn}' \
  --output json > /tmp/metadata.json

# Crear nueva revisión de TD
log "5/6) Registrando nueva Task Definition..."

# Construir comando para registrar nueva TD
NEW_TD_ARN=$(aws ecs register-task-definition \
  --family $FAMILY \
  --cpu 1024 \
  --memory 3072 \
  --network-mode awsvpc \
  --task-role-arn arn:aws:iam::196080479890:role/ecsTaskExecutionRole \
  --execution-role-arn arn:aws:iam::196080479890:role/ecsTaskExecutionRole \
  --container-definitions file:///tmp/containers-updated.json \
  --query 'taskDefinition.taskDefinitionArn' --output text \
  --region $REGION)

log "Nueva Task Definition: $NEW_TD_ARN"

# 6) Actualizar servicio
log "6/6) Actualizando servicio ECS..."
aws ecs update-service \
  --cluster $CLUSTER \
  --service $SERVICE \
  --task-definition $FAMILY \
  --force-new-deployment \
  --region $REGION

log "Esperando a que el servicio se estabilice..."
aws ecs wait services-stable --cluster $CLUSTER --services $SERVICE --region $REGION

log "=================================="
log "✓ Despliegue completado exitosamente"
log "=================================="

