# ==========================================
# Script Automatizado de Despliegue Completo
# San Marino - Backend y Frontend a AWS ECS
# PowerShell Version
# ==========================================

$ErrorActionPreference = "Stop"

# Colores para PowerShell
function Write-Header($msg) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host $msg -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta
    Write-Host ""
}

function Write-Success($msg) {
    Write-Host "[SUCCESS] $msg" -ForegroundColor Green
}

function Write-Error($msg) {
    Write-Host "[ERROR] $msg" -ForegroundColor Red
    exit 1
}

function Write-Info($msg) {
    Write-Host "[INFO] $msg" -ForegroundColor Cyan
}

# Banner
Clear-Host
Write-Host @"
╔═══════════════════════════════════════════════════════════╗
║                                                           ║
║      🚀 DESPLIEGUE AUTOMATIZADO - SAN MARINO APP        ║
║                                                           ║
║           Backend + Frontend → AWS ECS                    ║
║                                                           ║
╚═══════════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

# Obtener directorio del proyecto
$PROJECT_ROOT = Split-Path -Parent $MyInvocation.MyCommand.Path

# Verificaciones pre-despliegue
Write-Header "VERIFICACIÓN DE PRE-REQUISITOS"

# Verificar Docker
Write-Info "Verificando Docker..."
try {
    $dockerVersion = docker version --format '{{.Server.Version}}' 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $dockerVersion) {
        Write-Error "Docker daemon no está corriendo. Por favor inicia Docker Desktop."
    }
    Write-Success "Docker está instalado y corriendo"
} catch {
    Write-Error "Docker no está instalado. Por favor instala Docker Desktop."
}

# Verificar AWS CLI
Write-Info "Verificando AWS CLI..."
if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
    Write-Error "AWS CLI no está instalado. Por favor instala AWS CLI."
}

# Verificar credenciales AWS
Write-Info "Verificando credenciales AWS..."
try {
    $identity = aws sts get-caller-identity 2>$null | ConvertFrom-Json
    if (-not $identity) {
        Write-Error "Credenciales AWS no configuradas. Ejecuta 'aws configure'."
    }
    $ACCOUNT_ID = $identity.Account
    $REGION = (aws configure get region) -or "us-east-2"
    Write-Success "AWS CLI configurado (Account: $ACCOUNT_ID, Region: $REGION)"
} catch {
    Write-Error "Error verificando credenciales AWS"
}

# Verificar que los scripts existen
Write-Info "Verificando scripts de despliegue..."
if (-not (Test-Path "$PROJECT_ROOT\backend\scripts\deploy-backend-ecs.sh")) {
    Write-Error "No se encontró backend\scripts\deploy-backend-ecs.sh"
}

if (-not (Test-Path "$PROJECT_ROOT\frontend\scripts\deploy-frontend-ecs.sh")) {
    Write-Error "No se encontró frontend\scripts\deploy-frontend-ecs.sh"
}
Write-Success "Scripts de despliegue encontrados"

# Opciones de despliegue
Write-Header "OPCIONES DE DESPLIEGUE"
Write-Host "1) Desplegar Backend y Frontend (completo)"
Write-Host "2) Desplegar solo Backend"
Write-Host "3) Desplegar solo Frontend"
Write-Host ""
$OPTION = Read-Host "Selecciona una opción (1-3)"

switch ($OPTION) {
    "1" {
        $DEPLOY_BACKEND = $true
        $DEPLOY_FRONTEND = $true
    }
    "2" {
        $DEPLOY_BACKEND = $true
        $DEPLOY_FRONTEND = $false
    }
    "3" {
        $DEPLOY_BACKEND = $false
        $DEPLOY_FRONTEND = $true
    }
    default {
        Write-Error "Opción inválida"
    }
}

# Desplegar Backend
if ($DEPLOY_BACKEND) {
    Write-Header "DESPLIEGUE BACKEND"
    Push-Location "$PROJECT_ROOT\backend"
    
    Write-Info "Ejecutando script de despliegue del backend..."
    # En PowerShell, necesitamos usar bash o wsl para ejecutar scripts .sh
    if (Get-Command bash -ErrorAction SilentlyContinue) {
        bash scripts/deploy-backend-ecs.sh
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Backend desplegado exitosamente"
            $BACKEND_SUCCESS = $true
        } else {
            Write-Error "Fallo en despliegue del backend"
        }
    } else {
        Write-Error "Bash no está disponible. Instala Git Bash o WSL para ejecutar scripts .sh"
    }
    
    Pop-Location
    Write-Host ""
}

# Desplegar Frontend
if ($DEPLOY_FRONTEND) {
    Write-Header "DESPLIEGUE FRONTEND"
    Push-Location "$PROJECT_ROOT\frontend"
    
    Write-Info "Ejecutando script de despliegue del frontend..."
    if (Get-Command bash -ErrorAction SilentlyContinue) {
        bash scripts/deploy-frontend-ecs.sh
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Frontend desplegado exitosamente"
            $FRONTEND_SUCCESS = $true
        } else {
            Write-Error "Fallo en despliegue del frontend"
        }
    } else {
        Write-Error "Bash no está disponible"
    }
    
    Pop-Location
    Write-Host ""
}

# Resumen final
Write-Header "RESUMEN DEL DESPLIEGUE"

if ($DEPLOY_BACKEND) {
    if ($BACKEND_SUCCESS) {
        Write-Host "✅ Backend: DESPLEGADO" -ForegroundColor Green
    } else {
        Write-Host "❌ Backend: FALLÓ" -ForegroundColor Red
    }
}

if ($DEPLOY_FRONTEND) {
    if ($FRONTEND_SUCCESS) {
        Write-Host "✅ Frontend: DESPLEGADO" -ForegroundColor Green
    } else {
        Write-Host "❌ Frontend: FALLÓ" -ForegroundColor Red
    }
}

# Obtener URLs
Write-Header "URLs DE ACCESO"

try {
    $albOutput = aws elbv2 describe-load-balancers --region us-east-2 --query 'LoadBalancers[?contains(LoadBalancerName, `sanmarino`)].DNSName' --output text 2>$null
    if ($albOutput) {
        $ALB_DNS = ($albOutput -split "`n" | Select-Object -First 1).Trim()
        Write-Host "Frontend (ALB): http://$ALB_DNS" -ForegroundColor Cyan
        Write-Host "API (ALB):      http://$ALB_DNS/api" -ForegroundColor Cyan
        Write-Host "Swagger:        http://$ALB_DNS/swagger" -ForegroundColor Cyan
    }
} catch {
    Write-Host "No se pudo obtener la URL del ALB" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "🎉 DESPLIEGUE COMPLETADO" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

