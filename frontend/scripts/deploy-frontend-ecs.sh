#!/bin/bash

# Script Automatizado de Despliegue Frontend San Marino
# Para el nuevo AWS: Account 196080479890

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
[ ! -x "$(command -v docker)" ] && error "Docker no instalado"
docker info &> /dev/null || error "Docker no corriendo"
[ ! -x "$(command -v aws)" ] && error "AWS CLI no instalado"

cd "$DEPLOY_DIR"

# Login a ECR
log "1/6) Login a ECR..."
aws ecr get-login-password --region $REGION | docker login --username AWS --password-stdin $ECR_URI > /dev/null
success "Login exitoso"

# Build
log "2/6) Building imagen para linux/amd64..."
docker buildx build --platform linux/amd64 -t ${ECR_URI}:${TAG} -t ${ECR_URI}:latest --push . > /dev/null
success "Imagen pusheada"

# Registrar Task Definition
log "3/6) Registrando Task Definition..."
NEW_TD_ARN=$(aws ecs register-task-definition --cli-input-json file://ecs-taskdef.json --query 'taskDefinition.taskDefinitionArn' --output text --region $REGION)
[ -z "$NEW_TD_ARN" ] && error "Error registrando Task Definition"
success "TD registrada: $NEW_TD_ARN"

# Actualizar servicio
log "4/6) Actualizando servicio..."
aws ecs update-service --cluster $CLUSTER --service $SERVICE --task-definition $NEW_TD_ARN --force-new-deployment --region $REGION > /dev/null
success "Servicio actualizado"

# Esperar
log "5/6) Esperando estabilización (2-3 min)..."
aws ecs wait services-stable --cluster $CLUSTER --services $SERVICE --region $REGION

# Verificar
log "6/6) Verificando..."
sleep 10
RUNNING=$(aws ecs describe-services --cluster $CLUSTER --services $SERVICE --region $REGION --query 'services[0].runningCount' --output text)
[ "$RUNNING" -eq "0" ] && error "Servicio no corriendo"

success "Servicio corriendo ($RUNNING tarea)"

# Obtener IP
TASK_ARN=$(aws ecs list-tasks --cluster $CLUSTER --service-name $SERVICE --region $REGION --desired-status RUNNING --query 'taskArns[0]' --output text)
ENI_ID=$(aws ecs describe-tasks --cluster $CLUSTER --tasks $TASK_ARN --region $REGION --query 'tasks[0].attachments[0].details[?name==`networkInterfaceId`].value' --output text)
PUBLIC_IP=$(aws ec2 describe-network-interfaces --network-interface-ids $ENI_ID --region $REGION --query 'NetworkInterfaces[0].Association.PublicIp' --output text)

# Obtener ALB DNS
ALB_DNS=$(aws elbv2 describe-load-balancers --region $REGION --query 'LoadBalancers[?contains(LoadBalancerName, `sanmarino`)].DNSName' --output text | head -1)

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
