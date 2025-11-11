# 🚀 Despliegue Automatizado a AWS ECS

Este proyecto incluye scripts automatizados para desplegar tanto el **backend** como el **frontend** a AWS ECS con un solo comando.

## 📋 Pre-requisitos

Antes de ejecutar el despliegue, asegúrate de tener:

1. **Docker Desktop** instalado y corriendo
2. **AWS CLI** instalado y configurado (`aws configure`)
3. **Credenciales AWS** con permisos para:
   - ECR (Elastic Container Registry)
   - ECS (Elastic Container Service)
   - EC2 (para Security Groups y Networking)

## 🎯 Despliegue Completo (Backend + Frontend)

### Opción 1: Script Bash (macOS/Linux)

```bash
# Desde la raíz del proyecto
./deploy-to-aws.sh
```

### Opción 2: Script PowerShell (Windows)

```powershell
# Desde la raíz del proyecto
.\deploy-to-aws.ps1
```

## 📝 Proceso del Script

El script automatizado realiza lo siguiente:

1. **Verificación de Pre-requisitos:**
   - ✅ Verifica que Docker esté instalado y corriendo
   - ✅ Verifica que AWS CLI esté configurado
   - ✅ Valida credenciales AWS

2. **Menú Interactivo:**
   - Selecciona qué desplegar:
     - Opción 1: Backend + Frontend (completo)
     - Opción 2: Solo Backend
     - Opción 3: Solo Frontend

3. **Despliegue:**
   - Ejecuta los scripts individuales de backend/frontend
   - Construye y pushea imágenes Docker a ECR
   - Actualiza servicios ECS
   - Espera a que los servicios se estabilicen

4. **Resumen:**
   - Muestra el estado del despliegue
   - Proporciona URLs de acceso

## 🔧 Despliegue Individual

Si prefieres desplegar manualmente cada componente:

### Backend

```bash
cd backend
./scripts/deploy-backend-ecs.sh
```

### Frontend

```bash
cd frontend
./scripts/deploy-frontend-ecs.sh
```

## 📊 Configuración AWS

**Account ID:** `196080479890`  
**Región:** `us-east-2`  
**Cluster:** `devSanmarinoZoo`

**Backend:**
- Service: `sanmarino-back-task-service-75khncfa`
- Task Definition: `sanmarino-back-task`
- ECR: `196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend`

**Frontend:**
- Service: `sanmarino-front-task-service-zp2f403l`
- Task Definition: `sanmarino-front-task`
- ECR: `196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend`

## 🌐 URLs de Acceso

Después del despliegue, podrás acceder a:

- **Frontend (ALB):** `http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com`
- **API Backend:** `http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api`
- **Swagger:** `http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/swagger`

## ⏱️ Tiempo Estimado

- **Backend:** ~2-3 minutos
- **Frontend:** ~3-5 minutos (incluye build de Angular)
- **Total (ambos):** ~5-8 minutos

## 🔍 Verificación Post-Despliegue

### Verificar Estado de los Servicios

```bash
# Backend
aws ecs describe-services --cluster devSanmarinoZoo \
  --services sanmarino-back-task-service-75khncfa \
  --region us-east-2

# Frontend
aws ecs describe-services --cluster devSanmarinoZoo \
  --services sanmarino-front-task-service-zp2f403l \
  --region us-east-2
```

### Ver Logs

```bash
# Backend
aws logs tail /ecs/sanmarino-back-task --follow --region us-east-2

# Frontend
aws logs tail /ecs/sanmarino-front-task --follow --region us-east-2
```

## 🐛 Troubleshooting

### Error: "Docker daemon no está corriendo"
- Abre Docker Desktop y espera a que inicie completamente
- Verifica con: `docker info`

### Error: "Credenciales AWS no configuradas"
- Ejecuta: `aws configure`
- Ingresa tu Access Key ID, Secret Access Key, región (us-east-2)

### Error: "Fallo en login a ECR"
- Verifica que tengas permisos ECR en tu cuenta AWS
- Asegúrate de que el repositorio ECR exista

### Error: "Servicio no se estabiliza"
- Revisa los logs de CloudWatch para ver errores
- Verifica que las Task Definitions estén correctas
- Asegúrate de que los Security Groups permitan el tráfico necesario

## 📚 Scripts Individuales

Los scripts individuales están ubicados en:

- **Backend:** `backend/scripts/deploy-backend-ecs.sh`
- **Frontend:** `frontend/scripts/deploy-frontend-ecs.sh`

Estos scripts pueden ejecutarse independientemente si solo necesitas actualizar un componente.

## 🔄 Actualización de Conexión a RDS

La Task Definition del backend (`backend/documentacion/ecs-taskdef-new-aws.json`) contiene la configuración de conexión a RDS. Si necesitas actualizar la conexión:

1. Edita `backend/documentacion/ecs-taskdef-new-aws.json`
2. Actualiza la variable de entorno `ConnectionStrings__ZooSanMarinoContext`
3. Ejecuta el despliegue nuevamente

## ✅ Checklist Pre-Despliegue

Antes de cada despliegue, verifica:

- [ ] Docker Desktop está corriendo
- [ ] AWS CLI está configurado
- [ ] Cambios de código están commiteados (opcional)
- [ ] Task Definitions tienen la configuración correcta
- [ ] Connection Strings son correctos (backend)

---

**Nota:** El script principal (`deploy-to-aws.sh` o `deploy-to-aws.ps1`) maneja toda la lógica de despliegue automáticamente. Solo necesitas ejecutarlo y seguir las instrucciones en pantalla.

