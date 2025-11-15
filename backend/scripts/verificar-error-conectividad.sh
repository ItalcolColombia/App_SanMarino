#!/bin/bash

# Script para verificar el error de conectividad Backend-RDS
# Ejecutar desde la consola o terminal con credenciales AWS

set -e

echo "=========================================="
echo "🔍 VERIFICACIÓN DE ERROR DE CONECTIVIDAD"
echo "=========================================="
echo ""

# Colores
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Información
BACKEND_SG="sg-8f1ff7fe"
BACKEND_REGION="us-east-2"
RDS_ENDPOINT="reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com"
RDS_REGION="us-east-1"
CLUSTER="devSanmarinoZoo"
SERVICE="sanmarino-back-task-service-75khncfa"

echo "=== 1. Verificando Estado del Backend ==="
BACKEND_STATUS=$(aws ecs describe-services \
  --cluster $CLUSTER \
  --services $SERVICE \
  --region $BACKEND_REGION \
  --query 'services[0].status' \
  --output text 2>/dev/null || echo "ERROR")

if [ "$BACKEND_STATUS" = "ACTIVE" ]; then
    echo -e "${GREEN}✅ Backend está ACTIVO${NC}"
else
    echo -e "${RED}❌ Backend NO está activo: $BACKEND_STATUS${NC}"
fi

RUNNING=$(aws ecs describe-services \
  --cluster $CLUSTER \
  --services $SERVICE \
  --region $BACKEND_REGION \
  --query 'services[0].runningCount' \
  --output text 2>/dev/null || echo "0")

echo "   Tareas ejecutándose: $RUNNING"
echo ""

echo "=== 2. Verificando Security Group del Backend ==="
BACKEND_OUTBOUND=$(aws ec2 describe-security-groups \
  --group-ids $BACKEND_SG \
  --region $BACKEND_REGION \
  --query 'SecurityGroups[0].IpPermissionsEgress[?IpProtocol==`-1` || (FromPort==`5432` && ToPort==`5432`)]' \
  --output json 2>/dev/null || echo "[]")

if echo "$BACKEND_OUTBOUND" | grep -q "0.0.0.0/0\|5432"; then
    echo -e "${GREEN}✅ Backend puede salir al puerto 5432${NC}"
else
    echo -e "${RED}❌ Backend NO puede salir al puerto 5432${NC}"
fi
echo ""

echo "=== 3. Verificando RDS ==="
echo "   Endpoint: $RDS_ENDPOINT"
echo "   Región: $RDS_REGION"
echo ""
echo "   ⚠️  Para verificar Security Group de RDS:"
echo "   1. Ve a consola AWS → RDS → Región $RDS_REGION"
echo "   2. Databases → Busca: $RDS_ENDPOINT"
echo "   3. Pestaña 'Connectivity & security'"
echo "   4. Anota Security Group ID"
echo ""

echo "=== 4. Comando para Verificar Security Group de RDS ==="
echo "   (Ejecuta después de obtener el Security Group ID)"
echo ""
echo "   aws ec2 describe-security-groups \\"
echo "     --group-ids <RDS_SECURITY_GROUP_ID> \\"
echo "     --region $RDS_REGION \\"
echo "     --query 'SecurityGroups[0].IpPermissions[?FromPort==\`5432\`]' \\"
echo "     --output table"
echo ""

echo "=== 5. Comando para SOLUCIONAR ==="
echo ""
echo "   aws ec2 authorize-security-group-ingress \\"
echo "     --group-id <RDS_SECURITY_GROUP_ID> \\"
echo "     --protocol tcp \\"
echo "     --port 5432 \\"
echo "     --source-group $BACKEND_SG \\"
echo "     --region $RDS_REGION"
echo ""

echo "=========================================="
echo "📋 RESUMEN"
echo "=========================================="
echo ""
echo "Backend (us-east-2):"
echo "  - Security Group: $BACKEND_SG"
echo "  - Estado: $BACKEND_STATUS"
echo ""
echo "RDS (us-east-1):"
echo "  - Endpoint: $RDS_ENDPOINT"
echo "  - ⚠️  Necesita regla que permita tráfico desde $BACKEND_SG"
echo ""
echo "Problema: RDS en región diferente (requiere acceso público o VPC Peering)"
echo ""

