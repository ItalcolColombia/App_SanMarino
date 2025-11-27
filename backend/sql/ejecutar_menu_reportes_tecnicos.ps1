# Script PowerShell para ejecutar el SQL de inserción del menú de Reportes Técnicos
# Requiere: PostgreSQL instalado y acceso a la base de datos

param(
    [string]$DatabaseName = "sanmarinoapp_local",
    [string]$Host = "localhost",
    [int]$Port = 5433,
    [string]$Username = "postgres",
    [string]$Password = "123456789"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Ejecutando Script SQL para Menú de Reportes Técnicos ===" -ForegroundColor Cyan
Write-Host ""

# Construir la cadena de conexión
$connectionString = "Host=$Host;Port=$Port;Database=$DatabaseName;Username=$Username;Password=$Password"

# Leer el script SQL
$scriptPath = Join-Path $PSScriptRoot "add_reportes_tecnicos_menu_simple.sql"
if (-not (Test-Path $scriptPath)) {
    Write-Host "Error: No se encontró el archivo SQL en: $scriptPath" -ForegroundColor Red
    exit 1
}

$sqlScript = Get-Content $scriptPath -Raw

Write-Host "Conectando a la base de datos: $DatabaseName en $Host:$Port" -ForegroundColor Yellow
Write-Host ""

try {
    # Ejecutar usando psql si está disponible
    $env:PGPASSWORD = $Password
    $psqlCommand = "psql -h $Host -p $Port -U $Username -d $DatabaseName -f `"$scriptPath`""
    
    Write-Host "Ejecutando comando:" -ForegroundColor Gray
    Write-Host "  $psqlCommand" -ForegroundColor Gray
    Write-Host ""
    
    # Intentar ejecutar con psql
    $result = & psql -h $Host -p $Port -U $Username -d $DatabaseName -f $scriptPath 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Script ejecutado correctamente" -ForegroundColor Green
        Write-Host ""
        Write-Host "Resultado:" -ForegroundColor Yellow
        $result | Write-Host
    } else {
        Write-Host "❌ Error al ejecutar el script" -ForegroundColor Red
        $result | Write-Host
        exit 1
    }
} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternativa: Ejecuta el script manualmente usando:" -ForegroundColor Yellow
    Write-Host "  psql -h $Host -p $Port -U $Username -d $DatabaseName -f `"$scriptPath`"" -ForegroundColor Gray
    exit 1
} finally {
    $env:PGPASSWORD = $null
}

Write-Host ""
Write-Host "=== Verificación ===" -ForegroundColor Cyan
Write-Host "Verificando que el menú se insertó correctamente..." -ForegroundColor Yellow

$verifyQuery = @"
SELECT 
    id,
    label,
    icon,
    route,
    parent_id,
    "order",
    is_active
FROM menus
WHERE label = 'Reportes Técnicos';
"@

try {
    $verifyResult = & psql -h $Host -p $Port -U $Username -d $DatabaseName -c $verifyQuery 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        $verifyResult | Write-Host
        Write-Host ""
        Write-Host "✅ Menú 'Reportes Técnicos' configurado correctamente" -ForegroundColor Green
    }
} catch {
    Write-Host "⚠️  No se pudo verificar automáticamente. Verifica manualmente en la base de datos." -ForegroundColor Yellow
}


