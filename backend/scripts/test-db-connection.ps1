# Script para verificar conexión a la base de datos local
Write-Host "🔍 Verificando conexión a PostgreSQL..." -ForegroundColor Yellow

$connectionString = "Host=localhost;Port=5432;Username=postgres;Password=123456789;Database=sanmarinoapp_local;SSL Mode=Disable"

# Verificar si PostgreSQL está corriendo
Write-Host "`n1. Verificando si PostgreSQL está corriendo..." -ForegroundColor Cyan
$pgProcess = Get-Process -Name "postgres" -ErrorAction SilentlyContinue
if ($pgProcess) {
    Write-Host "   ✅ PostgreSQL está corriendo (PID: $($pgProcess.Id))" -ForegroundColor Green
} else {
    Write-Host "   ❌ PostgreSQL NO está corriendo" -ForegroundColor Red
    Write-Host "   💡 Inicia el servicio PostgreSQL desde Services.msc o ejecuta: net start postgresql-x64-15" -ForegroundColor Yellow
    exit 1
}

# Verificar si el puerto 5432 está abierto
Write-Host "`n2. Verificando puerto 5432..." -ForegroundColor Cyan
$port = Get-NetTCPConnection -LocalPort 5432 -ErrorAction SilentlyContinue
if ($port) {
    Write-Host "   ✅ Puerto 5432 está abierto" -ForegroundColor Green
} else {
    Write-Host "   ❌ Puerto 5432 NO está abierto" -ForegroundColor Red
    exit 1
}

# Intentar conectar usando psql si está disponible
Write-Host "`n3. Intentando conectar con psql..." -ForegroundColor Cyan
$env:PGPASSWORD = "123456789"
try {
    $result = & psql -h localhost -p 5432 -U postgres -d sanmarinoapp_local -c "SELECT version();" -t 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Conexión exitosa a la base de datos" -ForegroundColor Green
        Write-Host "   📊 Versión: $($result.Trim())" -ForegroundColor Gray
    } else {
        Write-Host "   ❌ Error al conectar: $result" -ForegroundColor Red
        
        # Verificar si la base de datos existe
        Write-Host "`n4. Verificando si la base de datos existe..." -ForegroundColor Cyan
        $dbCheck = & psql -h localhost -p 5432 -U postgres -d postgres -c "SELECT 1 FROM pg_database WHERE datname = 'sanmarinoapp_local';" -t 2>&1
        if ($LASTEXITCODE -eq 0 -and $dbCheck.Trim() -eq "1") {
            Write-Host "   ✅ La base de datos 'sanmarinoapp_local' existe" -ForegroundColor Green
        } else {
            Write-Host "   ❌ La base de datos 'sanmarinoapp_local' NO existe" -ForegroundColor Red
            Write-Host "   💡 Crear la base de datos con:" -ForegroundColor Yellow
            Write-Host "      psql -h localhost -p 5432 -U postgres -d postgres -c `"CREATE DATABASE sanmarinoapp_local;`"" -ForegroundColor Gray
        }
    }
} catch {
    Write-Host "   ⚠️ psql no está disponible en PATH" -ForegroundColor Yellow
    Write-Host "   💡 Instala PostgreSQL o agrega psql al PATH" -ForegroundColor Yellow
}

# Verificar conexión usando .NET (simulando lo que hace la aplicación)
Write-Host "`n5. Verificando conexión con Npgsql..." -ForegroundColor Cyan
try {
    $testScript = @"
using System;
using Npgsql;

try {
    var connString = "Host=localhost;Port=5432;Username=postgres;Password=123456789;Database=sanmarinoapp_local;SSL Mode=Disable;Timeout=5";
    using (var conn = new NpgsqlConnection(connString)) {
        conn.Open();
        Console.WriteLine("SUCCESS: Conexión exitosa");
        using (var cmd = new NpgsqlCommand("SELECT version();", conn)) {
            var version = cmd.ExecuteScalar();
            Console.WriteLine("VERSION: " + version);
        }
    }
} catch (Exception ex) {
    Console.WriteLine("ERROR: " + ex.Message);
    if (ex.InnerException != null) {
        Console.WriteLine("INNER: " + ex.InnerException.Message);
    }
}
"@
    $testScript | Out-File -FilePath "$env:TEMP\test-db.cs" -Encoding UTF8
    Write-Host "   ℹ️  Para probar con .NET, ejecuta el backend y revisa los logs" -ForegroundColor Gray
} catch {
    Write-Host "   ⚠️  No se pudo crear script de prueba" -ForegroundColor Yellow
}

Write-Host "`n✅ Verificación completada" -ForegroundColor Green
Write-Host "`n💡 Si hay errores, verifica:" -ForegroundColor Yellow
Write-Host "   1. PostgreSQL está corriendo" -ForegroundColor Gray
Write-Host "   2. La base de datos 'sanmarinoapp_local' existe" -ForegroundColor Gray
Write-Host "   3. El usuario 'postgres' tiene la contraseña '123456789'" -ForegroundColor Gray
Write-Host "   4. El puerto 5432 está disponible" -ForegroundColor Gray


