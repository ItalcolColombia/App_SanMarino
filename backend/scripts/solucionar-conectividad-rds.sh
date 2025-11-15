#!/bin/bash

# Script para solucionar conectividad Backend-RDS
# ================================================

set -e

REGION="us-east-2"
BACKEND_SG="sg-8f1ff7fe"  # Security Group del Backend
RDS_DB_ID="sanmarinoapp"   # Nombre de la instancia RDS

echo "=========================================="
echo "🔧 SOLUCIONAR CONECTIVIDAD BACKEND-RDS"
echo "=========================================="
echo ""

# Verificar que AWS CLI está instalado
if ! command -v aws &> /dev/null; then
    echo "❌ AWS CLI no está instalado"
    exit 1
fi

# Verificar credenciales
echo "1️⃣ Verificando credenciales..."
if ! aws sts get-caller-identity &> /dev/null; then
    echo "❌ Credenciales AWS inválidas"
    exit 1
fi
echo "✅ Credenciales válidas"
echo ""

echo "2️⃣ Información necesaria:"
echo "   Backend Security Group: $BACKEND_SG"
echo "   RDS Instance: $RDS_DB_ID"
echo "   Región: $REGION"
echo ""

# Intentar obtener el Security Group de RDS
echo "3️⃣ Intentando obtener Security Group de RDS..."
echo "   ⚠️  Si no tienes permisos RDS, necesitarás obtenerlo manualmente desde la consola AWS"
echo ""

# Mostrar comando para obtener SG de RDS manualmente
cat << INSTRUCTIONS
========================================
📝 INSTRUCCIONES PARA SOLUCIONAR
========================================

PASO 1: Obtener Security Group de RDS
--------------------------------------
1. Ve a la consola AWS: https://console.aws.amazon.com/rds/
2. Selecciona la región: us-east-2
3. Ve a "Databases" y busca: sanmarinoapp
4. Haz clic en la instancia
5. En la pestaña "Connectivity & security", anota el Security Group ID

PASO 2: Agregar regla al Security Group de RDS
------------------------------------------------
Una vez que tengas el Security Group ID de RDS, ejecuta:

aws ec2 authorize-security-group-ingress \\
  --group-id <RDS_SECURITY_GROUP_ID> \\
  --protocol tcp \\
  --port 5432 \\
  --source-group $BACKEND_SG \\
  --region $REGION

Esto permitirá que el backend (sg-8f1ff7fe) se conecte a RDS en puerto 5432.

PASO 3: Verificar que la regla se agregó
-----------------------------------------
aws ec2 describe-security-groups \\
  --group-ids <RDS_SECURITY_GROUP_ID> \\
  --region $REGION \\
  --query 'SecurityGroups[0].IpPermissions[?FromPort==\`5432\`]' \\
  --output table

INSTRUCTIONS

echo ""
echo "=========================================="
echo "🚀 COMANDO ALTERNATIVO (si tienes permisos)"
echo "=========================================="
echo ""
echo "Si tienes permisos RDS, puedes ejecutar este script mejorado"
echo "que intenta obtener automáticamente el Security Group de RDS:"
echo ""
echo "# El script buscará RDS y agregará la regla automáticamente"
echo ""



