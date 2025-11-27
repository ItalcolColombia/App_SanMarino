# Script para verificar y diagnosticar la conexión del backend a la base de datos local
Write-Host "=== DIAGNÓSTICO DE CONEXIÓN BACKEND A BASE DE DATOS ===" -ForegroundColor Cyan
Write-Host ""

$apiPath = "C:\Users\SAN MARINO\Documents\App_SanMarino_intalcol\App_SanMarino\backend\src\ZooSanMarino.API"

# 1. Verificar configuración
Write-Host "1. Verificando archivos de configuración..." -ForegroundColor Yellow
$devConfigPath = Join-Path $apiPath "appsettings.Development.json"
$baseConfigPath = Join-Path $apiPath "appsettings.json"

if (Test-Path $devConfigPath) {
    $devConfig = Get-Content $devConfigPath | ConvertFrom-Json
    Write-Host "   ✅ appsettings.Development.json encontrado" -ForegroundColor Green
    Write-Host "   ConnectionString: $($devConfig.ConnectionStrings.ZooSanMarinoContext)" -ForegroundColor Gray
} else {
    Write-Host "   ❌ appsettings.Development.json NO encontrado" -ForegroundColor Red
}

if (Test-Path $baseConfigPath) {
    $baseConfig = Get-Content $baseConfigPath | ConvertFrom-Json
    Write-Host "   ✅ appsettings.json encontrado" -ForegroundColor Green
    Write-Host "   ConnectionString (PRODUCCIÓN): $($baseConfig.ConnectionStrings.ZooSanMarinoContext)" -ForegroundColor Gray
}

Write-Host ""

# 2. Verificar entorno
Write-Host "2. Verificando variable de entorno ASPNETCORE_ENVIRONMENT..." -ForegroundColor Yellow
$currentEnv = $env:ASPNETCORE_ENVIRONMENT
if ($currentEnv -eq "Development") {
    Write-Host "   ✅ ASPNETCORE_ENVIRONMENT = Development" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  ASPNETCORE_ENVIRONMENT = '$currentEnv' (debería ser 'Development')" -ForegroundColor Yellow
    Write-Host "   💡 Esto significa que se usará appsettings.json en lugar de appsettings.Development.json" -ForegroundColor Yellow
}

Write-Host ""

# 3. Verificar puerto PostgreSQL
Write-Host "3. Verificando puerto PostgreSQL 5433..." -ForegroundColor Yellow
$portTest = Test-NetConnection -ComputerName localhost -Port 5433 -InformationLevel Quiet -WarningAction SilentlyContinue
if ($portTest) {
    Write-Host "   ✅ Puerto 5433 está abierto" -ForegroundColor Green
} else {
    Write-Host "   ❌ Puerto 5433 NO está abierto" -ForegroundColor Red
}

Write-Host ""

# 4. Probar conexión directa
Write-Host "4. Probando conexión directa a la base de datos..." -ForegroundColor Yellow
$connectionString = "Host=localhost;Port=5433;Username=postgres;Password=123456789;Database=sanmarinoapp_local;SSL Mode=Disable;Timeout=15;Command Timeout=30"

# Crear script temporal de prueba
$testScript = @"
using System;
using Npgsql;

try {
    var conn = new NpgsqlConnection("$connectionString");
    conn.Open();
    Console.WriteLine("   ✅ Conexión exitosa!");
    using (var cmd = new NpgsqlCommand("SELECT current_database();", conn)) {
        var db = cmd.ExecuteScalar();
        Console.WriteLine("   📁 Base de datos: " + db);
    }
    conn.Close();
} catch (Exception ex) {
    Console.WriteLine("   ❌ Error: " + ex.Message);
    Environment.Exit(1);
}
"@

$tempDir = Join-Path $env:TEMP "db-test-$(Get-Random)"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
$testScript | Out-File -FilePath "$tempDir\Program.cs" -Encoding UTF8

try {
    Push-Location $tempDir
    dotnet new console -n Test -f net8.0 --force 2>&1 | Out-Null
    dotnet add package Npgsql --version 8.0.0 2>&1 | Out-Null
    Copy-Item "$tempDir\Program.cs" "$tempDir\Program.cs.bak" -Force
    $testScript | Out-File -FilePath "$tempDir\Program.cs" -Encoding UTF8 -Force
    $result = dotnet run 2>&1
    Write-Host $result
} catch {
    Write-Host "   ⚠️  No se pudo probar la conexión automáticamente" -ForegroundColor Yellow
} finally {
    Pop-Location
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""

# 5. Recomendaciones
Write-Host "5. RECOMENDACIONES:" -ForegroundColor Yellow
Write-Host ""

if ($currentEnv -ne "Development") {
    Write-Host "   ⚠️  IMPORTANTE: El backend NO está usando el entorno Development" -ForegroundColor Red
    Write-Host "   Para solucionarlo, ejecuta el backend con:" -ForegroundColor Yellow
    Write-Host "   `$env:ASPNETCORE_ENVIRONMENT='Development'" -ForegroundColor Cyan
    Write-Host "   dotnet run" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "   O desde Visual Studio/VS Code, asegúrate de que el perfil use 'Development'" -ForegroundColor Yellow
} else {
    Write-Host "   ✅ El entorno está configurado correctamente" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== FIN DEL DIAGNÓSTICO ===" -ForegroundColor Cyan


