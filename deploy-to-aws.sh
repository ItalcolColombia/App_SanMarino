#!/bin/bash

# ==========================================
# Script Automatizado de Despliegue Completo
# San Marino - Backend y Frontend a AWS ECS
# ==========================================

set -e

# Colores
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m' # No Color

# Obtener directorio del proyecto
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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

info() {
    echo -e "${CYAN}[INFO]${NC} $1"
}

header() {
    echo ""
    echo -e "${MAGENTA}========================================${NC}"
    echo -e "${MAGENTA}$1${NC}"
    echo -e "${MAGENTA}========================================${NC}"
    echo ""
}

# Banner
clear
echo -e "${CYAN}"
cat << "EOF"
╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║      🚀 DESPLIEGUE AUTOMATIZADO - SAN MARINO APP        ║
║                                                           ║
║           Backend + Frontend → AWS ECS                    ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
EOF
echo -e "${NC}"

# Verificaciones pre-despliegue
header "VERIFICACIÓN DE PRE-REQUISITOS"

# Verificar Docker
log "Verificando Docker..."
if ! command -v docker &> /dev/null; then
    error "Docker no está instalado. Por favor instala Docker Desktop."
fi

if ! docker info &> /dev/null; then
    error "Docker daemon no está corriendo. Por favor inicia Docker Desktop."
fi
success "Docker está instalado y corriendo"

# Verificar AWS CLI
log "Verificando AWS CLI..."
if ! command -v aws &> /dev/null; then
    error "AWS CLI no está instalado. Por favor instala AWS CLI."
fi

# Verificar credenciales AWS
log "Verificando credenciales AWS..."
if ! aws sts get-caller-identity &> /dev/null; then
    error "Credenciales AWS no configuradas. Ejecuta 'aws configure'."
fi
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
REGION=$(aws configure get region || echo "us-east-2")
success "AWS CLI configurado (Account: $ACCOUNT_ID, Region: $REGION)"

# Verificar que los scripts existen
log "Verificando scripts de despliegue..."
if [ ! -f "$PROJECT_ROOT/backend/scripts/deploy-backend-ecs.sh" ]; then
    error "No se encontró backend/scripts/deploy-backend-ecs.sh"
fi

if [ ! -f "$PROJECT_ROOT/frontend/scripts/deploy-frontend-ecs.sh" ]; then
    error "No se encontró frontend/scripts/deploy-frontend-ecs.sh"
fi
success "Scripts de despliegue encontrados"

# Hacer ejecutables los scripts
chmod +x "$PROJECT_ROOT/backend/scripts/deploy-backend-ecs.sh" 2>/dev/null || true
chmod +x "$PROJECT_ROOT/frontend/scripts/deploy-frontend-ecs.sh" 2>/dev/null || true

# Opciones de despliegue
echo ""
header "OPCIONES DE DESPLIEGUE"

echo "1) Desplegar Backend y Frontend (completo)"
echo "2) Desplegar solo Backend"
echo "3) Desplegar solo Frontend"
echo ""
read -p "Selecciona una opción (1-3): " OPTION

case $OPTION in
    1)
        DEPLOY_BACKEND=true
        DEPLOY_FRONTEND=true
        ;;
    2)
        DEPLOY_BACKEND=true
        DEPLOY_FRONTEND=false
        ;;
    3)
        DEPLOY_BACKEND=false
        DEPLOY_FRONTEND=true
        ;;
    *)
        error "Opción inválida"
        ;;
esac

# Desplegar Backend
if [ "$DEPLOY_BACKEND" = true ]; then
    header "DESPLIEGUE BACKEND"
    cd "$PROJECT_ROOT/backend"
    
    log "Ejecutando script de despliegue del backend..."
    if ./scripts/deploy-backend-ecs.sh; then
        success "Backend desplegado exitosamente"
        BACKEND_SUCCESS=true
    else
        error "Fallo en despliegue del backend"
    fi
    
    echo ""
fi

# Desplegar Frontend
if [ "$DEPLOY_FRONTEND" = true ]; then
    header "DESPLIEGUE FRONTEND"
    cd "$PROJECT_ROOT/frontend"
    
    log "Ejecutando script de despliegue del frontend..."
    if ./scripts/deploy-frontend-ecs.sh; then
        success "Frontend desplegado exitosamente"
        FRONTEND_SUCCESS=true
    else
        error "Fallo en despliegue del frontend"
    fi
    
    echo ""
fi

# Resumen final
header "RESUMEN DEL DESPLIEGUE"

if [ "$DEPLOY_BACKEND" = true ]; then
    if [ "$BACKEND_SUCCESS" = true ]; then
        echo -e "${GREEN}✅ Backend: DESPLEGADO${NC}"
    else
        echo -e "${RED}❌ Backend: FALLÓ${NC}"
    fi
fi

if [ "$DEPLOY_FRONTEND" = true ]; then
    if [ "$FRONTEND_SUCCESS" = true ]; then
        echo -e "${GREEN}✅ Frontend: DESPLEGADO${NC}"
    else
        echo -e "${RED}❌ Frontend: FALLÓ${NC}"
    fi
fi

# Obtener URLs
echo ""
header "URLs DE ACCESO"

ALB_DNS=$(aws elbv2 describe-load-balancers --region us-east-2 --query 'LoadBalancers[?contains(LoadBalancerName, `sanmarino`)].DNSName' --output text 2>/dev/null | head -1)

if [ ! -z "$ALB_DNS" ]; then
    echo -e "${CYAN}Frontend (ALB):${NC} http://${ALB_DNS}"
    echo -e "${CYAN}API (ALB):${NC}     http://${ALB_DNS}/api"
    echo -e "${CYAN}Swagger:${NC}      http://${ALB_DNS}/swagger"
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}🎉 DESPLIEGUE COMPLETADO${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""


