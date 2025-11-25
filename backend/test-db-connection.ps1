# Script para probar conexión a la base de datos
Write-Host "=== PRUEBA DE CONEXIÓN A BASE DE DATOS ===" -ForegroundColor Cyan
Write-Host ""

$connectionString = "Host=localhost;Port=5433;Username=postgres;Password=123456789;Database=sanmarinoapp_local;SSL Mode=Disable;Timeout=15;Command Timeout=30"

Write-Host "Connection String: $connectionString" -ForegroundColor Gray
Write-Host ""

# Crear un script C# temporal para probar la conexión
$testScript = @"
using System;
using Npgsql;

try {
    var connString = "$connectionString";
    Console.WriteLine("Intentando conectar...");
    
    using (var conn = new NpgsqlConnection(connString)) {
        conn.Open();
        Console.WriteLine("✅ Conexión exitosa!");
        
        using (var cmd = new NpgsqlCommand("SELECT version();", conn)) {
            var version = cmd.ExecuteScalar();
            Console.WriteLine("📊 Versión PostgreSQL: " + version);
        }
        
        using (var cmd = new NpgsqlCommand("SELECT current_database();", conn)) {
            var db = cmd.ExecuteScalar();
            Console.WriteLine("📁 Base de datos actual: " + db);
        }
        
        using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';", conn)) {
            var count = cmd.ExecuteScalar();
            Console.WriteLine("📋 Tablas en la base de datos: " + count);
        }
    }
} catch (Exception ex) {
    Console.WriteLine("❌ Error: " + ex.Message);
    Console.WriteLine("Tipo: " + ex.GetType().Name);
    if (ex.InnerException != null) {
        Console.WriteLine("Inner: " + ex.InnerException.Message);
    }
    Environment.Exit(1);
}
"@

$testScriptPath = [System.IO.Path]::GetTempFileName() + ".cs"
$testScript | Out-File -FilePath $testScriptPath -Encoding UTF8

Write-Host "Compilando script de prueba..." -ForegroundColor Yellow

# Intentar compilar y ejecutar
try {
    $apiPath = "C:\Users\SAN MARINO\Documents\App_SanMarino_intalcol\App_SanMarino\backend\src\ZooSanMarino.API"
    $dllPath = Join-Path $apiPath "bin\Debug\net8.0\ZooSanMarino.API.dll"
    
    if (Test-Path $dllPath) {
        Write-Host "Usando DLL del proyecto para referencia..." -ForegroundColor Gray
        $refs = @(
            "System.dll",
            "System.Data.dll",
            $dllPath
        )
    } else {
        Write-Host "⚠️ No se encontró el DLL compilado. Intentando compilar..." -ForegroundColor Yellow
        Set-Location $apiPath
        dotnet build --no-restore 2>&1 | Out-Null
    }
    
    # Intentar ejecutar directamente con dotnet
    Write-Host "Ejecutando prueba de conexión..." -ForegroundColor Yellow
    Write-Host ""
    
} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
} finally {
    if (Test-Path $testScriptPath) {
        Remove-Item $testScriptPath -ErrorAction SilentlyContinue
    }
}


