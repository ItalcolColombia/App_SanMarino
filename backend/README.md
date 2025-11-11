backend/
├── ZooSanMarino.sln
├── global.json              ← (opcional) fija SDK .NET 8
├── docker-compose.yml
├── src/
│   ├── ZooSanMarino.Domain/         ← Lógica de negocio pura (Entidades, VO, interfaces)
│   │   ├── Entities/
│   │   └── Interfaces/
│   ├── ZooSanMarino.Application/    ← Casos de uso, DTOs, puertos secundarios
│   │   ├── DTOs/
│   │   └── UseCases/
│   ├── ZooSanMarino.Infrastructure/ ← Adaptadores (EF Core, Postgres, repositorios)
│   │   ├── Persistence/
│   │   └── Repositories/
│   └── ZooSanMarino.API/            ← Web API (Program.cs, Controllers o Minimal API)
│       ├── Controllers/
│       └── DTOs/
└── tests/
    ├── ZooSanMarino.Domain.Tests/
    └── ZooSanMarino.Application.Tests/


## Actualizar la base de datos 

dotnet ef migrations add AddSeguimientoLoteLevante  -- nombre que tendra la migracion 
dotnet ef database update   --- actualizara la base de datos

## migrar desde la carpeta de infrastructure
dotnet ef migrations add AddSeguimientoLoteLevante --project ZooSanMarino.Infrastructure --startup-project ZooSanMarino.API

## Aplicar migracion 
dotnet ef database update --project ZooSanMarino.Infrastructure --startup-project ZooSanMarino.API



ZooSanMarino – Backend (API + EF Core)
===========================================

API .NET (Clean Architecture)
- ZooSanMarino.API – endpoints / Swagger (startup project)
- ZooSanMarino.Infrastructure – EF Core, DbContext, Migrations, Services
- ZooSanMarino.Application – DTOs, Interfaces
- ZooSanMarino.Domain – Entidades

DB recomendada: PostgreSQL (Npgsql)
Migraciones: se guardan en 'Infrastructure' y se ejecutan usando la API como startup.

------------------------------------------------------------
1) Requisitos
------------------------------------------------------------
- .NET SDK 8.x
- PostgreSQL 13+
- dotnet-ef CLI:
  dotnet tool install --global dotnet-ef
  dotnet tool update --global dotnet-ef   (si ya lo tenías)

------------------------------------------------------------
2) Estructura esperada
------------------------------------------------------------
/src
  /ZooSanMarino.API
  /ZooSanMarino.Infrastructure
  /ZooSanMarino.Application
  /ZooSanMarino.Domain

------------------------------------------------------------
3) Cadena de conexión
------------------------------------------------------------
En src/ZooSanMarino.API/appsettings.Development.json:
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=zoo_sanmarino;Username=postgres;Password=postgres;"
  }
}

En Program.cs (API):
builder.Services.AddDbContext<ZooSanMarinoContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

------------------------------------------------------------
4) Restaurar y compilar
------------------------------------------------------------
dotnet restore
dotnet build

------------------------------------------------------------
5) Migraciones de EF Core (migraciones en Infrastructure)
------------------------------------------------------------
Ejecutar desde: src/ZooSanMarino.API

5.1 Crear nueva migración
dotnet ef migrations add <nombre_migracion>   --project ../ZooSanMarino.Infrastructure   --startup-project .   --context ZooSanMarinoContext

Ejemplo:
dotnet ef migrations add add_mixtas_y_pesoMixto_a_lote_reproductoras   --project ../ZooSanMarino.Infrastructure   --startup-project .   --context ZooSanMarinoContext

5.2 Aplicar migraciones a la base
dotnet ef database update   --project ../ZooSanMarino.Infrastructure   --startup-project .   --context ZooSanMarinoContext

5.3 Utilidades
- Listar migraciones
  dotnet ef migrations list     --project ../ZooSanMarino.Infrastructure     --startup-project .     --context ZooSanMarinoContext

- Revertir a una migración previa
  dotnet ef database update <NombreMigracion>     --project ../ZooSanMarino.Infrastructure     --startup-project .     --context ZooSanMarinoContext

- Quitar la última migración (si NO está aplicada):
  dotnet ef migrations remove     --project ../ZooSanMarino.Infrastructure     --startup-project .     --context ZooSanMarinoContext

(Alternativa: correr desde Infrastructure usando --startup-project ../ZooSanMarino.API)

------------------------------------------------------------
6) Levantar la API (Development)
------------------------------------------------------------
cd src/ZooSanMarino.API
dotnet run
# o con hot reload:
dotnet watch run

Swagger: https://localhost:<puerto>/swagger

------------------------------------------------------------
7) Endpoints de prueba (curl)
------------------------------------------------------------
Crear 1 Lote Reproductora
curl -X POST https://localhost:7243/api/LoteReproductora  -H "Content-Type: application/json"  -d '{
  "loteId":"L001","reproductoraId":"Sanmarino-A",
  "nombreLote":"L432","fechaEncasetamiento":"2025-08-22T00:00:00Z",
  "m":900,"h":1100,"mixtas":0,"mortCajaH":0,"mortCajaM":0,"unifH":0,"unifM":0,
  "pesoInicialM":38.5,"pesoInicialH":36.9
 }'

Crear varias (bulk)
curl -X POST https://localhost:7243/api/LoteReproductora/bulk  -H "Content-Type: application/json"  -d '[
  {
    "loteId":"L001","reproductoraId":"Sanmarino-A",
    "nombreLote":"L432","fechaEncasetamiento":"2025-08-22T00:00:00Z",
    "m":900,"h":1100,"mixtas":0,"mortCajaH":0,"mortCajaM":0,"unifH":0,"unifM":0,
    "pesoInicialM":38.5,"pesoInicialH":36.9
  },
  {
    "loteId":"L001","reproductoraId":"Sanmarino-B",
    "nombreLote":"L432","fechaEncasetamiento":"2025-08-22T00:00:00Z",
    "m":950,"h":1000,"mixtas":0,"mortCajaH":0,"mortCajaM":0,"unifH":0,"unifM":0,
    "pesoInicialM":38.2,"pesoInicialH":36.4
  }
 ]'

Listar por lote
curl "https://localhost:7243/api/LoteReproductora?loteId=L001"

------------------------------------------------------------
8) Flujo típico al cambiar el modelo
------------------------------------------------------------
1. Editar entidades (Domain) y configuraciones (Infrastructure/Configurations).
2. Crear migración (5.1).
3. Aplicar migraciones (5.2).
4. Ejecutar la API y validar en Swagger/curl.

------------------------------------------------------------
9) Errores comunes
------------------------------------------------------------
A) Mismatch de proyectos (migrations assembly):
   Usa los flags --project (Infrastructure) y --startup-project (API).

B) PendingModelChangesWarning al 'database update':
   Crea la migración primero (5.1) y luego vuelve a ejecutar 'database update'.

C) Fallo de conexión:
   Verifica cadena de conexión y que PostgreSQL esté activo.

D) SQL Server:
   Cambia a UseSqlServer(...), instala Microsoft.EntityFrameworkCore.SqlServer,
   ajusta la cadena y tipos si aplica.

------------------------------------------------------------
10) (Opcional) Migraciones dentro de la API
------------------------------------------------------------
En Program.cs:
builder.Services.AddDbContext<ZooSanMarinoContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"),
        b => b.MigrationsAssembly("ZooSanMarino.API")));

Luego puedes ejecutar:
dotnet ef migrations add <nombre> --context ZooSanMarinoContext
dotnet ef database update --context ZooSanMarinoContext

(Recomendado mantener migraciones en Infrastructure).


# 1) Asegúrate del cambio GalponId a string en PlanGramajeGalpon y de tener todas las Configuration
dotnet build

# 2) Genera migración con todos los cambios pendientes
dotnet ef migrations add Align_Domain_20250901 -p backend/src/ZooSanMarino.Infrastructure -s backend/src/ZooSanMarino.API

# 3) Aplica migración
dotnet ef database update -s backend/src/ZooSanMarino.API

# 4) Corre la API
cd backend/src/ZooSanMarino.API
dotnet run

Cómo ejecutarlo despliegue a aws back

Ejemplo estándar (con build normal):

.\deploy-ecs.ps1 -Profile sanmarino -Region us-east-2 `
  -Cluster sanmarino-cluster -Service sanmarino-api-svc `
  -Family sanmarino-backend -Container api `
  -EcrUri 021891592771.dkr.ecr.us-east-2.amazonaws.com/sanmarino-backend

---

## 🚀 Despliegue AWS ECS - NUEVO AWS (Actualizado Oct 2025)

### ✅ Estado Actual
**Backend desplegado exitosamente en:** Account 196080479890, Región us-east-2

- **Cluster**: devSanmarinoZoo
- **Service**: sanmarino-back-task-service-75khncfa
- **Task Definition**: sanmarino-back-task:4
- **Puerto**: 5002
- **Estado**: ✅ OPERATIVO

### 🌐 Acceso Actual
**http://3.145.143.253:5002**

> ⚠️ La IP puede cambiar. Para IP estable, configurar Load Balancer.

### 📚 Documentación de Despliegue
Ver archivos en `backend/documentacion/`:
- `DESPLIEGUE_EXITOSO_AWS.md` - Documentación del despliegue exitoso
- `INSTRUCCIONES_DESPLIEGUE.md` - Instrucciones detalladas
- `REQUISITOS_MIGRACION_AWS_NUEVO.md` - Requisitos de migración
- `ESTADO_CONFIGURACION_AWS.md` - Estado de configuración AWS

### 🔧 Script de Despliegue Automatizado
```bash
# Despliegue automatizado (bash)
cd backend/scripts
./deploy-backend-ecs.sh

# O usar PowerShell
./scripts/deploy-new-aws.ps1
```

### 📋 Configuración AWS Actual
```bash
Account ID: 196080479890
Región: us-east-2
Cluster: devSanmarinoZoo
ECR: 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend
```
