# Script PowerShell para verificar conectividad Backend-RDS en AWS
# ================================================================

$REGION = "us-east-2"
$RDS_ENDPOINT = "sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com"
$RDS_INSTANCE_ID = "sanmarinoapp"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "🔍 VERIFICACIÓN DE CONECTIVIDAD RDS" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que AWS CLI está instalado
try {
    $null = Get-Command aws -ErrorAction Stop
} catch {
    Write-Host "❌ AWS CLI no está instalado" -ForegroundColor Red
    exit 1
}

# Verificar credenciales AWS
Write-Host "1️⃣ Verificando credenciales AWS..." -ForegroundColor Yellow
try {
    $identity = aws sts get-caller-identity 2>$null | ConvertFrom-Json
    if (-not $identity) {
        throw "Credenciales inválidas"
    }
    Write-Host "✅ Credenciales AWS válidas" -ForegroundColor Green
} catch {
    Write-Host "❌ No se pudieron validar las credenciales AWS" -ForegroundColor Red
    Write-Host "   Ejecuta: aws configure" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Información de la cuenta
Write-Host "2️⃣ Información de la cuenta AWS:" -ForegroundColor Yellow
aws sts get-caller-identity --output table
Write-Host ""

# Verificar RDS
Write-Host "3️⃣ Verificando instancia RDS..." -ForegroundColor Yellow
try {
    $rdsInfo = aws rds describe-db-instances `
        --db-instance-identifier $RDS_INSTANCE_ID `
        --region $REGION `
        --output json 2>$null | ConvertFrom-Json
    
    if (-not $rdsInfo -or -not $rdsInfo.DBInstances) {
        throw "Instancia no encontrada"
    }
    
    Write-Host "✅ Instancia RDS encontrada" -ForegroundColor Green
    Write-Host ""
    
    # Información detallada del RDS
    Write-Host "4️⃣ Información de RDS:" -ForegroundColor Yellow
    $dbInstance = $rdsInfo.DBInstances[0]
    Write-Host "   Endpoint: $($dbInstance.Endpoint.Address)" -ForegroundColor White
    Write-Host "   Puerto: $($dbInstance.Endpoint.Port)" -ForegroundColor White
    Write-Host "   Estado: $($dbInstance.DBInstanceStatus)" -ForegroundColor White
    Write-Host "   Públicamente accesible: $($dbInstance.PubliclyAccessible)" -ForegroundColor White
    Write-Host "   Versión Engine: $($dbInstance.EngineVersion)" -ForegroundColor White
    Write-Host ""
    
    # Security Groups del RDS
    Write-Host "5️⃣ Security Groups del RDS:" -ForegroundColor Yellow
    $rdsSecurityGroups = $dbInstance.VpcSecurityGroups
    
    if (-not $rdsSecurityGroups -or $rdsSecurityGroups.Count -eq 0) {
        Write-Host "⚠️  No se encontraron Security Groups" -ForegroundColor Yellow
    } else {
        foreach ($sg in $rdsSecurityGroups) {
            $sgId = $sg.VpcSecurityGroupId
            Write-Host "   📋 Security Group: $sgId" -ForegroundColor White
            
            # Obtener detalles del Security Group
            $sgDetails = aws ec2 describe-security-groups `
                --group-ids $sgId `
                --region $REGION `
                --output json | ConvertFrom-Json
            
            $sgInfo = $sgDetails.SecurityGroups[0]
            Write-Host "      Nombre: $($sgInfo.GroupName)" -ForegroundColor Gray
            Write-Host "      Descripción: $($sgInfo.Description)" -ForegroundColor Gray
            Write-Host ""
            
            Write-Host "      🔒 Reglas de entrada (Inbound):" -ForegroundColor Cyan
            if ($sgInfo.IpPermissions.Count -eq 0) {
                Write-Host "         ⚠️  No hay reglas de entrada configuradas" -ForegroundColor Red
            } else {
                foreach ($rule in $sgInfo.IpPermissions) {
                    $protocol = $rule.IpProtocol
                    $fromPort = if ($rule.FromPort) { $rule.FromPort } else { "all" }
                    $toPort = if ($rule.ToPort) { $rule.ToPort } else { "all" }
                    
                    Write-Host "         Protocolo: $protocol, Puerto: $fromPort-$toPort" -ForegroundColor White
                    
                    if ($rule.IpRanges) {
                        foreach ($ipRange in $rule.IpRanges) {
                            Write-Host "            IP: $($ipRange.CidrIp)" -ForegroundColor Gray
                        }
                    }
                    
                    if ($rule.UserIdGroupPairs) {
                        foreach ($groupPair in $rule.UserIdGroupPairs) {
                            Write-Host "            Security Group: $($groupPair.GroupId)" -ForegroundColor Gray
                        }
                    }
                }
            }
            Write-Host ""
        }
    }
    
    # VPC del RDS
    Write-Host "6️⃣ VPC y Subnets del RDS:" -ForegroundColor Yellow
    $rdsVpc = $dbInstance.DBSubnetGroup.VpcId
    Write-Host "   VPC ID: $rdsVpc" -ForegroundColor White
    Write-Host ""
    
} catch {
    Write-Host "❌ Error al verificar RDS: $_" -ForegroundColor Red
    exit 1
}

# Buscar servicios ECS
Write-Host "7️⃣ Buscando servicios ECS en región $REGION..." -ForegroundColor Yellow
try {
    $clusters = aws ecs list-clusters `
        --region $REGION `
        --output json | ConvertFrom-Json
    
    if (-not $clusters.clusterArns -or $clusters.clusterArns.Count -eq 0) {
        Write-Host "⚠️  No se encontraron clusters ECS en la región $REGION" -ForegroundColor Yellow
    } else {
        Write-Host "✅ Clusters ECS encontrados:" -ForegroundColor Green
        foreach ($clusterArn in $clusters.clusterArns) {
            $clusterName = $clusterArn.Split('/')[-1]
            Write-Host "   - $clusterName" -ForegroundColor White
            
            # Obtener servicios del cluster
            $services = aws ecs list-services `
                --cluster $clusterName `
                --region $REGION `
                --output json | ConvertFrom-Json
            
            if ($services.serviceArns -and $services.serviceArns.Count -gt 0) {
                Write-Host "     Servicios:" -ForegroundColor Gray
                foreach ($serviceArn in $services.serviceArns) {
                    $serviceName = $serviceArn.Split('/')[-1]
                    Write-Host "       - $serviceName" -ForegroundColor Gray
                    
                    # Obtener configuración de red del servicio
                    $serviceDetails = aws ecs describe-services `
                        --cluster $clusterName `
                        --services $serviceName `
                        --region $REGION `
                        --output json | ConvertFrom-Json
                    
                    $networkConfig = $serviceDetails.services[0].networkConfiguration.awsvpcConfiguration
                    if ($networkConfig) {
                        Write-Host "         Subnets: $($networkConfig.subnets -join ', ')" -ForegroundColor DarkGray
                        Write-Host "         Security Groups: $($networkConfig.securityGroups -join ', ')" -ForegroundColor DarkGray
                        Write-Host "         Public IP: $($networkConfig.assignPublicIp)" -ForegroundColor DarkGray
                    }
                }
            }
            Write-Host ""
        }
    }
} catch {
    Write-Host "⚠️  Error al buscar servicios ECS: $_" -ForegroundColor Yellow
}

# Resumen final
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "📋 RESUMEN Y RECOMENDACIONES" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "🔍 Verifica lo siguiente:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. ✅ Backend y RDS en la misma región ($REGION)" -ForegroundColor White
Write-Host "2. ⚠️  Security Group de RDS permite tráfico en puerto 5432" -ForegroundColor Yellow
Write-Host "3. ⚠️  Security Group del Backend permite salida al puerto 5432" -ForegroundColor Yellow
Write-Host "4. ⚠️  Ambos Security Groups permiten comunicación entre sí" -ForegroundColor Yellow
Write-Host "5. ⚠️  Backend y RDS en la misma VPC ($rdsVpc)" -ForegroundColor Yellow
Write-Host ""
Write-Host "📝 Si necesitas agregar una regla de seguridad:" -ForegroundColor Cyan
Write-Host ""
Write-Host 'aws ec2 authorize-security-group-ingress \' -ForegroundColor Gray
Write-Host "  --group-id <SECURITY_GROUP_ID_RDS> \`" -ForegroundColor Gray
Write-Host "  --protocol tcp \`" -ForegroundColor Gray
Write-Host "  --port 5432 \`" -ForegroundColor Gray
Write-Host "  --source-group <SECURITY_GROUP_ID_BACKEND> \`" -ForegroundColor Gray
Write-Host "  --region $REGION" -ForegroundColor Gray
Write-Host ""



