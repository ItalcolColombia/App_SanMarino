# Script para verificar conexión a la base de datos de desarrollo
Write-Host "🔍 Verificando conexión a PostgreSQL (Desarrollo)..." -ForegroundColor Yellow
Write-Host ""

# Configuración desde appsettings.Development.json
$dbHost = "localhost"
$dbPort = "5433"
$dbUser = "postgres"
$dbPassword = "123456789"
$dbName = "sanmarinoapp_local"

Write-Host "📋 Configuración:" -ForegroundColor Cyan
Write-Host "   Host: $dbHost" -ForegroundColor Gray
Write-Host "   Port: $dbPort" -ForegroundColor Gray
Write-Host "   User: $dbUser" -ForegroundColor Gray
Write-Host "   Database: $dbName" -ForegroundColor Gray
Write-Host ""

# 1. Verificar si el puerto está abierto
Write-Host "1️⃣ Verificando puerto $dbPort..." -ForegroundColor Cyan
$portTest = Test-NetConnection -ComputerName localhost -Port $dbPort -InformationLevel Quiet -WarningAction SilentlyContinue
if ($portTest) {
    Write-Host "   ✅ Puerto $dbPort está abierto" -ForegroundColor Green
} else {
    Write-Host "   ❌ Puerto $dbPort NO está abierto" -ForegroundColor Red
    Write-Host "   💡 Verifica que PostgreSQL esté corriendo" -ForegroundColor Yellow
    exit 1
}

# 2. Intentar conectar usando psql si está disponible
Write-Host ""
Write-Host "2️⃣ Intentando conectar con psql..." -ForegroundColor Cyan
$env:PGPASSWORD = $dbPassword

try {
    # Probar conexión al servidor PostgreSQL
    $serverTest = & psql -h $dbHost -p $dbPort -U $dbUser -d postgres -c "SELECT version();" -t 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Conexión al servidor PostgreSQL exitosa" -ForegroundColor Green
        $version = ($serverTest | Where-Object { $_ -match "PostgreSQL" } | Select-Object -First 1)
        if ($version) {
            Write-Host "   📊 Versión: $($version.Trim())" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ❌ Error al conectar al servidor: $serverTest" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ⚠️ psql no está disponible en PATH" -ForegroundColor Yellow
    Write-Host "   💡 Instala PostgreSQL o agrega psql al PATH" -ForegroundColor Yellow
}

# 3. Verificar si la base de datos existe
Write-Host ""
Write-Host "3️⃣ Verificando si la base de datos '$dbName' existe..." -ForegroundColor Cyan
try {
    $dbCheck = & psql -h $dbHost -p $dbPort -U $dbUser -d postgres -c "SELECT 1 FROM pg_database WHERE datname = '$dbName';" -t 2>&1
    if ($LASTEXITCODE -eq 0 -and ($dbCheck -match "1" -or $dbCheck.Trim() -eq "1")) {
        Write-Host "   ✅ La base de datos '$dbName' existe" -ForegroundColor Green
    } else {
        Write-Host "   ❌ La base de datos '$dbName' NO existe" -ForegroundColor Red
        Write-Host "   💡 Crear la base de datos con:" -ForegroundColor Yellow
        Write-Host "      psql -h $dbHost -p $dbPort -U $dbUser -d postgres -c `"CREATE DATABASE $dbName;`"" -ForegroundColor Gray
        exit 1
    }
} catch {
    Write-Host "   ⚠️ No se pudo verificar la base de datos" -ForegroundColor Yellow
}

# 4. Probar conexión directa a la base de datos
Write-Host ""
Write-Host "4️⃣ Probando conexión directa a la base de datos '$dbName'..." -ForegroundColor Cyan
try {
    $dbTest = & psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -c "SELECT current_database(), version();" -t 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Conexión a la base de datos '$dbName' exitosa" -ForegroundColor Green
        $dbInfo = ($dbTest | Where-Object { $_ -notmatch "^\s*$" } | Select-Object -First 1)
        if ($dbInfo) {
            Write-Host "   📊 Base de datos actual: $($dbInfo.Trim())" -ForegroundColor Gray
        }
    } else {
        Write-Host "   ❌ Error al conectar a la base de datos: $dbTest" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "   ⚠️ Error al probar conexión: $_" -ForegroundColor Yellow
}

# 5. Verificar tablas en la base de datos
Write-Host ""
Write-Host "5️⃣ Verificando tablas en la base de datos..." -ForegroundColor Cyan
try {
    $tables = & psql -h $dbHost -p $dbPort -U $dbUser -d $dbName -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" -t 2>&1
    if ($LASTEXITCODE -eq 0) {
        $tableCount = ($tables | Where-Object { $_ -match "^\d+$" } | Select-Object -First 1).Trim()
        if ($tableCount) {
            Write-Host "   ✅ Base de datos tiene $tableCount tabla(s) en el schema 'public'" -ForegroundColor Green
        } else {
            Write-Host "   ⚠️ Base de datos existe pero no tiene tablas (puede necesitar migraciones)" -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "   ⚠️ No se pudo verificar las tablas" -ForegroundColor Yellow
}

# 6. Verificar credenciales
Write-Host ""
Write-Host "6️⃣ Verificando credenciales..." -ForegroundColor Cyan
Write-Host "   Usuario: $dbUser" -ForegroundColor Gray
Write-Host "   Password: $(if ($dbPassword) { '***' } else { 'NO CONFIGURADA' })" -ForegroundColor Gray

Write-Host ""
Write-Host "✅ Verificación completa" -ForegroundColor Green
Write-Host ""
Write-Host "💡 Si todo está correcto pero el backend no conecta:" -ForegroundColor Yellow
Write-Host "   1. Verifica que ASPNETCORE_ENVIRONMENT=Development" -ForegroundColor Gray
Write-Host "   2. Reinicia el backend" -ForegroundColor Gray
Write-Host "   3. Revisa los logs del backend para más detalles" -ForegroundColor Gray

