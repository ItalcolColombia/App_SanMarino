# 🏗️ San Marino App

Aplicación web completa para gestión avícola con arquitectura moderna.

## 📁 Estructura del Proyecto

```
App_SanMarino/
├── backend/          # API .NET Core (Clean Architecture)
│   ├── deploy/       # Configuraciones y scripts de despliegue AWS
│   ├── docs/         # Documentación técnica
│   ├── scripts/      # Scripts de utilidad y despliegue
│   ├── sql/          # Scripts SQL y migraciones
│   ├── src/          # Código fuente del backend
│   └── tests/        # Tests unitarios
│
├── db/               # Base de datos
│   └── *.sql         # Backups y scripts SQL
│
├── frontend/         # Aplicación Angular
│   ├── deploy/       # Configuraciones de despliegue AWS
│   ├── scripts/      # Scripts de utilidad
│   └── src/          # Código fuente del frontend
│
├── deploy-to-aws.ps1 # Script de despliegue (Windows)
├── deploy-to-aws.sh  # Script de despliegue (macOS/Linux)
├── docker-compose.yml # Configuración Docker para desarrollo
├── Makefile          # Comandos de desarrollo
└── README.md         # Este archivo
```

## 🚀 Inicio Rápido

### Prerrequisitos

- Docker Desktop
- .NET 8 SDK (para backend)
- Node.js 18+ y npm/yarn (para frontend)

### Desarrollo Local

```bash
# Levantar servicios con Docker
make up

# O manualmente
docker-compose up -d
```

### Despliegue a AWS

```bash
# macOS/Linux
./deploy-to-aws.sh

# Windows
.\deploy-to-aws.ps1
```

## 📚 Documentación

- **Despliegue**: Ver [README-DEPLOY.md](./README-DEPLOY.md)
- **Backend**: Ver [backend/README.md](./backend/README.md)
- **Frontend**: Ver [frontend/README.md](./frontend/README.md)
- **AWS Infrastructure**: Ver [backend/docs/aws-infrastructure/](./backend/docs/aws-infrastructure/)

## 🛠️ Comandos Útiles

```bash
# Ver ayuda
make help

# Levantar servicios
make up

# Detener servicios
make down

# Ver logs
make logs

# Reconstruir todo
make rebuild
```

## 🏛️ Arquitectura

- **Backend**: .NET 8, Clean Architecture, Entity Framework Core
- **Frontend**: Angular 17, Standalone Components, Tailwind CSS
- **Base de Datos**: PostgreSQL
- **Despliegue**: AWS ECS, Docker, CloudFront

## 📝 Licencia

Proyecto privado - San Marino
