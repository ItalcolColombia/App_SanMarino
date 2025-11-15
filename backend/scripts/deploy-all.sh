#!/bin/bash

# Script para desplegar Backend y Frontend a ECS
# ================================================

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}Despliegue Completo - Backend y Frontend${NC}"
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

# Verificaciones
if ! command -v docker &> /dev/null; then
    error "Docker no está instalado"
fi

if ! docker info &> /dev/null; then
    error "Docker daemon no está corriendo. Por favor inicia Docker Desktop."
fi

if ! command -v aws &> /dev/null; then
    error "AWS CLI no está instalado"
fi

# Desplegar Backend
echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}PASO 1: Desplegando Backend${NC}"
echo -e "${CYAN}========================================${NC}"
cd "$PROJECT_ROOT/backend"
if [ -f "scripts/deploy-backend-ecs.sh" ]; then
    chmod +x scripts/deploy-backend-ecs.sh
    ./scripts/deploy-backend-ecs.sh
    success "Backend desplegado exitosamente"
else
    error "No se encontró el script de despliegue del backend"
fi

echo ""
echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}PASO 2: Desplegando Frontend${NC}"
echo -e "${CYAN}========================================${NC}"
cd "$PROJECT_ROOT/frontend"
if [ -f "scripts/deploy-frontend-ecs.sh" ]; then
    chmod +x scripts/deploy-frontend-ecs.sh
    ./scripts/deploy-frontend-ecs.sh
    success "Frontend desplegado exitosamente"
else
    error "No se encontró el script de despliegue del frontend"
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}✓ Despliegue completo exitoso${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Backend y Frontend han sido desplegados exitosamente."



