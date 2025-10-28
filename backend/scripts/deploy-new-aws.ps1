<# 
Script de Despliegue Backend San Marino - Nuevo AWS
Account: 196080479890, Region: us-east-2
#>

param(
  [string]$Profile = "",
  [string]$RdsEndpoint = "",
  [string]$RdsUsername = "",
  [string]$RdsPassword = "",
  [string]$RdsDatabase = ""
)

$ErrorActionPreference = "Stop"

# Configuración
$ACCOUNT_ID = "196080479890"
$REGION = "us-east-2"
$CLUSTER = "devSanmarinoZoo"
$SERVICE = "sanmarino-back-task-service-75khncfa"
$FAMILY = "sanmarino-back-task"
$CONTAINER = "backend"
$ECR_URI = "$ACCOUNT_ID.dkr.ecr.$REGION.amazonaws.com/sanmarino/zootecnia/granjas/backend"
$TAG = Get-Date -Format 'yyyyMMdd-HHmm'

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Info($msg) { Write-Host "$msg" -ForegroundColor DarkCyan }
function Write-Warn($msg) { Write-Warning $msg }
function Fail($msg) { throw $msg }

Write-Host @"

==================================
Despliegue Backend San Marino
Nuevo AWS: Account $ACCOUNT_ID
==================================
Cluster: $CLUSTER
Service: $SERVICE
ECR URI: $ECR_URI
Tag: $TAG
==================================

"@ -ForegroundColor Green

# Configurar perfil si se proporciona
if ($Profile) {
    $env:AWS_PROFILE = $Profile
    Write-Info "Usando perfil: $Profile"
}
$env:AWS_REGION = $REGION

# ====== 1) Login a ECR ======
Write-Step "1) Login a ECR"
aws ecr get-login-password | docker login --username AWS --password-stdin $ECR_URI | Out-Null

# ====== 2) Build / Tag / Push ======
Write-Step "2) Build / Tag / Push"

# Verificar Docker
$dockerRunning = docker version --format '{{.Server.Version}}' 2>$null
if (-not $dockerRunning) {
    Fail "Docker daemon no está disponible. Abre Docker Desktop y reintenta."
}

$imgLocal = "$FAMILY`:$TAG"
$imgEcr = "$ECR_URI`:$TAG"

docker build -t $imgLocal .
if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker build" }

docker tag $imgLocal $imgEcr
if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker tag" }

docker tag $imgLocal "$ECR_URI`:latest"
if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker tag latest" }

docker push $imgEcr
if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker push" }

docker push "$ECR_URI`:latest"
if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker push latest" }

# ====== 3) Obtener Task Definition actual ======
Write-Step "3) Obteniendo Task Definition actual"
aws ecs describe-task-definition --task-definition $FAMILY --query 'taskDefinition.containerDefinitions' --output json > containers.json

# Actualizar imagen en containers.json
$containers = Get-Content .\containers.json -Raw | ConvertFrom-Json
$containers = @($containers)
foreach ($c in $containers) {
    if ($c.name -eq $CONTAINER) { 
        $c.image = $imgEcr
        Write-Info "Actualizada imagen: $($c.image)"
    }
}

# Guardar JSON actualizado
$containersJson = ConvertTo-Json -InputObject $containers -Depth 100 -Compress
Set-Content -Path .\containers.json -Value $containersJson -Encoding ascii -NoNewline

# ====== 4) Registrar nueva revisión de Task Definition ======
Write-Step "4) Registrando nueva Task Definition"
$NEW_TD_ARN = aws ecs register-task-definition `
  --family $FAMILY `
  --network-mode awsvpc `
  --requires-compatibilities FARGATE `
  --cpu 1024 `
  --memory 3072 `
  --task-role-arn "arn:aws:iam::${ACCOUNT_ID}:role/ecsTaskExecutionRole" `
  --execution-role-arn "arn:aws:iam::${ACCOUNT_ID}:role/ecsTaskExecutionRole" `
  --container-definitions file://containers.json `
  --query 'taskDefinition.taskDefinitionArn' --output text `
  --region $REGION

if (-not $NEW_TD_ARN) { Fail "Error registrando la nueva Task Definition" }

Write-Info "Nueva TD: $NEW_TD_ARN"

# ====== 5) Actualizar servicio ======
Write-Step "5) Actualizando servicio ECS"
aws ecs update-service --cluster $CLUSTER --service $SERVICE --task-definition $NEW_TD_ARN --force-new-deployment --region $REGION | Out-Null

Write-Info "Esperando a que el servicio se estabilice..."
aws ecs wait services-stable --cluster $CLUSTER --services $SERVICE --region $REGION

# ====== 6) Verificación ======
Write-Step "6) Verificación"
$activeTd = aws ecs describe-services --cluster $CLUSTER --services $SERVICE --query 'services[0].taskDefinition' --output text --region $REGION
Write-Info "Task Definition activa: $activeTd"

Write-Step "✓ Despliegue completado exitosamente"
Write-Host "Imagen desplegada: $imgEcr" -ForegroundColor Green

