# Script de Despliegue a Produccion
# =================================

Write-Host "PREPARACION PARA DESPLIEGUE A AWS ECS" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Green
Write-Host ""

# Verificar que estamos en el directorio correcto
if (-not (Test-Path "deploy-ecs.ps1")) {
    Write-Host "Error: No se encontro deploy-ecs.ps1" -ForegroundColor Red
    Write-Host "   Asegurate de estar en el directorio backend/" -ForegroundColor Yellow
    exit 1
}

# Verificar que Docker este ejecutandose
Write-Host "Verificando Docker..." -ForegroundColor Cyan
try {
    docker version | Out-Null
    Write-Host "Docker esta ejecutandose" -ForegroundColor Green
} catch {
    Write-Host "Error: Docker no esta ejecutandose" -ForegroundColor Red
    Write-Host "   Abre Docker Desktop y reintenta" -ForegroundColor Yellow
    exit 1
}

# Verificar AWS CLI
Write-Host "Verificando AWS CLI..." -ForegroundColor Cyan
try {
    aws --version | Out-Null
    Write-Host "AWS CLI esta instalado" -ForegroundColor Green
} catch {
    Write-Host "Error: AWS CLI no esta instalado" -ForegroundColor Red
    Write-Host "   Instala AWS CLI desde: https://aws.amazon.com/cli/" -ForegroundColor Yellow
    exit 1
}

# Verificar configuracion de AWS
Write-Host "Verificando configuracion de AWS..." -ForegroundColor Cyan
try {
    $awsIdentity = aws sts get-caller-identity --output json | ConvertFrom-Json
    Write-Host "AWS configurado correctamente" -ForegroundColor Green
    Write-Host "   Cuenta: $($awsIdentity.Account)" -ForegroundColor Gray
    Write-Host "   Usuario: $($awsIdentity.Arn)" -ForegroundColor Gray
} catch {
    Write-Host "Error: AWS no esta configurado correctamente" -ForegroundColor Red
    Write-Host "   Ejecuta: aws configure" -ForegroundColor Yellow
    exit 1
}

# Verificar que el Dockerfile existe
if (-not (Test-Path "Dockerfile")) {
    Write-Host "Error: No se encontro Dockerfile" -ForegroundColor Red
    Write-Host "   El Dockerfile debe estar en el directorio backend/" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "CONFIGURACION DE DESPLIEGUE:" -ForegroundColor Yellow
Write-Host "============================" -ForegroundColor Yellow
Write-Host "Perfil AWS: sanmarino" -ForegroundColor White
Write-Host "Region: us-east-2" -ForegroundColor White
Write-Host "Cluster: sanmarino-cluster" -ForegroundColor White
Write-Host "Servicio: sanmarino-api-svc" -ForegroundColor White
Write-Host "Familia TD: sanmarino-backend" -ForegroundColor White
Write-Host "Contenedor: api" -ForegroundColor White
Write-Host "ECR URI: 021891592771.dkr.ecr.us-east-2.amazonaws.com/sanmarino-backend" -ForegroundColor White
Write-Host ""

# Preguntar si continuar
$confirm = Read-Host "Continuar con el despliegue? (y/N)"
if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "Despliegue cancelado" -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "INICIANDO DESPLIEGUE..." -ForegroundColor Green
Write-Host "=======================" -ForegroundColor Green

# Ejecutar el script de despliegue
.\deploy-ecs.ps1 -Profile sanmarino -Region us-east-2 -Cluster sanmarino-cluster -Service sanmarino-api-svc -Family sanmarino-backend -Container api -EcrUri 021891592771.dkr.ecr.us-east-2.amazonaws.com/sanmarino-backend

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "DESPLIEGUE COMPLETADO EXITOSAMENTE!" -ForegroundColor Green
    Write-Host "===================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Proximos pasos:" -ForegroundColor Cyan
    Write-Host "1. Verificar que el servicio este funcionando en AWS Console" -ForegroundColor White
    Write-Host "2. Probar los endpoints de la API" -ForegroundColor White
    Write-Host "3. Verificar logs en CloudWatch" -ForegroundColor White
    Write-Host ""
    Write-Host "Enlaces utiles:" -ForegroundColor Cyan
    Write-Host "- AWS Console ECS: https://us-east-2.console.aws.amazon.com/ecs/" -ForegroundColor White
    Write-Host "- CloudWatch Logs: https://us-east-2.console.aws.amazon.com/cloudwatch/" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "ERROR EN EL DESPLIEGUE" -ForegroundColor Red
    Write-Host "======================" -ForegroundColor Red
    Write-Host "Revisa los errores anteriores y reintenta" -ForegroundColor Yellow
}