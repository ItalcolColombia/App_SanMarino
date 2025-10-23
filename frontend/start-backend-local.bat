@echo off
echo 🚀 Iniciando Backend con Base de Datos Local
echo.

REM Configurar variables de entorno para desarrollo local
set ConnectionStrings__ZooSanMarinoContext=Host=localhost;Port=5432;Database=sanmarinoapp_dev;Username=postgres;Password=dev123456;SSL Mode=Disable;Timeout=30;Command Timeout=60
set ASPNETCORE_ENVIRONMENT=Development

echo 📊 Configuración de Base de Datos:
echo Host: localhost
echo Puerto: 5432  
echo Base de Datos: sanmarinoapp_dev
echo Usuario: postgres
echo.

echo 🔧 Navegando al directorio del backend...
cd /d "%~dp0..\backend\src\ZooSanMarino.API"

echo 🏃 Ejecutando el backend...
dotnet run

pause
