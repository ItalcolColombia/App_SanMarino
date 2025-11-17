<# 
.SYNOPSIS
  Build, tag & push a Docker image to ECR, registra nueva Task Definition y
  fuerza el despliegue de un servicio ECS (Fargate).

.EXAMPLE
  .\deploy-ecs.ps1 -Profile sanmarino -Region us-east-2 `
    -Cluster sanmarino-cluster -Service sanmarino-api-svc `
    -Family sanmarino-backend -Container api `
    -EcrUri 021891592771.dkr.ecr.us-east-2.amazonaws.com/sanmarino-backend

.PARAMETERS
  -Profile       Perfil del AWS CLI (opcional si ya usas variables de entorno).
  -Region        Región AWS (ej. us-east-2).
  -Cluster       Nombre del cluster ECS.
  -Service       Nombre del servicio ECS.
  -Family        Nombre de la Task Definition (family).
  -Container     Nombre del contenedor dentro de la TD a actualizar.
  -EcrUri        URI completo del repo ECR (cuenta.dkr.ecr.region.amazonaws.com/repo).
  -RepoName      Nombre local de la imagen Docker (default = nombre del repo ECR).
  -Tag           Tag a usar para la imagen. Si no se pasa, usa timestamp 'yyyyMMdd-HHmm'.
  -UseBuildx     Usa `docker buildx build` en vez de `docker build`.
  -Platform      Plataforma para buildx (default 'linux/amd64' si -UseBuildx).
#>

param(
  [string]$Profile,
  [Parameter(Mandatory=$true)][string]$Region,
  [Parameter(Mandatory=$true)][string]$Cluster,
  [Parameter(Mandatory=$true)][string]$Service,
  [Parameter(Mandatory=$true)][string]$Family,
  [Parameter(Mandatory=$true)][string]$Container,
  [Parameter(Mandatory=$true)][string]$EcrUri,
  [string]$RepoName,
  [string]$Tag,
  [switch]$UseBuildx,
  [string]$Platform = "linux/amd64"
)

$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Info($msg) { Write-Host "$msg" -ForegroundColor DarkCyan }
function Write-Warn($msg) { Write-Warning $msg }
function Fail($msg)      { throw $msg }

function Wait-Docker {
  $deadline = (Get-Date).AddMinutes(3)
  while ((Get-Date) -lt $deadline) {
    try {
      $v = docker version --format '{{.Server.Version}}' 2>$null
      if ($LASTEXITCODE -eq 0 -and $v) { return }
    } catch {}
    Start-Sleep -Seconds 2
  }
  Fail "Docker daemon no está disponible (timeout). Abre Docker Desktop y reintenta."
}

# ====== 0) Preparación de entorno ======
if ($Profile) { $env:AWS_PROFILE = $Profile }
$env:AWS_REGION  = $Region

# Deducir RepoName si no se pasa (toma lo que está después del último '/')
if (-not $RepoName) { 
  $RepoName = ($EcrUri.Split('/')[-1])
}

# Tag por timestamp si no se pasa
if (-not $Tag) { $Tag = Get-Date -Format 'yyyyMMdd-HHmm' }

Write-Info "Perfil: $($env:AWS_PROFILE)"
Write-Info "Región: $Region"
Write-Info "ECR URI: $EcrUri"
Write-Info "Repo local: $RepoName"
Write-Info "Tag: $Tag"

# ====== 1) Login a ECR ======
Write-Step "1) Login a ECR"
$registry = $EcrUri.Split('/')[0]
aws ecr get-login-password | docker login --username AWS --password-stdin $registry | Out-Null

# ====== 2) Build / Tag / Push ======
Write-Step "2) Build / Tag / Push"

Wait-Docker

$imgLocal = ("{0}:{1}" -f $RepoName, $Tag)
$imgEcr   = ("{0}:{1}" -f $EcrUri, $Tag)

if ($UseBuildx) {
  Write-Info "Usando buildx para plataforma: $Platform"
  docker buildx build --platform $Platform -t $imgLocal .
  if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker buildx build" }
} else {
  docker build -t $imgLocal .
  if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker build" }
}

docker tag $imgLocal $imgEcr
if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker tag" }

docker push $imgEcr
if ($LASTEXITCODE -ne 0) { Fail "Fallo en docker push" }

# ====== 3) Exportar containerDefinitions de la TD base ======
Write-Step "3) Exportar containerDefinitions de la TD base"
aws ecs describe-task-definition --task-definition $Family `
  --query 'taskDefinition.containerDefinitions' --output json > containers.json

# Cargar como array (aunque venga un objeto), actualizar imagen + forzar env.value a string
$containers = Get-Content .\containers.json -Raw | ConvertFrom-Json
$containers = @($containers)
foreach ($c in $containers) {
  if ($c.name -eq $Container) { $c.image = ("{0}:{1}" -f $EcrUri, $Tag) }
  if ($c.environment) { foreach ($e in $c.environment) { $e.value = "$($e.value)" } }
}

# Guardar en JSON compacto y SIN BOM
$containersJson = ConvertTo-Json -InputObject $containers -Depth 100 -Compress
Set-Content -Path .\containers.json -Value $containersJson -Encoding ascii -NoNewline

# ====== 4) Leer metadatos mínimos de la TD base ======
$meta = aws ecs describe-task-definition --task-definition $Family `
  --query 'taskDefinition.{cpu:cpu,memory:memory,networkMode:networkMode,taskRoleArn:taskRoleArn,executionRoleArn:executionRoleArn,volumes:volumes}' `
  --output json | ConvertFrom-Json

# Si hay volumes, exportarlos a archivo para pasarlos como file://
$volArgs = @()
if ($meta.volumes -and $meta.volumes.Count -gt 0) {
  $volJson = ($meta.volumes | ConvertTo-Json -Depth 100 -Compress)
  Set-Content -Path .\volumes.json -Value $volJson -Encoding ascii -NoNewline
  $volArgs = @("--volumes", ("file://{0}" -f (Join-Path (Get-Location) 'volumes.json')))
}

# ====== 5) Registrar nueva revisión de TD ======
Write-Step "4) Registrar nueva revisión de TD"
$regArgs = @(
  "ecs","register-task-definition",
  "--family",               $Family,
  "--network-mode",         $meta.networkMode,
  "--requires-compatibilities","FARGATE",
  "--cpu",                  "$($meta.cpu)",
  "--memory",               "$($meta.memory)",
  "--container-definitions","file://containers.json",
  "--query","taskDefinition.taskDefinitionArn","--output","text"
)
if ($meta.executionRoleArn) { $regArgs += @("--execution-role-arn", $meta.executionRoleArn) }
if ($meta.taskRoleArn)      { $regArgs += @("--task-role-arn",      $meta.taskRoleArn) }
$regArgs += $volArgs

$NEW_TD_ARN = aws @regArgs
if (-not $NEW_TD_ARN) { Fail "Error registrando la nueva Task Definition" }

Write-Info "Nueva TD: $NEW_TD_ARN"

# ====== 6) Actualizar servicio y forzar rollout ======
Write-Step "5) Actualizar servicio y forzar rollout"
aws ecs update-service --cluster $Cluster --service $Service --task-definition $NEW_TD_ARN --force-new-deployment | Out-Null
aws ecs wait services-stable --cluster $Cluster --services $Service

# ====== 7) Verificación ======
Write-Step "6) Verificación"
$activeTd  = aws ecs describe-services --cluster $Cluster --services $Service --query 'services[0].taskDefinition' --output text
$imgActiva = aws ecs describe-task-definition --task-definition $activeTd `
  --query ("taskDefinition.containerDefinitions[?name=='{0}'].image" -f $Container) --output text

Write-Host "TD activa del servicio: $activeTd"
Write-Host "Imagen activa en contenedor '$Container': $imgActiva"

if ($imgActiva -ne $imgEcr) {
  Write-Warn "La imagen activa no coincide con la recién publicada ($imgEcr). Reintentando rollout forzado…"
  aws ecs update-service --cluster $Cluster --service $Service --task-definition $NEW_TD_ARN --force-new-deployment | Out-Null
  aws ecs wait services-stable --cluster $Cluster --services $Service
  $imgActiva = aws ecs describe-task-definition --task-definition $NEW_TD_ARN `
    --query ("taskDefinition.containerDefinitions[?name=='{0}'].image" -f $Container) --output text
  Write-Host "Imagen tras reintento: $imgActiva"
}

Write-Step ("DONE: versión desplegada {0}" -f $imgEcr)
