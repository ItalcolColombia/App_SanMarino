#!/bin/bash

# Script Automatizado de Despliegue Frontend San Marino
# Para el nuevo AWS: Account 196080479890
# Nota: este script debe estar en formato LF (no CRLF) para bash.

set -e

# Colores
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

# Configuración
ACCOUNT_ID="196080479890"
REGION="us-east-2"
CLUSTER="devSanmarinoZoo"
SERVICE="sanmarino-front-task-service-zp2f403l"
FAMILY="sanmarino-front-task"
CONTAINER="frontend"
ECR_URI="${ACCOUNT_ID}.dkr.ecr.${REGION}.amazonaws.com/sanmarino/zootecnia/granjas/frontend"
TAG=$(date +%Y%m%d-%H%M)
DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Despliegue Frontend San Marino${NC}"
echo -e "${CYAN}========================================${NC}"
echo -e "Account ID: ${GREEN}${ACCOUNT_ID}${NC}"
echo -e "Región: ${GREEN}${REGION}${NC}"
echo -e "Cluster: ${GREEN}${CLUSTER}${NC}"
echo -e "Service: ${GREEN}${SERVICE}${NC}"
echo -e "ECR URI: ${GREEN}${ECR_URI}${NC}"
echo -e "Tag: ${GREEN}${TAG}${NC}"
echo ""

log() { echo -e "[$(date +'%H:%M:%S')] $1"; }
error() { echo -e "${RED}[ERROR]${NC} $1" >&2; exit 1; }
success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }

# Verificaciones
DOCKER_CMD="docker"
if ! command -v docker &> /dev/null; then
    if command -v docker.exe &> /dev/null; then
        DOCKER_CMD="docker.exe"
    else
        error "Docker no instalado"
    fi
fi
$DOCKER_CMD info &> /dev/null || error "Docker no corriendo"

AWS_CMD="aws"
if ! command -v aws &> /dev/null; then
    if command -v aws.exe &> /dev/null; then
        AWS_CMD="aws.exe"
    else
        error "AWS CLI no instalado"
    fi
fi

cd "$DEPLOY_DIR"

# Login a ECR (igual que backend: usa ECR_URI)
log "1/7) Login a ECR..."
if ! $AWS_CMD ecr get-login-password --region $REGION | $DOCKER_CMD login --username AWS --password-stdin $ECR_URI 2>&1 | grep -q "Login Succeeded"; then
    error "Fallo en login a ECR"
fi
success "Login exitoso"

# Build y push (igual que backend: buildx --push)
log "2/7) Building imagen para linux/amd64..."
if ! $DOCKER_CMD buildx build --platform linux/amd64 --provenance=false --sbom=false -t ${ECR_URI}:${TAG} -t ${ECR_URI}:latest --push .; then
    error "Fallo en docker buildx build/push"
fi
success "Imagen pusheada"

# Preparar Task Definition
log "3/7) Actualizando Task Definition con nuevo tag..."
if [ -f "deploy/ecs-taskdef.json" ]; then
    cp deploy/ecs-taskdef.json ecs-taskdef.json
else
    error "No se encontró deploy/ecs-taskdef.json"
fi

# Actualizar el tag de la imagen en el JSON (compatible con macOS y Linux)
if [[ "$OSTYPE" == "darwin"* ]]; then
    # macOS
    sed -i '' "s|${ECR_URI}:[^\"}]*|${ECR_URI}:${TAG}|g" ecs-taskdef.json
else
    # Linux
    sed -i "s|${ECR_URI}:[^\"}]*|${ECR_URI}:${TAG}|g" ecs-taskdef.json
fi

# Registrar Task Definition
log "4/7) Registrando Task Definition..."
NEW_TD_ARN=$($AWS_CMD ecs register-task-definition --cli-input-json file://ecs-taskdef.json --query 'taskDefinition.taskDefinitionArn' --output text --region $REGION | tr -d '\r')
[ -z "$NEW_TD_ARN" ] && error "Error registrando Task Definition"
success "TD registrada: $NEW_TD_ARN"

# Actualizar servicio
log "5/7) Actualizando servicio..."
$AWS_CMD ecs update-service --cluster $CLUSTER --service $SERVICE --task-definition $NEW_TD_ARN --force-new-deployment --region $REGION > /dev/null
success "Servicio actualizado"

# Esperar
log "6/7) Esperando estabilización (2-3 min)..."
$AWS_CMD ecs wait services-stable --cluster $CLUSTER --services $SERVICE --region $REGION

# Verificar
log "7/7) Verificando..."
sleep 10
RUNNING=$($AWS_CMD ecs describe-services --cluster $CLUSTER --services $SERVICE --region $REGION --query 'services[0].runningCount' --output text | tr -d '\r')
[ "$RUNNING" -eq "0" ] && error "Servicio no corriendo"

success "Servicio corriendo ($RUNNING tarea)"

# Obtener IP
TASK_ARN=$($AWS_CMD ecs list-tasks --cluster $CLUSTER --service-name $SERVICE --region $REGION --desired-status RUNNING --query 'taskArns[0]' --output text | tr -d '\r')
ENI_ID=$($AWS_CMD ecs describe-tasks --cluster $CLUSTER --tasks $TASK_ARN --region $REGION --query 'tasks[0].attachments[0].details[?name==`networkInterfaceId`].value' --output text | tr -d '\r')
PUBLIC_IP=$($AWS_CMD ec2 describe-network-interfaces --network-interface-ids $ENI_ID --region $REGION --query 'NetworkInterfaces[0].Association.PublicIp' --output text | tr -d '\r')

# Obtener ALB DNS
ALB_DNS=$($AWS_CMD elbv2 describe-load-balancers --region $REGION --query 'LoadBalancers[?contains(LoadBalancerName, `sanmarino`)].DNSName' --output text | head -1 | tr -d '\r')

echo ""
echo -e "${GREEN}✓ Despliegue exitoso${NC}"
echo -e "${CYAN}========================================${NC}"
echo -e "${GREEN}URLs de Acceso:${NC}"
echo -e "ALB (Recomendado): ${CYAN}http://${ALB_DNS}/${NC}"
if [ ! -z "$PUBLIC_IP" ]; then
    echo -e "IP Directa: ${CYAN}http://${PUBLIC_IP}/${NC}"
fi
echo -e "${CYAN}========================================${NC}"
echo -e "Imagen: ${CYAN}${ECR_URI}:${TAG}${NC}"
echo -e "API: ${CYAN}http://${ALB_DNS}/api${NC}"
echo ""
