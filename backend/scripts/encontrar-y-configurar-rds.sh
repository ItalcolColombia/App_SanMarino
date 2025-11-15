#!/bin/bash

# Script para encontrar RDS y configurar la conexión del backend
# =============================================================

set -e

REGION="us-east-2"
BACKEND_SG="sg-8f1ff7fe"
BACKEND_VPC="vpc-8ae456e1"
RDS_ENDPOINT="sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com"
DB_NAME="sanmarinoapp"

echo "=========================================="
echo "🔍 ENCONTRAR RDS Y CONFIGURAR BACKEND"
echo "=========================================="
echo ""

# Verificar credenciales
if ! aws sts get-caller-identity &> /dev/null; then
    echo "❌ Credenciales AWS inválidas"
    exit 1
fi

echo "✅ Credenciales válidas"
echo ""

echo "=== Información Conocida ==="
echo "RDS Endpoint: $RDS_ENDPOINT"
echo "Región: $REGION"
echo "Base de datos: $DB_NAME"
echo "Backend Security Group: $BACKEND_SG"
echo "Backend VPC: $BACKEND_VPC"
echo ""

# Intentar obtener información de RDS usando diferentes métodos
echo "=== Intentando encontrar RDS ==="

# Método 1: Buscar por tags o nombre
echo "Método 1: Buscando instancias que contengan 'sanmarino'..."
RDS_INSTANCES=$(aws rds describe-db-instances --region $REGION --query 'DBInstances[?contains(DBInstanceIdentifier, `sanmarino`) || contains(Endpoint.Address, `sanmarino`)].DBInstanceIdentifier' --output text 2>/dev/null || echo "")

if [ ! -z "$RDS_INSTANCES" ]; then
    echo "✅ Instancias encontradas:"
    echo "$RDS_INSTANCES"
    echo ""
    
    for INSTANCE in $RDS_INSTANCES; do
        echo "=== Información de instancia: $INSTANCE ==="
        
        # Obtener Security Groups de esta instancia
        RDS_SGS=$(aws rds describe-db-instances \
            --db-instance-identifier "$INSTANCE" \
            --region $REGION \
            --query 'DBInstances[0].VpcSecurityGroups[*].VpcSecurityGroupId' \
            --output text 2>/dev/null || echo "")
        
        if [ ! -z "$RDS_SGS" ]; then
            echo "Security Groups de RDS:"
            for SG in $RDS_SGS; do
                echo "  - $SG"
                
                # Verificar si ya tiene regla desde el backend
                HAS_RULE=$(aws ec2 describe-security-groups \
                    --group-ids "$SG" \
                    --region $REGION \
                    --query "SecurityGroups[0].IpPermissions[?FromPort==\`5432\` && contains(UserIdGroupPairs[*].GroupId, \`$BACKEND_SG\`)]" \
                    --output text 2>/dev/null || echo "")
                
                if [ -z "$HAS_RULE" ]; then
                    echo "    ⚠️  NO tiene regla desde el backend"
                    echo ""
                    echo "    🔧 COMANDO PARA AGREGAR REGLA:"
                    echo "    aws ec2 authorize-security-group-ingress \\"
                    echo "      --group-id $SG \\"
                    echo "      --protocol tcp \\"
                    echo "      --port 5432 \\"
                    echo "      --source-group $BACKEND_SG \\"
                    echo "      --region $REGION"
                    echo ""
                else
                    echo "    ✅ Ya tiene regla desde el backend"
                fi
            done
        else
            echo "  ⚠️  No se pudieron obtener Security Groups"
        fi
        
        # Obtener VPC de RDS
        RDS_VPC=$(aws rds describe-db-instances \
            --db-instance-identifier "$INSTANCE" \
            --region $REGION \
            --query 'DBInstances[0].DBSubnetGroup.VpcId' \
            --output text 2>/dev/null || echo "")
        
        if [ ! -z "$RDS_VPC" ]; then
            echo "  VPC de RDS: $RDS_VPC"
            if [ "$RDS_VPC" = "$BACKEND_VPC" ]; then
                echo "  ✅ RDS y Backend están en la misma VPC"
            else
                echo "  ⚠️  RDS y Backend están en VPCs diferentes"
                echo "      Se necesita VPC Peering"
            fi
        fi
        
        echo ""
    done
else
    echo "⚠️  No se encontraron instancias RDS con permisos actuales"
    echo ""
    echo "=== BÚSQUEDA ALTERNATIVA ==="
    echo "Buscando Security Groups en el VPC que puedan ser de RDS..."
    
    # Buscar Security Groups en el mismo VPC que tengan reglas en puerto 5432
    ALL_SGS=$(aws ec2 describe-security-groups \
        --filters "Name=vpc-id,Values=$BACKEND_VPC" \
        --region $REGION \
        --query 'SecurityGroups[*].GroupId' \
        --output text 2>/dev/null || echo "")
    
    if [ ! -z "$ALL_SGS" ]; then
        echo "Security Groups encontrados en VPC $BACKEND_VPC:"
        for SG in $ALL_SGS; do
            # Verificar si tiene reglas en puerto 5432
            HAS_5432=$(aws ec2 describe-security-groups \
                --group-ids "$SG" \
                --region $REGION \
                --query 'SecurityGroups[0].IpPermissions[?FromPort==`5432`]' \
                --output text 2>/dev/null || echo "")
            
            if [ ! -z "$HAS_5432" ]; then
                SG_NAME=$(aws ec2 describe-security-groups \
                    --group-ids "$SG" \
                    --region $REGION \
                    --query 'SecurityGroups[0].GroupName' \
                    --output text 2>/dev/null || echo "unknown")
                
                echo "  🔍 $SG ($SG_NAME) - Tiene reglas en puerto 5432"
                
                # Verificar si permite desde el backend
                ALLOWS_BACKEND=$(aws ec2 describe-security-groups \
                    --group-ids "$SG" \
                    --region $REGION \
                    --query "SecurityGroups[0].IpPermissions[?FromPort==\`5432\` && (UserIdGroupPairs[*].GroupId==\`$BACKEND_SG\` || IpRanges[*].CidrIp==\`0.0.0.0/0\`)]" \
                    --output text 2>/dev/null || echo "")
                
                if [ -z "$ALLOWS_BACKEND" ]; then
                    echo "    ⚠️  NO permite desde el backend ($BACKEND_SG)"
                    echo ""
                    echo "    🔧 COMANDO PARA AGREGAR REGLA:"
                    echo "    aws ec2 authorize-security-group-ingress \\"
                    echo "      --group-id $SG \\"
                    echo "      --protocol tcp \\"
                    echo "      --port 5432 \\"
                    echo "      --source-group $BACKEND_SG \\"
                    echo "      --region $REGION"
                    echo ""
                else
                    echo "    ✅ Ya permite desde el backend"
                fi
            fi
        done
    fi
fi

echo ""
echo "=========================================="
echo "📋 RESUMEN"
echo "=========================================="
echo ""
echo "Si no pudiste identificar el RDS automáticamente:"
echo "1. Ve a la consola AWS: https://console.aws.amazon.com/rds/"
echo "2. Región: $REGION"
echo "3. Databases → Busca instancia con endpoint: $RDS_ENDPOINT"
echo "4. Pestaña 'Connectivity & security' → Anota Security Group ID"
echo "5. Ejecuta el comando mostrado arriba con ese Security Group ID"
echo ""



