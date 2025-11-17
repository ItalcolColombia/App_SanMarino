# 🚀 Script de Configuración Automática - Entorno de Desarrollo
# Ejecutar como administrador

param(
    [switch]$InstallPostgreSQL,
    [switch]$SkipInstall,
    [string]$PostgreSQLPassword = "dev123456"
)

Write-Host "🚀 Configurando Entorno de Desarrollo San Marino" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green

# Función para verificar si se ejecuta como administrador
function Test-Administrator {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Función para instalar PostgreSQL con Chocolatey
function Install-PostgreSQLChoco {
    Write-Host "📦 Instalando PostgreSQL con Chocolatey..." -ForegroundColor Yellow
    
    # Verificar si Chocolatey está instalado
    if (!(Get-Command choco -ErrorAction SilentlyContinue)) {
        Write-Host "🍫 Instalando Chocolatey..." -ForegroundColor Yellow
        Set-ExecutionPolicy Bypass -Scope Process -Force
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
        iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
        refreshenv
    }
    
    # Instalar PostgreSQL
    choco install postgresql --params "/Password:$PostgreSQLPassword" -y
    refreshenv
}

# Función para configurar la base de datos
function Setup-Database {
    Write-Host "🐘 Configurando Base de Datos..." -ForegroundColor Yellow
    
    $env:PGPASSWORD = $PostgreSQLPassword
    
    # Esperar a que PostgreSQL esté listo
    Start-Sleep -Seconds 10
    
    # Crear base de datos de desarrollo
    $createDbScript = @"
CREATE DATABASE sanmarinoapp_dev;
CREATE USER sanmarino_dev WITH PASSWORD '$PostgreSQLPassword';
GRANT ALL PRIVILEGES ON DATABASE sanmarinoapp_dev TO sanmarino_dev;
"@
    
    try {
        $createDbScript | psql -h localhost -U postgres -d postgres
        Write-Host "✅ Base de datos creada exitosamente" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️  Error creando base de datos: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "💡 Puedes crearla manualmente con pgAdmin" -ForegroundColor Yellow
    }
}

# Función para configurar variables de entorno
function Set-EnvironmentVariables {
    Write-Host "🔧 Configurando Variables de Entorno..." -ForegroundColor Yellow
    
    $connectionString = "Host=localhost;Port=5432;Database=sanmarinoapp_dev;Username=postgres;Password=$PostgreSQLPassword;SSL Mode=Disable;Timeout=30;Command Timeout=60"
    
    # Variables para la sesión actual
    $env:ConnectionStrings__ZooSanMarinoContext = $connectionString
    $env:ASPNETCORE_ENVIRONMENT = "Development"
    
    Write-Host "✅ Variables de entorno configuradas" -ForegroundColor Green
}

# Función para verificar dependencias
function Test-Dependencies {
    Write-Host "🔍 Verificando Dependencias..." -ForegroundColor Yellow
    
    $dependencies = @{
        "dotnet" = "SDK de .NET"
        "node" = "Node.js"
        "ng" = "Angular CLI"
    }
    
    $missing = @()
    
    foreach ($dep in $dependencies.Keys) {
        if (!(Get-Command $dep -ErrorAction SilentlyContinue)) {
            $missing += $dependencies[$dep]
            Write-Host "❌ $($dependencies[$dep]) no encontrado" -ForegroundColor Red
        } else {
            Write-Host "✅ $($dependencies[$dep]) encontrado" -ForegroundColor Green
        }
    }
    
    if ($missing.Count -gt 0) {
        Write-Host "⚠️  Dependencias faltantes: $($missing -join ', ')" -ForegroundColor Yellow
        Write-Host "💡 Instala las dependencias faltantes antes de continuar" -ForegroundColor Yellow
        return $false
    }
    
    return $true
}

# Función para iniciar servicios
function Start-Services {
    Write-Host "🚀 Iniciando Servicios..." -ForegroundColor Yellow
    
    # Verificar si PostgreSQL está corriendo
    $pgService = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue
    if ($pgService) {
        if ($pgService.Status -ne "Running") {
            Start-Service $pgService.Name
            Write-Host "✅ PostgreSQL iniciado" -ForegroundColor Green
        } else {
            Write-Host "✅ PostgreSQL ya está corriendo" -ForegroundColor Green
        }
    } else {
        Write-Host "⚠️  Servicio PostgreSQL no encontrado" -ForegroundColor Yellow
    }
}

# Función para probar conexión
function Test-DatabaseConnection {
    Write-Host "🔌 Probando Conexión a Base de Datos..." -ForegroundColor Yellow
    
    $env:PGPASSWORD = $PostgreSQLPassword
    
    try {
        $result = psql -h localhost -U postgres -d sanmarinoapp_dev -c "SELECT version();" -t
        if ($result) {
            Write-Host "✅ Conexión a base de datos exitosa" -ForegroundColor Green
            return $true
        }
    }
    catch {
        Write-Host "❌ Error conectando a base de datos: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
    
    return $false
}

# Función principal
function Main {
    if (!(Test-Administrator)) {
        Write-Host "❌ Este script debe ejecutarse como Administrador" -ForegroundColor Red
        Write-Host "💡 Haz clic derecho en PowerShell y selecciona 'Ejecutar como administrador'" -ForegroundColor Yellow
        exit 1
    }
    
    Write-Host ""
    
    # Verificar dependencias
    if (!(Test-Dependencies)) {
        exit 1
    }
    
    Write-Host ""
    
    # Instalar PostgreSQL si se solicita
    if ($InstallPostgreSQL -and !$SkipInstall) {
        Install-PostgreSQLChoco
        Write-Host ""
    }
    
    # Iniciar servicios
    Start-Services
    Write-Host ""
    
    # Configurar base de datos
    if (!$SkipInstall) {
        Setup-Database
        Write-Host ""
    }
    
    # Configurar variables de entorno
    Set-EnvironmentVariables
    Write-Host ""
    
    # Probar conexión
    if (Test-DatabaseConnection) {
        Write-Host ""
        Write-Host "🎉 ¡Configuración Completada Exitosamente!" -ForegroundColor Green
        Write-Host ""
        Write-Host "📋 Próximos Pasos:" -ForegroundColor Cyan
        Write-Host "1. Ejecutar Backend:" -ForegroundColor White
        Write-Host "   cd ..\backend\src\ZooSanMarino.API" -ForegroundColor Gray
        Write-Host "   dotnet run" -ForegroundColor Gray
        Write-Host ""
        Write-Host "2. Ejecutar Frontend:" -ForegroundColor White
        Write-Host "   ng serve --port 4200" -ForegroundColor Gray
        Write-Host ""
        Write-Host "3. Abrir en navegador:" -ForegroundColor White
        Write-Host "   http://localhost:4200" -ForegroundColor Gray
        Write-Host ""
    } else {
        Write-Host ""
        Write-Host "❌ Error en la configuración" -ForegroundColor Red
        Write-Host "💡 Revisa el archivo setup-postgres-local.md para configuración manual" -ForegroundColor Yellow
    }
}

# Ejecutar función principal
Main
