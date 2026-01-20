# 🚀 Instrucciones: Mover Backend a us-east-1

## ✅ Ya Completado

1. ✅ **Cluster ECS creado**: `sanmarino-cluster` en us-east-1
2. ✅ **Repositorios ECR creados**:
   - `sanmarino/zootecnia/granjas/backend` 
   - `sanmarino/zootecnia/granjas/frontend`
3. ✅ **Imágenes Docker copiadas**:
   - Backend: `196080479890.dkr.ecr.us-east-1.amazonaws.com/sanmarino/zootecnia/granjas/backend:latest`
   - Frontend: `196080479890.dkr.ecr.us-east-1.amazonaws.com/sanmarino/zootecnia/granjas/frontend:latest`
4. ✅ **Security Group creado**: `sg-0c6a91db2ba4b872f` (ECR venues)
   - Puerto 80: ✅
   - Puerto 5002: ✅

---

## 📋 Pasos Restantes (Requieren Administrador)

### Paso 1: Configurar Security Group del RDS

El RDS en us-east-1 necesita permitir conexiones desde ECS:

1. Ve a **EC2 Console** → **Security Groups** en **us-east-1**
2. Encuentra el Security Group del RDS: `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
3. Agrega Inbound Rule:
   - **Type**: PostgreSQL (o Custom TCP)
   - **Port**: 5432
   - **Source**: Security Group `sg-0c6a91db2ba4b872f` (no IP específica)
   - **Description**: Allow ECS tasks

### Paso 2: Registrar Task Definition

```bash
cd backend
aws ecs register-task-definition --cli-input-json file://ecs-taskdef-us-east-1.json --region us-east-1
```

### Paso 3: Crear ALB en us-east-1

```bash
# Crear Security Group para ALB
aws ec2 create-security-group --group-name sanmarino-alb-sg --description "ALB Security Group" --vpc-id vpc-03fb2b7e --region us-east-1

# Agregar reglas
aws ec2 authorize-security-group-ingress --group-id <ALB_SG_ID> --protocol tcp --port 80 --cidr 0.0.0.0/0 --region us-east-1
aws ec2 authorize-security-group-ingress --group-id <ALB_SG_ID> --protocol tcp --port 443 --cidr 0.0.0.0/0 --region us-east-1

# Crear ALB (usar subnets de diferentes AZs)
aws elbv2 create-load-balancer --name sanmarino-alb --subnets subnet-9a4dc9fc subnet-2dc7450c subnet-80f0a4cd --security-groups <ALB_SG_ID> --scheme internet-facing --type application --region us-east-1
```

### Paso 4: Crear Target Groups

```bash
VPC_ID="vpc-03fb2b7e"

# Backend Target Group
aws elbv2 create-target-group --name sanmarino-backend-tg --protocol HTTP --port 5002 --target-type ip --vpc-id $VPC_ID --health-check-path /health --health-check-protocol HTTP --region us-east-1

# Frontend Target Group
aws elbv2 create-target-group --name sanmarino-frontend-tg --protocol HTTP --port 80 --target-type ip --vpc-id $VPC_ID --health-check-path / --health-check-protocol HTTP --region us-east-1
```

### Paso 5: Crear Listeners y Reglas

```bash
ALB_ARN="<ALB_ARN>"
BACKEND_TG_ARN="<BACKEND_TG_ARN>"
FRONTEND_TG_ARN="<FRONTEND_TG_ARN>"

# Listener HTTP
aws elbv2 create-listener --load-balancer-arn $ALB_ARN --protocol HTTP --port 80 --default-actions Type=forward,TargetGroupArn=$FRONTEND_TG_ARN --region us-east-1

# Regla para /api
LISTENER_ARN="<LISTENER_ARN>"
aws elbv2 create-rule --listener-arn $LISTENER_ARN --priority 100 --conditions Field=path-pattern,Values='/api/*' --actions Type=forward,TargetGroupArn=$BACKEND_TG_ARN --region us-east-1
```

### Paso 6: Crear Servicios ECS

```bash
# Obtener subnets (al menos 2 de diferentes AZs)
SUBNET_1="subnet-9a4dc9fc"  # us-east-1a
SUBNET_2="subnet-2dc7450c"  # us-east-1b

# Backend Service
aws ecs create-service \
  --cluster sanmarino-cluster \
  --service-name sanmarino-backend-service \
  --task-definition sanmarino-back-task \
  --desired-count 1 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[$SUBNET_1,$SUBNET_2],securityGroups=[sg-0c6a91db2ba4b872f],assignPublicIp=ENABLED}" \
  --load-balancers "targetGroupArn=<BACKEND_TG_ARN>,containerName=backend,containerPort=5002" \
  --region us-east-1

# Frontend Service
aws ecs create-service \
  --cluster sanmarino-cluster \
  --service-name sanmarino-frontend-service \
  --task-definition sanmarino-front-task \
  --desired-count 1 \
  --launch-type FARGATE \
  --network-configuration "awsvpcConfiguration={subnets=[$SUBNET_1,$SUBNET_2],securityGroups=[sg-0c6a91db2ba4b872f],assignPublicIp=ENABLED}" \
  --load-balancers "targetGroupArn=<FRONTEND_TG_ARN>,containerName=frontend,containerPort=80" \
  --region us-east-1
```

---

## 📝 Task Definition para Frontend

Crea el archivo `frontend/ecs-taskdef-us-east-1.json`:

```json
{
  "family": "sanmarino-front-task",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "512",
  "memory": "1024",
  "runtimePlatform": {
    "cpuArchitecture": "X86_64",
    "operatingSystemFamily": "LINUX"
  },
  "executionRoleArn": "arn:aws:iam::196080479890:role/ecsTaskExecutionRole",
  "taskRoleArn": "arn:aws:iam::196080479890:role/ecsTaskExecutionRole",
  "containerDefinitions": [
    {
      "name": "frontend",
      "image": "196080479890.dkr.ecr.us-east-1.amazonaws.com/sanmarino/zootecnia/granjas/frontend:latest",
      "essential": true,
      "portMappings": [
        { "containerPort": 80, "hostPort": 80, "protocol": "tcp" }
      ],
      "environment": [
        { "name": "NODE_ENV", "value": "production" }
      ],
      "healthCheck": {
        "command": ["CMD-SHELL", "curl -f http://localhost/ || exit 1"],
        "interval": 30,
        "timeout": 5,
        "retries": 3,
        "startPeriod": 10
      },
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/sanmarino-front-task-us-east-1",
          "awslogs-create-group": "true",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "frontend"
        }
      }
    }
  ]
}
```

---

## 🎯 Arquitectura Final en us-east-1

```
Internet
   ↓
ALB (us-east-1)
   ├─ /api/* → Backend Service → RDS (us-east-1) ✅ Misma región
   └─ /* → Frontend Service
```

---

## ⚠️ Importante

**Security Group del RDS**: Debe permitir conexiones desde `sg-0c6a91db2ba4b872f` (no por IP).

**Verificación**:
- Todas las tareas de ECS usarán el Security Group `sg-0c6a91db2ba4b872f`
- El RDS debe tener una regla que permita tráfico desde ese SG

---

## 📞 Resumen

**Ventaja**: Backend y RDS en la misma región (us-east-1) = **CONECTIVIDAD DIRECTA** ✅

**Archivos listos**:
- ✅ `backend/ecs-taskdef-us-east-1.json`
- ⏳ `frontend/ecs-taskdef-us-east-1.json` (crear)

**Completar**: Pasos 1-6 anteriores

