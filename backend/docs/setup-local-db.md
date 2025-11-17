# 🐘 Configuración de Base de Datos Local

## 🚀 Inicio Rápido (Automático)

### Opción 1: Script Automático (Recomendado)
```powershell
# Ejecutar como Administrador en PowerShell
.\setup-dev-environment.ps1 -InstallPostgreSQL
```

### Opción 2: Solo Configuración (PostgreSQL ya instalado)
```powershell
# Ejecutar como Administrador en PowerShell
.\setup-dev-environment.ps1 -SkipInstall
```

## 🛠️ Configuración Manual

### 1. Instalar PostgreSQL Local
Ver archivo: `setup-postgres-local.md` para instrucciones detalladas

### 2. Verificar Conexión
```bash
# Verificar que el contenedor esté corriendo
docker ps | grep sanmarino-postgres-dev

# Probar conexión
docker exec -it sanmarino-postgres-dev psql -U postgres -d sanmarinoapp -c "SELECT version();"
```

### 3. Ejecutar Backend
```bash
cd ../backend/src/ZooSanMarino.API
dotnet run --environment Development
```

## 🔧 Configuración

### Credenciales de Desarrollo:
- **Host**: localhost
- **Puerto**: 5433
- **Base de Datos**: sanmarinoapp  
- **Usuario**: postgres
- **Contraseña**: dev123456

### Connection String:
```
Host=localhost;Port=5433;Database=sanmarinoapp;Username=postgres;Password=dev123456;SSL Mode=Disable;Timeout=30;Command Timeout=60
```

## 🛠️ Comandos Útiles

### Detener Base de Datos:
```bash
docker-compose -f docker-compose.dev.yml down
```

### Reiniciar con Datos Limpios:
```bash
docker-compose -f docker-compose.dev.yml down -v
docker-compose -f docker-compose.dev.yml up -d postgres-dev
```

### Acceder a PostgreSQL:
```bash
docker exec -it sanmarino-postgres-dev psql -U postgres -d sanmarinoapp
```

### Ver Logs:
```bash
docker logs sanmarino-postgres-dev -f
```

## 🔍 Solución de Problemas

### Si el puerto 5433 está ocupado:
```bash
# Verificar qué proceso usa el puerto
netstat -ano | findstr :5433

# Cambiar el puerto en docker-compose.dev.yml
# Ejemplo: "5434:5432"
```

### Si hay problemas de permisos:
```bash
# Reiniciar Docker Desktop
# O ejecutar como administrador
```

## 📊 Migración de Datos

Una vez que la base de datos local esté funcionando, el backend automáticamente:
1. ✅ Ejecutará las migraciones de Entity Framework
2. ✅ Creará las tablas necesarias
3. ✅ Poblará datos iniciales (si está configurado)

## 🌐 Acceso al Frontend

Con la base de datos local funcionando:
```bash
# En el directorio frontend
ng serve --port 4200
```

El dashboard debería mostrar datos correctamente en:
`http://localhost:4200`
