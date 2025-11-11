#!/bin/bash

# Script para configurar conexión Backend-RDS de Desarrollo
# ==========================================================

set -e

REGION_RDS="us-east-1"
REGION_BACKEND="us-east-2"
BACKEND_SG="sg-8f1ff7fe"
RDS_ENDPOINT="reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com"
DB_NAME="sanmarinoappdev"

echo "=========================================="
echo "🔧 CONFIGURAR RDS DE DESARROLLO"
echo "=========================================="
echo ""

echo "=== Información ==="
echo "RDS Endpoint: $RDS_ENDPOINT"
echo "Región RDS: $REGION_RDS"
echo "Región Backend: $REGION_BACKEND"
echo "Base de datos: $DB_NAME"
echo "Backend Security Group: $BACKEND_SG"
echo ""

echo "⚠️  IMPORTANTE:"
echo "   El RDS está en us-east-1 y el Backend en us-east-2"
echo "   Para que funcione, el RDS debe ser públicamente accesible"
echo "   O deben estar en VPCs con Peering configurado"
echo ""

echo "=========================================="
echo "📋 PASOS PARA CONFIGURAR"
echo "=========================================="
echo ""
echo "PASO 1: Obtener Security Group de RDS"
echo "--------------------------------------"
echo "1. Ve a: https://console.aws.amazon.com/rds/"
echo "2. Selecciona región: $REGION_RDS"
echo "3. Databases → Busca instancia con endpoint: $RDS_ENDPOINT"
echo "4. Haz clic en la instancia"
echo "5. Pestaña 'Connectivity & security'"
echo "6. Anota el 'VPC security groups' → Security Group ID"
echo ""

echo "PASO 2: Agregar regla al Security Group de RDS"
echo "------------------------------------------------"
echo "Una vez que tengas el Security Group ID de RDS, ejecuta:"
echo ""
echo "aws ec2 authorize-security-group-ingress \\"
echo "  --group-id <RDS_SECURITY_GROUP_ID> \\"
echo "  --protocol tcp \\"
echo "  --port 5432 \\"
echo "  --source-group $BACKEND_SG \\"
echo "  --region $REGION_RDS"
echo ""

echo "PASO 3: Verificar que RDS es públicamente accesible"
echo "----------------------------------------------------"
echo "En la consola RDS, pestaña 'Connectivity & security':"
echo "  - Verifica que 'Publicly accessible' esté en 'Yes'"
echo "  - Si está en 'No', necesitas modificar la instancia RDS"
echo ""

echo "PASO 4: Verificar VPC Peering (si no es público)"
echo "---------------------------------------------------"
echo "Si RDS NO es público, necesitas:"
echo "  1. VPC Peering entre VPC del backend y VPC del RDS"
echo "  2. Rutas en las Route Tables"
echo ""

echo "=========================================="
echo "🚀 COMANDO COMPLETO"
echo "=========================================="
echo ""
echo "Reemplaza <RDS_SECURITY_GROUP_ID> con el ID obtenido:"
echo ""
cat << 'COMMAND'
aws ec2 authorize-security-group-ingress \
  --group-id <RDS_SECURITY_GROUP_ID> \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
  --region us-east-1

# Verificar que se agregó
aws ec2 describe-security-groups \
  --group-ids <RDS_SECURITY_GROUP_ID> \
  --region us-east-1 \
  --query 'SecurityGroups[0].IpPermissions[?FromPort==`5432`]' \
  --output table
COMMAND
echo ""


