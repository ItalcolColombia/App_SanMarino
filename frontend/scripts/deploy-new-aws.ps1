<# 
Script de Despliegue Frontend San Marino - Nuevo AWS
Account: 196080479890, Region: us-east-2
#>

$ErrorActionPreference = "Stop"

# Configuración
$ACCOUNT_ID = "196080479890"
$REGION = "us-east-2"
$CLUSTER = "devSanmarinoZoo"
$SERVICE = "sanmarino-front-task-service-zp2f403l"
$FAMILY = "sanmarino-front-task"
$CONTAINER = "frontend"
$ECR_URI = "$ACCOUNT_ID.dkr.ecr.$REGION.amazonaws.com/sanmarino/zootecnia/granjas/frontend"
$TAG = Get-Date -Format 'yyyyMMdd-HHmm'

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Info($msg) { Write-Host "$msg" -ForegroundColor DarkCyan }
function Write-Warn($msg) { Write-Warning $msg }
function Fail($msg) { throw $msg }

Write-Host @"

==================================
Despliegue Frontend San Marino
Nuevo AWS: Account $ACCOUNT_ID
==================================
Cluster: $CLUSTER
Service: $SERVICE
ECR URI: $ECR_URI
Tag: $TAG
==================================

"@ -ForegroundColor Green

$env:AWS_REGION = $REGION

# ====== 1) Login a ECR ======
Write-Step "1) Login a ECR"
aws ecr get-login-password --region $REGION | docker login --username AWS --password-stdin $ECR_URI | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "Fallo en login a ECR" }

# ====== 2) Build / Tag / Push ======
Write-Step "2) Build / Tag / Push"

# Verificar Docker
$dockerRunning = docker version --format '{{.Server.Version}}' 2>$null
if (-not $dockerRunning) {
    Fail "Docker daemon no está disponible. Abre Docker Desktop y reintenta."
}

# Build con buildx para linux/amd64
docker buildx build --platform linux/amd64 -t ${ECR_URI}:${TAG} -t ${ECR_URI}:latest --push .
if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker buildx build" }

# ====== 3) Preparar Task Definition ======
Write-Step "3) Preparando Task Definition"
$taskDefPath = "deploy/ecs-taskdef.json"
if (-not (Test-Path $taskDefPath)) {
    Fail "No se encontró $taskDefPath"
}

Copy-Item $taskDefPath "ecs-taskdef.json"

# Actualizar el tag de la imagen en el JSON
$taskDefContent = Get-Content "ecs-taskdef.json" -Raw
$taskDefContent = $taskDefContent -replace "${ECR_URI}:[^`"`}]*", "${ECR_URI}:${TAG}"
Set-Content -Path "ecs-taskdef.json" -Value $taskDefContent -NoNewline

Write-Info "Task Definition actualizada con tag: $TAG"

# ====== 4) Registrar nueva revisión de Task Definition ======
Write-Step "4) Registrando nueva Task Definition"
$NEW_TD_ARN = aws ecs register-task-definition `
  --cli-input-json file://ecs-taskdef.json `
  --query 'taskDefinition.taskDefinitionArn' `
  --output text `
  --region $REGION

if (-not $NEW_TD_ARN) { Fail "Error registrando la nueva Task Definition" }

Write-Info "Nueva TD: $NEW_TD_ARN"

# ====== 5) Actualizar servicio ======
Write-Step "5) Actualizando servicio ECS"
aws ecs update-service --cluster $CLUSTER --service $SERVICE --task-definition $NEW_TD_ARN --force-new-deployment --region $REGION | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "Fallo actualizando el servicio" }

Write-Info "Esperando a que el servicio se estabilice..."
aws ecs wait services-stable --cluster $CLUSTER --services $SERVICE --region $REGION

# ====== 6) Verificación ======
Write-Step "6) Verificación"
Start-Sleep -Seconds 10

$RUNNING = aws ecs describe-services --cluster $CLUSTER --services $SERVICE --region $REGION --query 'services[0].runningCount' --output text
if ([int]$RUNNING -eq 0) {
    Fail "El servicio no está corriendo. Revisa los logs."
}

Write-Info "Servicio corriendo con $RUNNING tarea(s)"

# Obtener ALB DNS
$ALB_DNS = aws elbv2 describe-load-balancers --region $REGION --query 'LoadBalancers[?contains(LoadBalancerName, `sanmarino`)].DNSName' --output text | Select-Object -First 1

Write-Step "Despliegue completado exitosamente"
Write-Host "Imagen desplegada: ${ECR_URI}:${TAG}" -ForegroundColor Green
if ($ALB_DNS) {
    Write-Host "Frontend (ALB): http://${ALB_DNS}/" -ForegroundColor Cyan
    Write-Host "API (ALB):      http://${ALB_DNS}/api" -ForegroundColor Cyan
}
