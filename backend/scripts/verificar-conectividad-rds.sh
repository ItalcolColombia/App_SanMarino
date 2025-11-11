#!/bin/bash

# Script para verificar conectividad Backend-RDS en AWS
# =====================================================

set -e

REGION="us-east-2"
RDS_ENDPOINT="sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com"
RDS_INSTANCE_ID="sanmarinoapp"

echo "=========================================="
echo "🔍 VERIFICACIÓN DE CONECTIVIDAD RDS"
echo "=========================================="
echo ""

# Verificar que AWS CLI está instalado
if ! command -v aws &> /dev/null; then
    echo "❌ AWS CLI no está instalado"
    exit 1
fi

# Verificar credenciales AWS
echo "1️⃣ Verificando credenciales AWS..."
if ! aws sts get-caller-identity &> /dev/null; then
    echo "❌ No se pudieron validar las credenciales AWS"
    echo "   Ejecuta: aws configure"
    exit 1
fi
echo "✅ Credenciales AWS válidas"
echo ""

# Información de la cuenta
echo "2️⃣ Información de la cuenta AWS:"
aws sts get-caller-identity --output table
echo ""

# Verificar RDS
echo "3️⃣ Verificando instancia RDS..."
if ! aws rds describe-db-instances \
    --db-instance-identifier "$RDS_INSTANCE_ID" \
    --region "$REGION" &> /dev/null; then
    echo "❌ No se encontró la instancia RDS: $RDS_INSTANCE_ID en región $REGION"
    exit 1
fi

echo "✅ Instancia RDS encontrada"
echo ""

# Información detallada del RDS
echo "4️⃣ Información de RDS:"
echo "   Endpoint:"
aws rds describe-db-instances \
    --db-instance-identifier "$RDS_INSTANCE_ID" \
    --region "$REGION" \
    --query 'DBInstances[0].[Endpoint.Address,Endpoint.Port,DBInstanceStatus,PubliclyAccessible,EngineVersion]' \
    --output table
echo ""

# Security Groups del RDS
echo "5️⃣ Security Groups del RDS:"
RDS_SG_IDS=$(aws rds describe-db-instances \
    --db-instance-identifier "$RDS_INSTANCE_ID" \
    --region "$REGION" \
    --query 'DBInstances[0].VpcSecurityGroups[*].VpcSecurityGroupId' \
    --output text)

if [ -z "$RDS_SG_IDS" ]; then
    echo "⚠️  No se encontraron Security Groups"
else
    echo "   Security Group IDs: $RDS_SG_IDS"
    echo ""
    
    for SG_ID in $RDS_SG_IDS; do
        echo "   📋 Reglas del Security Group: $SG_ID"
        aws ec2 describe-security-groups \
            --group-ids "$SG_ID" \
            --region "$REGION" \
            --query 'SecurityGroups[0].[GroupId,GroupName,Description]' \
            --output table
        
        echo "   🔒 Reglas de entrada (Inbound):"
        aws ec2 describe-security-groups \
            --group-ids "$SG_ID" \
            --region "$REGION" \
            --query 'SecurityGroups[0].IpPermissions[*].[IpProtocol,FromPort,ToPort,IpRanges[0].CidrIp,UserIdGroupPairs[0].GroupId]' \
            --output table
        echo ""
    done
fi

# VPC del RDS
echo "6️⃣ VPC y Subnets del RDS:"
RDS_VPC=$(aws rds describe-db-instances \
    --db-instance-identifier "$RDS_INSTANCE_ID" \
    --region "$REGION" \
    --query 'DBInstances[0].DBSubnetGroup.VpcId' \
    --output text)

echo "   VPC ID: $RDS_VPC"
echo ""

# Buscar servicios ECS en la región
echo "7️⃣ Buscando servicios ECS en región $REGION..."
CLUSTERS=$(aws ecs list-clusters \
    --region "$REGION" \
    --query 'clusterArns[*]' \
    --output text)

if [ -z "$CLUSTERS" ]; then
    echo "⚠️  No se encontraron clusters ECS en la región $REGION"
else
    echo "✅ Clusters ECS encontrados:"
    for CLUSTER_ARN in $CLUSTERS; do
        CLUSTER_NAME=$(basename "$CLUSTER_ARN")
        echo "   - $CLUSTER_NAME"
        
        # Obtener servicios del cluster
        SERVICES=$(aws ecs list-services \
            --cluster "$CLUSTER_NAME" \
            --region "$REGION" \
            --query 'serviceArns[*]' \
            --output text)
        
        if [ ! -z "$SERVICES" ]; then
            echo "     Servicios:"
            for SERVICE_ARN in $SERVICES; do
                SERVICE_NAME=$(basename "$SERVICE_ARN")
                echo "       - $SERVICE_NAME"
                
                # Obtener configuración de red del servicio
                echo "         Configuración de red:"
                aws ecs describe-services \
                    --cluster "$CLUSTER_NAME" \
                    --services "$SERVICE_NAME" \
                    --region "$REGION" \
                    --query 'services[0].networkConfiguration.awsvpcConfiguration.{Subnets:subnets[*],SecurityGroups:securityGroups[*],AssignPublicIp:assignPublicIp}' \
                    --output table
            done
        fi
        echo ""
    done
fi

# Resumen final
echo "=========================================="
echo "📋 RESUMEN Y RECOMENDACIONES"
echo "=========================================="
echo ""
echo "🔍 Verifica lo siguiente:"
echo ""
echo "1. ✅ Backend y RDS en la misma región ($REGION)"
echo "2. ⚠️  Security Group de RDS permite tráfico en puerto 5432"
echo "3. ⚠️  Security Group del Backend permite salida al puerto 5432"
echo "4. ⚠️  Ambos Security Groups permiten comunicación entre sí"
echo "5. ⚠️  Backend y RDS en la misma VPC ($RDS_VPC)"
echo ""
echo "📝 Si necesitas agregar una regla de seguridad:"
echo ""
echo "aws ec2 authorize-security-group-ingress \\"
echo "  --group-id <SECURITY_GROUP_ID_RDS> \\"
echo "  --protocol tcp \\"
echo "  --port 5432 \\"
echo "  --source-group <SECURITY_GROUP_ID_BACKEND> \\"
echo "  --region $REGION"
echo ""


