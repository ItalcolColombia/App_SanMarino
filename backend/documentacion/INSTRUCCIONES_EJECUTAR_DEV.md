# 🚀 Instrucciones para Ejecutar el Backend en Desarrollo Local

## ⚠️ PROBLEMA RESUELTO

El archivo `backend/.env` que contenía la conexión a AWS ha sido **eliminado**. Ahora el backend usará la configuración local.

## ✅ Pasos para Ejecutar

### Opción 1: Usar el Script (Recomendado)

```powershell
cd "C:\Users\SAN MARINO\Documents\App_SanMarino_intalcol\App_SanMarino"
.\backend\run-dev.ps1
```

### Opción 2: Manual

```powershell
cd backend\src\ZooSanMarino.API
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --launch-profile http
```

## 🔍 Verificación

Después de ejecutar, revisa los logs del backend. Debes ver:

✅ **CORRECTO:**
- `localhost:5433`
- `sanmarinoapp_local`
- Sin errores de conexión

❌ **INCORRECTO (si ves esto, hay un problema):**
- `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- `sanmarinoappdev`
- Errores de "No such host is known"

## 📋 Configuración Actual

- **Puerto PostgreSQL:** 5433
- **Base de datos:** sanmarinoapp_local
- **Usuario:** postgres
- **Password:** 123456789
- **Host:** localhost

## 🛠️ Si Aún Hay Problemas

1. Verifica que PostgreSQL esté corriendo en el puerto 5433
2. Verifica que la base de datos `sanmarinoapp_local` exista
3. Asegúrate de que `backend/.env` NO exista (debe estar eliminado)
4. Verifica que `backend/src/ZooSanMarino.API/.env` tenga la configuración correcta

## 📝 Archivos de Configuración

El backend carga la configuración en este orden (el último sobrescribe):

1. `appsettings.json` (producción - AWS)
2. `appsettings.Development.json` (desarrollo - local) ✅
3. Variables de entorno
4. Archivos `.env` (si existen)

**IMPORTANTE:** El archivo `backend/.env` ha sido eliminado para evitar conflictos.


