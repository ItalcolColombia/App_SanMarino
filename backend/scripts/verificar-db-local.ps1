# Script para verificar y crear la base de datos local
# Ejecutar en PowerShell

$ErrorActionPreference = "Stop"

Write-Host "🔍 Verificando conexión a PostgreSQL..." -ForegroundColor Yellow

# Parámetros de conexión
$dbHost = "localhost"
$dbPort = "5432"
$dbUser = "postgres"
$dbPassword = "123456789"
$dbName = "sanmarinoapp_local"

# Verificar si PostgreSQL está corriendo
Write-Host "📡 Verificando si PostgreSQL está corriendo..." -ForegroundColor Cyan
try {
    $pgProcess = Get-Process -Name "postgres" -ErrorAction SilentlyContinue
    if ($pgProcess) {
        Write-Host "✅ PostgreSQL está corriendo" -ForegroundColor Green
    } else {
        Write-Host "⚠️  PostgreSQL no parece estar corriendo" -ForegroundColor Yellow
        Write-Host "💡 Intenta iniciar el servicio PostgreSQL desde Services.msc" -ForegroundColor Yellow
    }
} catch {
    Write-Host "⚠️  No se pudo verificar el proceso de PostgreSQL" -ForegroundColor Yellow
}

# Verificar conexión usando psql si está disponible
Write-Host "`n🔌 Probando conexión a PostgreSQL..." -ForegroundColor Cyan

# Establecer variable de entorno para la contraseña
$env:PGPASSWORD = $dbPassword

try {
    # Intentar conectar y listar bases de datos
    $result = & psql -h $dbHost -p $dbPort -U $dbUser -d postgres -c "SELECT version();" -t 2>&1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Conexión a PostgreSQL exitosa" -ForegroundColor Green
        Write-Host "📋 Versión: $($result.Trim())" -ForegroundColor Gray
        
        # Verificar si la base de datos existe
        Write-Host "`n🔍 Verificando si la base de datos '$dbName' existe..." -ForegroundColor Cyan
        $dbExists = & psql -h $dbHost -p $dbPort -U $dbUser -d postgres -c "SELECT 1 FROM pg_database WHERE datname = '$dbName';" -t 2>&1
        
        if ($dbExists -match "1") {
            Write-Host "✅ La base de datos '$dbName' ya existe" -ForegroundColor Green
        } else {
            Write-Host "⚠️  La base de datos '$dbName' NO existe" -ForegroundColor Yellow
            Write-Host "🔨 Creando base de datos '$dbName'..." -ForegroundColor Cyan
            
            $createResult = & psql -h $dbHost -p $dbPort -U $dbUser -d postgres -c "CREATE DATABASE $dbName;" 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Base de datos '$dbName' creada exitosamente" -ForegroundColor Green
            } else {
                Write-Host "❌ Error al crear la base de datos: $createResult" -ForegroundColor Red
                exit 1
            }
        }
        
        # Verificar tablas en la base de datos
        Write-Host "`n📊 Verificando tablas en la base de datos..." -ForegroundColor Cyan
        $tables = & psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" -t 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $tableCount = ($tables -replace '\s', '')
            if ($tableCount -gt 0) {
                Write-Host "✅ La base de datos tiene $tableCount tabla(s)" -ForegroundColor Green
            } else {
                Write-Host "⚠️  La base de datos está vacía (no tiene tablas)" -ForegroundColor Yellow
                Write-Host "💡 Necesitas ejecutar las migraciones de Entity Framework:" -ForegroundColor Yellow
                Write-Host "   cd backend/src/ZooSanMarino.Infrastructure" -ForegroundColor Gray
                Write-Host "   dotnet ef database update" -ForegroundColor Gray
            }
        }
        
    } else {
        Write-Host "❌ Error al conectar a PostgreSQL" -ForegroundColor Red
        Write-Host "Detalles: $result" -ForegroundColor Red
        Write-Host "`n💡 Verifica:" -ForegroundColor Yellow
        Write-Host "   1. PostgreSQL está instalado y corriendo" -ForegroundColor Gray
        Write-Host "   2. El usuario '$dbUser' existe" -ForegroundColor Gray
        Write-Host "   3. La contraseña es correcta" -ForegroundColor Gray
        Write-Host "   4. El puerto $dbPort está disponible" -ForegroundColor Gray
        exit 1
    }
    
} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    Write-Host "`n💡 Asegúrate de que:" -ForegroundColor Yellow
    Write-Host "   1. PostgreSQL está instalado" -ForegroundColor Gray
    Write-Host "   2. psql está en el PATH o usa pgAdmin" -ForegroundColor Gray
    Write-Host "   3. El servicio PostgreSQL está corriendo" -ForegroundColor Gray
    exit 1
} finally {
    # Limpiar variable de entorno
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Write-Host "`n✅ Verificación completada" -ForegroundColor Green
Write-Host "`n📝 Connection String configurada:" -ForegroundColor Cyan
Write-Host "   Host=$dbHost;Port=$dbPort;Username=$dbUser;Password=$dbPassword;Database=$dbName;SSL Mode=Disable" -ForegroundColor Gray

