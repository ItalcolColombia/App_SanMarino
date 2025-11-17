#!/bin/bash

# Script Automatizado de Despliegue Backend San Marino
# Para el nuevo AWS: Account 196080479890
# Configuración validada y funcionando

set -e

# Colores para output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# ====== CONFIGURACIÓN ======
ACCOUNT_ID="196080479890"
REGION="us-east-2"
CLUSTER="devSanmarinoZoo"
SERVICE="sanmarino-back-task-service-75khncfa"
FAMILY="sanmarino-back-task"
CONTAINER="backend"
ECR_URI="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/sanmarino/zootecnia/granjas/backend"
TAG=$(date +%Y%m%d-%H%M)
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Despliegue Backend San Marino${NC}"
echo -e "${CYAN}========================================${NC}"
echo -e "Account ID: ${GREEN}${ACCOUNT_ID}${NC}"
echo -e "Región: ${GREEN}${REGION}${NC}"
echo -e "Cluster: ${GREEN}${CLUSTER}${NC}"
echo -e "Service: ${GREEN}${SERVICE}${NC}"
echo -e "ECR URI: ${GREEN}${ECR_URI}${NC}"
echo -e "Tag: ${GREEN}${TAG}${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""

# Función para log
log() {
    echo -e "[$(date +'%Y-%m-%d %H:%M:%S')] $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
    exit 1
}

success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

# Verificar Docker
if ! command -v docker &> /dev/null; then
    error "Docker no está instalado o no está en el PATH"
fi

if ! docker info &> /dev/null; then
    error "Docker daemon no está corriendo. Por favor inicia Docker Desktop."
fi

# Verificar AWS CLI
if ! command -v aws &> /dev/null; then
    error "AWS CLI no está instalado"
fi

# 1) Login a ECR
log "1/7) Login a ECR..."
if ! aws ecr get-login-password --region $REGION | docker login --username AWS --password-stdin $ECR_URI 2>&1 | grep -q "Login Succeeded"; then
    error "Fallo en login a ECR"
fi
success "Login exitoso a ECR"

# 2) Build con buildx para linux/amd64
log "2/7) Building Docker image para linux/amd64..."
cd "$DEPLOY_DIR"

if ! docker buildx build --platform linux/amd64 -t ${ECR_URI}:${TAG} -t ${ECR_URI}:latest --push . 2>&1 | grep -q "pushing manifest"; then
    error "Fallo en docker buildx build"
fi
success "Imagen construida y pusheada"

# 3) Actualizar Task Definition
log "3/7) Actualizando Task Definition..."
if [ ! -f "$DEPLOY_DIR/ecs-taskdef-new-aws.json" ]; then
    error "No se encontró ecs-taskdef-new-aws.json"
fi

# Actualizar el tag en el JSON
sed -i.bak "s/\\(.*\"image\":.*backend:\\)[^\\\"]*\\(.*\\)/\\1${TAG}\\2/" "$DEPLOY_DIR/ecs-taskdef-new-aws.json"

# 4) Registrar Task Definition
log "4/7) Registrando Task Definition..."
NEW_TD_ARN=$(aws ecs register-task-definition \
    --cli-input-json file://ecs-taskdef-new-aws.json \
    --query 'taskDefinition.taskDefinitionArn' \
    --output text \
    --region $REGION 2>&1)

if [ -z "$NEW_TD_ARN" ]; then
    error "Error registrando Task Definition"
fi
success "Task Definition registrada: ${NEW_TD_ARN}"

# Restaurar backup
if [ -f "$DEPLOY_DIR/ecs-taskdef-new-aws.json.bak" ]; then
    mv "$DEPLOY_DIR/ecs-taskdef-new-aws.json.bak" "$DEPLOY_DIR/ecs-taskdef-new-aws.json"
fi

# 5) Actualizar servicio
log "5/7) Actualizando servicio ECS..."
if ! aws ecs update-service \
    --cluster $CLUSTER \
    --service $SERVICE \
    --task-definition $NEW_TD_ARN \
    --force-new-deployment \
    --region $REGION \
    --query 'service.serviceArn' \
    --output text &> /dev/null; then
    error "Fallo actualizando el servicio"
fi
success "Servicio actualizado"

# 6) Esperar estabilización
log "6/7) Esperando a que el servicio se estabilice..."
log "Esto puede tomar 2-3 minutos..."
aws ecs wait services-stable --cluster $CLUSTER --services $SERVICE --region $REGION 2>&1 || warning "El servicio puede tardar más en estabilizarse"

# 7) Verificación
log "7/7) Verificación..."
sleep 10

RUNNING_COUNT=$(aws ecs describe-services \
    --cluster $CLUSTER \
    --services $SERVICE \
    --region $REGION \
    --query 'services[0].runningCount' \
    --output text)

if [ "$RUNNING_COUNT" -eq "0" ]; then
    error "El servicio no está corriendo. Revisa los logs."
fi

success "Servicio corriendo con ${RUNNING_COUNT} tarea(s)"

# Obtener IP pública
log "Obteniendo IP pública..."
TASK_ARN=$(aws ecs list-tasks \
    --cluster $CLUSTER \
    --service-name $SERVICE \
    --region $REGION \
    --desired-status RUNNING \
    --query 'taskArns[0]' \
    --output text)

ENI_ID=$(aws ecs describe-tasks \
    --cluster $CLUSTER \
    --tasks $TASK_ARN \
    --region $REGION \
    --query 'tasks[0].attachments[0].details[?name==`networkInterfaceId`].value' \
    --output text)

PUBLIC_IP=$(aws ec2 describe-network-interfaces \
    --network-interface-ids $ENI_ID \
    --region $REGION \
    --query 'NetworkInterfaces[0].Association.PublicIp' \
    --output text)

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✓ Despliegue completado exitosamente${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "Imagen desplegada: ${CYAN}${ECR_URI}:${TAG}${NC}"
echo -e "IP Pública: ${CYAN}http://${PUBLIC_IP}:5002${NC}"
echo -e "Health Check: ${CYAN}http://${PUBLIC_IP}:5002/health${NC}"
echo -e "Swagger: ${CYAN}http://${PUBLIC_IP}:5002/swagger${NC}"
echo ""
echo -e "${YELLOW}Nota: La IP puede cambiar si la tarea se reinicia.${NC}"
echo ""

