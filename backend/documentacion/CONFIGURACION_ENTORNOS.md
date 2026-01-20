# 🔧 Configuración de Entornos - Desarrollo vs Producción

## 📋 Cómo Funciona el Sistema de Configuración

El backend de .NET Core carga la configuración en este orden (el último sobrescribe al anterior):

1. **`appsettings.json`** - Configuración base (PRODUCCIÓN)
2. **`appsettings.{Environment}.json`** - Configuración específica del entorno (sobrescribe la base)
3. **Variables de entorno** - Sobrescriben los archivos
4. **Archivos `.env`** - Si existen, también pueden sobrescribir

## 🏠 Desarrollo Local (Development)

### Configuración Actual:
- **Archivo:** `appsettings.Development.json`
- **Base de datos:** `sanmarinoapp_local`
- **Host:** `localhost`
- **Puerto:** `5433`
- **Usuario:** `postgres`
- **Password:** `123456789`

### Cómo Ejecutar:
```powershell
cd backend\src\ZooSanMarino.API
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --launch-profile http
```

O usa el script:
```powershell
.\backend\run-dev.ps1
```

### ✅ Resultado:
- Usa `appsettings.Development.json`
- Se conecta a PostgreSQL local en `localhost:5433`
- Base de datos: `sanmarinoapp_local`

## 🚀 Producción (Production)

### Configuración Actual:
- **Archivo:** `appsettings.json`
- **Base de datos:** `sanmarinoappprod`
- **Host:** `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- **Puerto:** `5432`
- **Usuario:** `repropesa01`
- **SSL:** Requerido

### Cómo Ejecutar/Compilar:
```powershell
# En producción, NO configures ASPNETCORE_ENVIRONMENT o configúralo como "Production"
$env:ASPNETCORE_ENVIRONMENT="Production"  # O simplemente no lo configures
dotnet build -c Release
dotnet publish -c Release
```

### ✅ Resultado:
- Usa `appsettings.json` (no carga Development)
- Se conecta a AWS RDS
- Base de datos: `sanmarinoappprod`

## ⚠️ IMPORTANTE: Archivos .env

### Para Desarrollo Local:
- ✅ `backend/src/ZooSanMarino.API/.env` puede existir con configuración local
- ❌ `backend/.env` NO debe existir (fue eliminado para evitar conflictos)

### Para Producción:
- ❌ NO incluyas archivos `.env` en el despliegue
- ✅ Usa variables de entorno del sistema o configuración de AWS/ECS

## 📝 Resumen de Archivos

| Archivo | Entorno | Base de Datos | Host |
|---------|---------|---------------|------|
| `appsettings.json` | Production | `sanmarinoappprod` | AWS RDS |
| `appsettings.Development.json` | Development | `sanmarinoapp_local` | localhost:5433 |

## 🔍 Verificación

### En Desarrollo:
Revisa los logs, debe mostrar:
```
✅ localhost:5433
✅ sanmarinoapp_local
```

### En Producción:
Revisa los logs, debe mostrar:
```
✅ reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com
✅ sanmarinoappprod
```

## 🛠️ Troubleshooting

### Si en desarrollo sigue conectando a AWS:
1. Verifica que `ASPNETCORE_ENVIRONMENT=Development`
2. Verifica que `backend/.env` NO exista
3. Verifica que `appsettings.Development.json` tenga la configuración correcta

### Si en producción conecta a localhost:
1. Verifica que `ASPNETCORE_ENVIRONMENT=Production` o no esté configurado
2. Verifica que no haya archivos `.env` en el despliegue
3. Verifica que `appsettings.json` tenga la configuración de AWS


