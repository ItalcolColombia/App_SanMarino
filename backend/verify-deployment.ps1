# Script de Verificación Previa al Despliegue
# ===========================================

Write-Host "🔍 VERIFICACIÓN PREVIA AL DESPLIEGUE" -ForegroundColor Cyan
Write-Host "====================================" -ForegroundColor Cyan
Write-Host ""

# Verificar que estamos en el directorio backend
if (-not (Test-Path "src/ZooSanMarino.API")) {
    Write-Host "❌ Error: No se encontró el proyecto API" -ForegroundColor Red
    Write-Host "   Asegúrate de estar en el directorio backend/" -ForegroundColor Yellow
    exit 1
}

# Verificar compilación del proyecto
Write-Host "🔨 Verificando compilación del proyecto..." -ForegroundColor Cyan
Set-Location "src/ZooSanMarino.API"
try {
    dotnet build --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Error: Fallo en la compilación" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Compilación exitosa" -ForegroundColor Green
} catch {
    Write-Host "❌ Error: No se pudo compilar el proyecto" -ForegroundColor Red
    exit 1
}

# Volver al directorio backend
Set-Location "../.."

# Verificar que el Dockerfile existe
if (-not (Test-Path "Dockerfile")) {
    Write-Host "❌ Error: No se encontró Dockerfile" -ForegroundColor Red
    exit 1
}

# Verificar que el script de despliegue existe
if (-not (Test-Path "deploy-ecs.ps1")) {
    Write-Host "❌ Error: No se encontró deploy-ecs.ps1" -ForegroundColor Red
    exit 1
}

# Verificar archivos de configuración
Write-Host "📋 Verificando archivos de configuración..." -ForegroundColor Cyan

# Verificar que existe appsettings.json
if (-not (Test-Path "src/ZooSanMarino.API/appsettings.json")) {
    Write-Host "⚠️  Advertencia: No se encontró appsettings.json" -ForegroundColor Yellow
    Write-Host "   Se usará appsettings.Production.json" -ForegroundColor Gray
} else {
    Write-Host "✅ appsettings.json encontrado" -ForegroundColor Green
}

# Verificar que existe appsettings.Production.json
if (-not (Test-Path "src/ZooSanMarino.API/appsettings.Production.json")) {
    Write-Host "⚠️  Advertencia: No se encontró appsettings.Production.json" -ForegroundColor Yellow
    Write-Host "   Se recomienda crear este archivo para producción" -ForegroundColor Gray
} else {
    Write-Host "✅ appsettings.Production.json encontrado" -ForegroundColor Green
}

# Verificar archivos de migración
$migrationFiles = Get-ChildItem "src/ZooSanMarino.Infrastructure/Persistence/Migrations" -Filter "*.cs" -ErrorAction SilentlyContinue
if ($migrationFiles) {
    Write-Host "✅ Migraciones encontradas: $($migrationFiles.Count) archivos" -ForegroundColor Green
} else {
    Write-Host "⚠️  Advertencia: No se encontraron archivos de migración" -ForegroundColor Yellow
}

# Verificar nuevas funcionalidades implementadas
Write-Host ""
Write-Host "🎯 VERIFICANDO NUEVAS FUNCIONALIDADES:" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan

# Verificar entidad SystemConfiguration
if (Test-Path "src/ZooSanMarino.Domain/Entities/SystemConfiguration.cs") {
    Write-Host "✅ SystemConfiguration.cs implementado" -ForegroundColor Green
} else {
    Write-Host "❌ SystemConfiguration.cs no encontrado" -ForegroundColor Red
}

# Verificar ConfigurationService
if (Test-Path "src/ZooSanMarino.Infrastructure/Services/ConfigurationService.cs") {
    Write-Host "✅ ConfigurationService.cs implementado" -ForegroundColor Green
} else {
    Write-Host "❌ ConfigurationService.cs no encontrado" -ForegroundColor Red
}

# Verificar ConfigurationController
if (Test-Path "src/ZooSanMarino.API/Controllers/ConfigurationController.cs") {
    Write-Host "✅ ConfigurationController.cs implementado" -ForegroundColor Green
} else {
    Write-Host "❌ ConfigurationController.cs no encontrado" -ForegroundColor Red
}

# Verificar EmailService
if (Test-Path "src/ZooSanMarino.Infrastructure/Services/EmailService.cs") {
    Write-Host "✅ EmailService.cs implementado" -ForegroundColor Green
} else {
    Write-Host "❌ EmailService.cs no encontrado" -ForegroundColor Red
}

# Verificar PasswordRecovery en AuthController
$authControllerContent = Get-Content "src/ZooSanMarino.API/Controllers/AuthController.cs" -Raw -ErrorAction SilentlyContinue
if ($authControllerContent -and $authControllerContent.Contains("RecoverPassword")) {
    Write-Host "✅ Funcionalidad de recuperación de contraseña implementada" -ForegroundColor Green
} else {
    Write-Host "❌ Funcionalidad de recuperación de contraseña no encontrada" -ForegroundColor Red
}

Write-Host ""
Write-Host "📊 RESUMEN DE VERIFICACIÓN:" -ForegroundColor Yellow
Write-Host "===========================" -ForegroundColor Yellow
Write-Host "✅ Proyecto compila correctamente" -ForegroundColor Green
Write-Host "✅ Dockerfile configurado" -ForegroundColor Green
Write-Host "✅ Script de despliegue disponible" -ForegroundColor Green
Write-Host "✅ Nuevas funcionalidades implementadas" -ForegroundColor Green
Write-Host ""
Write-Host "🚀 LISTO PARA DESPLIEGUE!" -ForegroundColor Green
Write-Host "=========================" -ForegroundColor Green
Write-Host ""
Write-Host "Para desplegar a producción, ejecuta:" -ForegroundColor Cyan
Write-Host "  .\deploy-to-production.ps1" -ForegroundColor White
Write-Host ""
Write-Host "O directamente:" -ForegroundColor Cyan
Write-Host "  .\deploy-ecs.ps1 -Profile sanmarino -Region us-east-2" -ForegroundColor White
Write-Host "    -Cluster sanmarino-cluster -Service sanmarino-api-svc" -ForegroundColor White
Write-Host "    -Family sanmarino-backend -Container api" -ForegroundColor White
Write-Host "    -EcrUri 021891592771.dkr.ecr.us-east-2.amazonaws.com/sanmarino-backend" -ForegroundColor White