# Script PowerShell para agregar regla de seguridad RDS
# ======================================================

$REGION = "us-east-2"
$BACKEND_SG = "sg-8f1ff7fe"  # Security Group del Backend

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "🔧 AGREGAR REGLA DE SEGURIDAD A RDS" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Solicitar Security Group de RDS
Write-Host "Ingresa el Security Group ID de RDS:" -ForegroundColor Yellow
Write-Host "(Lo puedes encontrar en RDS → Databases → sanmarinoapp → Connectivity & security)" -ForegroundColor Gray
$RDS_SG = Read-Host "RDS Security Group ID"

if ([string]::IsNullOrWhiteSpace($RDS_SG)) {
    Write-Host "❌ Security Group ID no puede estar vacío" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "¿Agregar la regla de seguridad? (S/N)" -ForegroundColor Yellow
$confirm = Read-Host

if ($confirm -ne "S" -and $confirm -ne "s") {
    Write-Host "Operación cancelada" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Agregando regla de seguridad..." -ForegroundColor Yellow

try {
    aws ec2 authorize-security-group-ingress `
        --group-id $RDS_SG `
        --protocol tcp `
        --port 5432 `
        --source-group $BACKEND_SG `
        --region $REGION
    
    Write-Host ""
    Write-Host "✅ Regla agregada exitosamente!" -ForegroundColor Green
    Write-Host ""
    Write-Host "La regla permite:" -ForegroundColor White
    Write-Host "  - Tráfico TCP en puerto 5432" -ForegroundColor Gray
    Write-Host "  - Desde Security Group: $BACKEND_SG (Backend)" -ForegroundColor Gray
    Write-Host "  - Hacia Security Group: $RDS_SG (RDS)" -ForegroundColor Gray
    Write-Host ""
    
    # Verificar que se agregó
    Write-Host "Verificando regla..." -ForegroundColor Yellow
    aws ec2 describe-security-groups `
        --group-ids $RDS_SG `
        --region $REGION `
        --query 'SecurityGroups[0].IpPermissions[?FromPort==`5432`]' `
        --output table
    
} catch {
    Write-Host ""
    Write-Host "❌ Error al agregar regla: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Verifica:" -ForegroundColor Yellow
    Write-Host "  1. Que tengas permisos ec2:AuthorizeSecurityGroupIngress" -ForegroundColor Gray
    Write-Host "  2. Que el Security Group ID de RDS sea correcto" -ForegroundColor Gray
    Write-Host "  3. Que la regla no exista ya" -ForegroundColor Gray
    exit 1
}



