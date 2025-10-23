# 🛠️ Guía de Instalación - ZooSanMarino Backend

## 🎯 Requisitos del Sistema

### Software Requerido

| Componente | Versión Mínima | Versión Recomendada | Notas |
|------------|----------------|---------------------|-------|
| **.NET SDK** | 9.0 | 9.0.301+ | Framework principal |
| **PostgreSQL** | 13.0 | 15.0+ | Base de datos |
| **Git** | 2.30+ | Última | Control de versiones |
| **IDE** | Visual Studio 2022 / VS Code | Última | Desarrollo |

### Herramientas Opcionales

- **Docker** (para contenedores)
- **pgAdmin** (administración de PostgreSQL)
- **Postman** (testing de APIs)
- **DBeaver** (cliente de base de datos universal)

## 📦 Instalación Paso a Paso

### 1. Clonar el Repositorio

```bash
# Clonar el proyecto
git clone [URL_DEL_REPOSITORIO]
cd backend

# Verificar la estructura
ls -la
```

**Estructura esperada:**
```
📁 backend/
├── 📁 src/
│   ├── 📁 ZooSanMarino.Domain/
│   ├── 📁 ZooSanMarino.Application/
│   ├── 📁 ZooSanMarino.Infrastructure/
│   └── 📁 ZooSanMarino.API/
├── 📁 tests/
├── 📁 documentacion/
├── 📄 .gitignore
├── 📄 README.md
└── 📄 ZooSanMarino.sln
```

### 2. Configurar Variables de Entorno

#### Crear archivo .env

```bash
# Copiar plantilla de ejemplo
cp .env.example .env

# Editar configuración
nano .env  # o usar tu editor preferido
```

#### Contenido del archivo .env

```env
# Configuración de Base de Datos
ZOO_CONN=Host=localhost;Port=5432;Database=zoosanmarino_dev;Username=postgres;Password=tu_password

# Configuración JWT
JWT_SECRET=tu_clave_secreta_muy_larga_y_segura_aqui_minimo_32_caracteres
JWT_ISSUER=ZooSanMarino
JWT_AUDIENCE=ZooSanMarino.API
JWT_EXPIRATION_HOURS=24

# Configuración de Entorno
ASPNETCORE_ENVIRONMENT=Development
PORT=5002

# Configuración de Logging
LOG_LEVEL=Information
```

### 3. Configurar PostgreSQL

#### Opción A: Instalación Local

```bash
# Ubuntu/Debian
sudo apt update
sudo apt install postgresql postgresql-contrib

# macOS (usando Homebrew)
brew install postgresql
brew services start postgresql

# Windows
# Descargar desde: https://www.postgresql.org/download/windows/
```

#### Opción B: Docker

```bash
# Ejecutar PostgreSQL en Docker
docker run --name zoosanmarino-postgres \
  -e POSTGRES_DB=zoosanmarino_dev \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=tu_password \
  -p 5432:5432 \
  -d postgres:15

# Verificar que esté ejecutándose
docker ps
```

#### Crear Base de Datos

```sql
-- Conectar a PostgreSQL
psql -h localhost -U postgres

-- Crear base de datos
CREATE DATABASE zoosanmarino_dev;

-- Crear usuario específico (opcional)
CREATE USER zoosanmarino WITH PASSWORD 'password_seguro';
GRANT ALL PRIVILEGES ON DATABASE zoosanmarino_dev TO zoosanmarino;

-- Salir
\q
```

### 4. Restaurar Dependencias

```bash
# Restaurar paquetes NuGet
dotnet restore

# Verificar que no hay errores
dotnet build
```

### 5. Configurar Entity Framework

#### Instalar herramientas EF (si no están instaladas)

```bash
# Instalar globalmente
dotnet tool install --global dotnet-ef

# Verificar instalación
dotnet ef --version
```

#### Ejecutar Migraciones

```bash
# Navegar al proyecto de Infrastructure
cd src/ZooSanMarino.Infrastructure

# Crear migración inicial (si no existe)
dotnet ef migrations add InitialCreate --startup-project ../ZooSanMarino.API

# Aplicar migraciones a la base de datos
dotnet ef database update --startup-project ../ZooSanMarino.API

# Verificar que las tablas se crearon
```

### 6. Configurar Datos Iniciales (Seed)

```bash
# Ejecutar desde el directorio raíz
cd ../../

# Ejecutar la API para aplicar seed data
cd src/ZooSanMarino.API
dotnet run
```

**Nota:** El seed data se ejecuta automáticamente al iniciar la aplicación en modo Development.

## 🚀 Ejecutar la Aplicación

### Modo Desarrollo

```bash
# Desde el directorio de la API
cd src/ZooSanMarino.API

# Ejecutar en modo desarrollo
dotnet run

# O con hot reload
dotnet watch run
```

### Verificar Instalación

1. **API ejecutándose:**
   ```
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: http://localhost:5002
   ```

2. **Swagger disponible:**
   - Abrir: `http://localhost:5002/swagger`
   - Verificar que aparecen todos los controladores

3. **Base de datos conectada:**
   - Verificar logs sin errores de conexión
   - Probar endpoint de health check (si existe)

## 🔧 Configuración del IDE

### Visual Studio 2022

#### Configuración de Proyectos de Inicio

1. **Clic derecho en la solución** → "Set Startup Projects"
2. **Seleccionar "Single startup project"**
3. **Elegir:** `ZooSanMarino.API`

#### Configuración de Debugging

```json
// launchSettings.json
{
  "profiles": {
    "ZooSanMarino.API": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "swagger",
      "applicationUrl": "http://localhost:5002",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Visual Studio Code

#### Extensiones Recomendadas

```bash
# Instalar extensiones
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.vscode-dotnet-runtime
code --install-extension humao.rest-client
code --install-extension ms-vscode.vscode-json
```

#### Configuración de Tasks (.vscode/tasks.json)

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "command": "dotnet",
      "type": "process",
      "args": ["build", "${workspaceFolder}/ZooSanMarino.sln"],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "run",
      "command": "dotnet",
      "type": "process",
      "args": ["run", "--project", "${workspaceFolder}/src/ZooSanMarino.API"],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

#### Configuración de Launch (.vscode/launch.json)

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch API",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/ZooSanMarino.API/bin/Debug/net9.0/ZooSanMarino.API.dll",
      "args": [],
      "cwd": "${workspaceFolder}/src/ZooSanMarino.API",
      "stopAtEntry": false,
      "serverReadyAction": {
        "action": "openExternally",
        "pattern": "\\bNow listening on:\\s+(https?://\\S+)"
      },
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

## 🧪 Verificar Instalación

### 1. Ejecutar Pruebas

```bash
# Ejecutar todas las pruebas
dotnet test

# Ejecutar pruebas con cobertura
dotnet test --collect:"XPlat Code Coverage"
```

### 2. Probar Endpoints

#### Health Check (si está implementado)

```bash
curl http://localhost:5002/health
```

#### Swagger UI

```bash
# Abrir en navegador
open http://localhost:5002/swagger
```

#### Endpoint de Prueba

```bash
# Probar endpoint público (si existe)
curl -X GET "http://localhost:5002/api/test" \
  -H "accept: application/json"
```

### 3. Verificar Base de Datos

```sql
-- Conectar a la base de datos
psql -h localhost -U postgres -d zoosanmarino_dev

-- Verificar tablas creadas
\dt

-- Verificar datos de ejemplo
SELECT COUNT(*) FROM usuarios;
SELECT COUNT(*) FROM lotes;

-- Salir
\q
```

## 🔍 Solución de Problemas Comunes

### Error: "Connection string not configured"

**Problema:** La cadena de conexión no está configurada correctamente.

**Solución:**
```bash
# Verificar archivo .env
cat .env | grep ZOO_CONN

# Verificar que PostgreSQL esté ejecutándose
sudo systemctl status postgresql  # Linux
brew services list | grep postgres  # macOS
```

### Error: "Could not load file or assembly"

**Problema:** Dependencias faltantes o versiones incorrectas.

**Solución:**
```bash
# Limpiar y restaurar
dotnet clean
dotnet restore
dotnet build
```

### Error: "Port already in use"

**Problema:** El puerto 5002 está siendo usado por otro proceso.

**Solución:**
```bash
# Encontrar proceso usando el puerto
lsof -i :5002  # macOS/Linux
netstat -ano | findstr :5002  # Windows

# Cambiar puerto en launchSettings.json o usar variable de entorno
export PORT=5003
dotnet run
```

### Error de Migraciones

**Problema:** Las migraciones no se aplican correctamente.

**Solución:**
```bash
# Eliminar base de datos y recrear
dotnet ef database drop --startup-project ../ZooSanMarino.API
dotnet ef database update --startup-project ../ZooSanMarino.API

# O aplicar migración específica
dotnet ef migrations list --startup-project ../ZooSanMarino.API
dotnet ef database update [MigrationName] --startup-project ../ZooSanMarino.API
```

## 📋 Checklist de Instalación

### ✅ Pre-requisitos
- [ ] .NET 9 SDK instalado
- [ ] PostgreSQL instalado y ejecutándose
- [ ] Git configurado
- [ ] IDE configurado

### ✅ Configuración
- [ ] Repositorio clonado
- [ ] Archivo .env creado y configurado
- [ ] Base de datos creada
- [ ] Dependencias restauradas

### ✅ Migraciones y Datos
- [ ] Migraciones aplicadas
- [ ] Datos iniciales cargados
- [ ] Conexión a BD verificada

### ✅ Ejecución
- [ ] API ejecutándose sin errores
- [ ] Swagger accesible
- [ ] Endpoints respondiendo
- [ ] Pruebas pasando

## 🚀 Próximos Pasos

Una vez completada la instalación:

1. **Revisar la [Documentación de API](api-liquidacion-tecnica.md)**
2. **Explorar [Arquitectura Hexagonal](arquitectura-hexagonal.md)**
3. **Probar [Importación de Excel](excel-import.md)**
4. **Configurar [Entorno de Producción](configuracion-entorno.md)**

---

**Última actualización:** Octubre 2024  
**Versión compatible:** .NET 9, PostgreSQL 15+  
**Tiempo estimado de instalación:** 30-45 minutos
