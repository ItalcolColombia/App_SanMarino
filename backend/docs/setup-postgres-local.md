# 🐘 Configuración PostgreSQL Local (Sin Docker)

## 📥 Instalación de PostgreSQL

### Opción 1: Instalador Oficial (Recomendado)
1. **Descargar PostgreSQL 15**:
   - Ve a: https://www.postgresql.org/download/windows/
   - Descarga PostgreSQL 15.x para Windows
   - Ejecuta el instalador como administrador

2. **Configuración durante la instalación**:
   - **Puerto**: 5432 (por defecto)
   - **Contraseña del superusuario**: `dev123456`
   - **Locale**: Spanish, Colombia o English, United States
   - **Componentes**: PostgreSQL Server, pgAdmin 4, Command Line Tools

### Opción 2: Chocolatey (Más rápido)
```powershell
# Ejecutar como administrador
choco install postgresql --params '/Password:dev123456'
```

### Opción 3: Scoop
```powershell
scoop bucket add main
scoop install postgresql
```

## 🔧 Configuración Post-Instalación

### 1. Crear Base de Datos de Desarrollo
```sql
-- Conectar como postgres (usar pgAdmin o psql)
CREATE DATABASE sanmarinoapp_dev;
CREATE USER sanmarino_dev WITH PASSWORD 'dev123456';
GRANT ALL PRIVILEGES ON DATABASE sanmarinoapp_dev TO sanmarino_dev;
```

### 2. Verificar Conexión
```bash
# Desde Command Prompt o PowerShell
psql -h localhost -p 5432 -U postgres -d sanmarinoapp_dev
# Contraseña: dev123456
```

### 3. Configurar Variables de Entorno (Opcional)
```batch
set PGHOST=localhost
set PGPORT=5432
set PGUSER=postgres
set PGPASSWORD=dev123456
set PGDATABASE=sanmarinoapp_dev
```

## 🚀 Ejecutar Backend

### Método 1: Script Automático
```batch
# Desde el directorio frontend
start-backend-local.bat
```

### Método 2: Manual
```batch
cd ..\backend\src\ZooSanMarino.API
set ConnectionStrings__ZooSanMarinoContext=Host=localhost;Port=5432;Database=sanmarinoapp_dev;Username=postgres;Password=dev123456;SSL Mode=Disable;Timeout=30;Command Timeout=60
set ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

### Método 3: PowerShell
```powershell
cd ..\backend\src\ZooSanMarino.API
$env:ConnectionStrings__ZooSanMarinoContext="Host=localhost;Port=5432;Database=sanmarinoapp_dev;Username=postgres;Password=dev123456;SSL Mode=Disable;Timeout=30;Command Timeout=60"
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run
```

## 🔍 Verificación

### 1. Backend Funcionando
- El backend debería iniciar en: `http://localhost:5002`
- Swagger disponible en: `http://localhost:5002/swagger`

### 2. Base de Datos Conectada
- Las migraciones se ejecutarán automáticamente
- Las tablas se crearán en `sanmarinoapp_dev`

### 3. Frontend Funcionando
```bash
# En el directorio frontend
ng serve --port 4200
```
- Frontend disponible en: `http://localhost:4200`
- Dashboard debería mostrar datos

## 🛠️ Solución de Problemas

### Error: "password authentication failed"
```sql
-- Cambiar método de autenticación
ALTER USER postgres PASSWORD 'dev123456';
```

### Error: "database does not exist"
```sql
-- Crear la base de datos manualmente
CREATE DATABASE sanmarinoapp_dev;
```

### Error: "connection refused"
```bash
# Verificar que PostgreSQL esté corriendo
net start postgresql-x64-15
# o
services.msc (buscar PostgreSQL)
```

### Puerto 5432 ocupado
```bash
# Verificar qué proceso usa el puerto
netstat -ano | findstr :5432
# Cambiar puerto en connection string si es necesario
```

## 📊 Herramientas Útiles

### pgAdmin 4
- **URL**: http://localhost:5050 (si se instaló)
- **Usuario**: postgres
- **Contraseña**: dev123456

### Línea de Comandos
```bash
# Conectar a la base de datos
psql -h localhost -U postgres -d sanmarinoapp_dev

# Ver tablas
\dt

# Ver datos de una tabla
SELECT * FROM "Users" LIMIT 5;

# Salir
\q
```

## 🎯 Resultado Esperado

Una vez completada la configuración:
1. ✅ PostgreSQL corriendo en puerto 5432
2. ✅ Base de datos `sanmarinoapp_dev` creada
3. ✅ Backend conectado y funcionando
4. ✅ Frontend mostrando datos del dashboard
5. ✅ APIs respondiendo correctamente
