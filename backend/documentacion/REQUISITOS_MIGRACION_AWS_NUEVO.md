# 📋 Requisitos para Migración al Nuevo AWS

## 🔍 Resumen del Proyecto Actual

### Arquitectura Actual (AWS Actual)
- **Backend**: .NET 9.0 API (ZooSanMarino.API) en ECS Fargate
- **Frontend**: Angular 17 SPA en S3 + CloudFront
- **Base de Datos**: PostgreSQL 13+ en RDS
- **Región**: us-east-2
- **Cuenta AWS**: 021891592771

---

## 🚀 Arquitectura del Nuevo AWS

### Componentes Requeridos en el Nuevo AWS

#### 1. **ECS (Elastic Container Service)**
- ✅ **Cluster ECS**: Crear un cluster nuevo (ej: `sanmarino-cluster`)
- ✅ **Task Definitions**: 
  - Backend: `sanmarino-backend`
  - Frontend: `sanmarino-frontend`
- ✅ **Services**:
  - Backend Service: `sanmarino-api-svc`
  - Frontend Service: `sanmarino-frontend-svc`
- ✅ **Task Role**: IAM role para permisos de ECS
  - Nombre: `ecsTaskExecutionRole` (para ECR, CloudWatch Logs)
  - Nombre: `ecsTaskAppRole` (para acceso a RDS, etc.)
- ✅ **VPC**: Debe estar en la misma VPC que RDS
- ✅ **Subnets**: Mínimo 2 subnets en diferentes AZs
- ✅ **Security Groups**: 
  - SG para Backend: Puerto 5002
  - SG para Frontend: Puerto 80/443
  - SG para RDS: Puerto 5432

#### 2. **ECR (Elastic Container Registry)**
- ✅ **Repositorios**:
  - `sanmarino-backend` (imágenes del backend)
  - `sanmarino-frontend` (imágenes del frontend)
  
#### 3. **RDS (Relational Database Service)**
- ✅ **PostgreSQL**: Versión 13 o superior
- ✅ **Security Group**: Debe permitir acceso desde los Security Groups de ECS
- ✅ **Endpoint**: Necesitarás el endpoint de RDS
- ✅ **Credenciales**: Usuario y contraseña de la base de datos
- ✅ **Database**: Nombre de la base de datos (actual: `sanmarinoapp`)

#### 4. **CloudWatch Logs**
- ✅ **Log Groups**:
  - `/ecs/sanmarino-backend` (logs del backend)
  - `/ecs/sanmarino-frontend` (logs del frontend)

#### 5. **Application Load Balancer (ALB) - Opcional pero Recomendado**
- ✅ **Load Balancer**: Para distribuir el tráfico
- ✅ **Target Groups**: 
  - Backend TG: Puerto 5002
  - Frontend TG: Puerto 80
- ✅ **Listener Rules**: Configurar enrutamiento

#### 6. **CloudFront + S3** (Si mantienes el frontend estático)
- ✅ **S3 Bucket**: Para almacenar el frontend compilado
- ✅ **CloudFront Distribution**: Para servir el frontend
- ✅ **Origin Access Control (OAC)**: Para seguridad S3

---

## 📝 Información Requerida del Nuevo AWS

### 🔑 Credenciales y Acceso

```bash
# 1. Account ID del nuevo AWS
NEW_AWS_ACCOUNT_ID=XXXXXXXXXXXX

# 2. Región del nuevo AWS
NEW_AWS_REGION=us-east-1  # o la región que vayas a usar

# 3. Perfil AWS CLI (opcional)
NEW_AWS_PROFILE=sanmarino-new
```

### 🗄️ Base de Datos RDS

```bash
# Endpoint de RDS
DB_HOST=sanmarinoapp.xxxxxxxxxx.us-east-1.rds.amazonaws.com

# Puerto de PostgreSQL
DB_PORT=5432

# Usuario de la base de datos
DB_USERNAME=postgres  # o el usuario que uses

# Contraseña de la base de datos
DB_PASSWORD=XXXXXXXXXX

# Nombre de la base de datos
DB_NAME=sanmarinoapp  # o el nombre que uses
```

### 🌐 ECS Configuration

```bash
# Cluster Name
ECS_CLUSTER_NAME=sanmarino-cluster

# Service Names
ECS_BACKEND_SERVICE=sanmarino-api-svc
ECS_FRONTEND_SERVICE=sanmarino-frontend-svc

# Task Definition Families
ECS_BACKEND_FAMILY=sanmarino-backend
ECS_FRONTEND_FAMILY=sanmarino-frontend

# ECR Repository URIs
ECR_BACKEND_URI=<ACCOUNT_ID>.dkr.ecr.<REGION>.amazonaws.com/sanmarino-backend
ECR_FRONTEND_URI=<ACCOUNT_ID>.dkr.ecr.<REGION>.amazonaws.com/sanmarino-frontend
```

### 🔒 Security Groups

```bash
# Security Group para Backend ECS
BACKEND_SECURITY_GROUP_ID=sg-xxxxxxxxxxxxx

# Security Group para Frontend ECS
FRONTEND_SECURITY_GROUP_ID=sg-xxxxxxxxxxxxx

# Security Group para RDS
RDS_SECURITY_GROUP_ID=sg-xxxxxxxxxxxxx
```

### 🚢 Network Configuration

```bash
# VPC ID
VPC_ID=vpc-xxxxxxxxxxxxx

# Subnet IDs (mínimo 2)
SUBNET_ID_1=subnet-xxxxxxxxxxxxx
SUBNET_ID_2=subnet-xxxxxxxxxxxxx

# IAM Roles ARN
ECS_TASK_EXECUTION_ROLE_ARN=arn:aws:iam::<ACCOUNT_ID>:role/ecsTaskExecutionRole
ECS_TASK_APP_ROLE_ARN=arn:aws:iam::<ACCOUNT_ID>:role/ecsTaskAppRole
```

### 📧 Email Configuration (SMTP)

```bash
# SMTP Host
SMTP_HOST=smtp.gmail.com  # o tu proveedor SMTP

# SMTP Port
SMTP_PORT=587

# SMTP Username
SMTP_USERNAME=tu-email@gmail.com

# SMTP Password (App Password)
SMTP_PASSWORD=xxxxxxxxxxxx

# From Email
FROM_EMAIL=tu-email@gmail.com

# From Name
FROM_NAME=Zoo San Marino
```

### 🔐 JWT Configuration

```bash
# JWT Secret Key (generar uno nuevo y seguro)
JWT_SECRET_KEY=tu-clave-super-secreta-de-al-menos-32-caracteres-aqui

# JWT Settings
JWT_ISSUER=ZooSanMarino.API
JWT_AUDIENCE=ZooSanMarino.Client
JWT_DURATION_MINUTES=60
```

### 🌍 CORS Configuration

```bash
# Dominios permitidos (separados por comas)
ALLOWED_ORIGINS=https://tu-dominio.com,https://www.tu-dominio.com,http://localhost:4200
```

---

## 🛠️ Pasos para Configurar el Nuevo AWS

### 1. **Configurar AWS CLI**

```bash
# Configurar nuevo perfil
aws configure --profile sanmarino-new

# O usar variables de entorno
export AWS_ACCESS_KEY_ID="tu-access-key"
export AWS_SECRET_ACCESS_KEY="tu-secret-key"
export AWS_REGION="us-east-1"
```

### 2. **Crear Repositorios ECR**

```bash
# Backend
aws ecr create-repository --repository-name sanmarino-backend \
  --region us-east-1 --profile sanmarino-new

# Frontend
aws ecr create-repository --repository-name sanmarino-frontend \
  --region us-east-1 --profile sanmarino-new
```

### 3. **Crear Security Groups**

```bash
# Security Group para Backend (puerto 5002)
aws ec2 create-security-group --group-name sanmarino-backend-sg \
  --description "Security group for San Marino Backend" \
  --vpc-id vpc-xxxxxxxxxxxxx \
  --region us-east-1

# Security Group para Frontend (puerto 80)
aws ec2 create-security-group --group-name sanmarino-frontend-sg \
  --description "Security group for San Marino Frontend" \
  --vpc-id vpc-xxxxxxxxxxxxx \
  --region us-east-1

# Security Group para RDS (puerto 5432)
aws ec2 create-security-group --group-name sanmarino-rds-sg \
  --description "Security group for San Marino RDS" \
  --vpc-id vpc-xxxxxxxxxxxxx \
  --region us-east-1
```

### 4. **Configurar Reglas de Security Groups**

```bash
# Permitir tráfico del ALB al Backend (puerto 5002)
aws ec2 authorize-security-group-ingress \
  --group-id sg-backend-xxxxx \
  --protocol tcp --port 5002 --source-group sg-alb-xxxxx

# Permitir tráfico del ALB al Frontend (puerto 80)
aws ec2 authorize-security-group-ingress \
  --group-id sg-frontend-xxxxx \
  --protocol tcp --port 80 --source-group sg-alb-xxxxx

# Permitir acceso RDS desde Backend ECS
aws ec2 authorize-security-group-ingress \
  --group-id sg-rds-xxxxx \
  --protocol tcp --port 5432 --source-group sg-backend-xxxxx
```

### 5. **Crear IAM Roles**

```bash
# Task Execution Role (para ECR y CloudWatch)
aws iam create-role --role-name ecsTaskExecutionRole \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Principal": {"Service": "ecs-tasks.amazonaws.com"},
      "Action": "sts:AssumeRole"
    }]
  }'

# Task App Role (para acceso a RDS)
aws iam create-role --role-name ecsTaskAppRole \
  --assume-role-policy-document '{
    "Version": "2012-10-17",
    "Statement": [{
      "Effect": "Allow",
      "Principal": {"Service": "ecs-tasks.amazonaws.com"},
      "Action": "sts:AssumeRole"
    }]
  }'
```

### 6. **Crear CloudWatch Log Groups**

```bash
# Log Group para Backend
aws logs create-log-group --log-group-name /ecs/sanmarino-backend \
  --region us-east-1 --profile sanmarino-new

# Log Group para Frontend
aws logs create-log-group --log-group-name /ecs/sanmarino-frontend \
  --region us-east-1 --profile sanmarino-new
```

### 7. **Crear Cluster ECS**

```bash
aws ecs create-cluster --cluster-name sanmarino-cluster \
  --region us.minus-east-1 --profile sanmarino-new
```

---

## 📤 Proceso de Despliegue

### Backend

```bash
# 1. Ir al directorio backend
cd backend

# 2. Modificar el script de despliegue con las nuevas credenciales
# Editar deploy-ecs.ps1 o crear uno nuevo

# 3. Ejecutar despliegue
.\deploy-ecs.ps1 -Profile sanmarino-new -Region us-east-1 \
  -Cluster sanmarino-cluster -Service sanmarino-api-svc \
  -Family sanmarino-backend -Container api \
  -EcrUri <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/sanmarino-backend
```

### Frontend

```bash
# Opción 1: Si usas S3 + CloudFront
cd frontend
ng build --configuration production
aws s3 sync ./dist s3://tu-bucket --delete
aws cloudfront create-invalidation --distribution-id <DIST_ID> --paths "/*"

# Opción 2: Si usas ECS (como Docker container)
docker build -t sanmarino-frontend .
docker tag sanmarino-frontend:latest <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/sanmarino-frontend:latest
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/sanmarino-frontend:latest
```

---

## 📋 Checklist de Verificación

### ✅ Pre-requisitos
- [ ] AWS Account con permisos necesarios
- [ ] AWS CLI configurado
- [ ] Docker Desktop instalado
- [ ] .NET SDK 9.0 instalado
- [ ] Node.js y Angular CLI instalados

### ✅ Infraestructura AWS
- [ ] RDS PostgreSQL creado y configurado
- [ ] ECR repositorios creados
- [ ] ECS Cluster creado
- [ ] IAM Roles creados y configurados
- [ ] Security Groups creados y configurados
- [ ] VPC y Subnets configuradas
- [ ] CloudWatch Log Groups creados

### ✅ Configuración de Base de Datos
- [ ] Base de datos creada en RDS
- [ ] Usuario de base de datos creado
- [ ] Security Group de RDS permite acceso desde ECS
- [ ] Migraciones aplicadas (si usas migraciones automáticas)

### ✅ Backend
- [ ] Task Definition creada o actualizada
- [ ] Variables de entorno configuradas
- [ ] Imagen Docker construida y subida a ECR
- [ ] Servicio ECS creado/actualizado
- [ ] Health checks funcionando

### ✅ Frontend
- [ ] Configuración de API URL actualizada
- [ ] Build de producción creado
- [ ] Subido a S3/ECR según corresponda
- [ ] CloudFront distribution configurada (si aplica)

### ✅ Conectividad
- [ ] Backend puede conectarse a RDS
- [ ] Frontend puede conectarse a Backend
- [ ] Security Groups permiten el tráfico necesario
- [ ] CORS configurado correctamente

### ✅ Paraímetros de Red
- [ ] Security Groups configurados correctamente
- [ ] Subnets en diferentes AZs
- [ ] Route Tables configuradas
- [ ] Internet Gateway configurada (si aplica)

---

## 🔧 Archivos que Necesitas Modificar

### Backend

1. **backend/env.production.example** - Actualizar con nuevas credenciales
2. **backend/ecs-taskdef.json** - Actualizar con nuevos valores
3. **backend/deploy-ecs.ps1** - Actualizar parámetros
4. **backend/src/ZooSanMarino.API/appsettings.json** - Actualizar si usas archivos locales

### Frontend

1. **frontend/src/environments/environment.prod.ts** - Actualizar `apiUrl`
2. **frontend/nginx.conf** - Verificar configuración
3. **frontend/proxy.conf.json** - Si usas proxy en desarrollo

---

## 📞 Comandos Útiles para Troubleshooting

### Verificar estado de servicios

```bash
# Ver estado del servicio ECS
aws ecs describe-services --cluster sanmarino-cluster \
  --services sanmarino-api-svc --region us-east-1

# Ver logs de CloudWatch
aws logs tail /ecs/sanmarino-backend --follow --region us-east-1

# Ver tareas ejecutándose
aws ecs list-tasks --cluster sanmarino-cluster --region us-east-1
```

### Verificar conectividad

```bash
# Probar conexión a RDS desde un contenedor ECS
aws ecs execute-command --cluster sanmarino-cluster \
  --task <TASK_ARN> --container api --command "/bin/sh" \
  --interactive --region us-east-1
```

### Verificar configuración de red

```bash
# Ver Security Groups
aws ec2 describe-security-groups --group-ids sg-xxxxx --region us-east-1

# Ver VPC y Subnets
aws ec2 describe-vpcs --region us-east-1
aws ec2 describe-subnets --filters "Name=vpc-id,Values=vpc-xxxxx" --region us-east-1
```

---

## 🔐 Recomendaciones de Seguridad

1. **Nunca** hardcodear credenciales en el código
2. **Usar** Secrets Manager para passwords
3. **Rotar** credenciales regularmente
4. **Usar** certificados SSL/TLS para todas las conexiones
5. **Configurar** MFA para cuentas AWS
6. **Auditar** logs regularmente con CloudWatch
7. **Aplicar** principle of least privilege en IAM roles

---

## 📚 Referencias

- [Documentación ECS](https://docs.aws.amazon.com/ecs/)
- [Documentación RDS](https://docs.aws.amazon.com/rds/)
- [Documentación ECR](https://docs.aws.amazon.com/ecr/)
- [Documentación CloudWatch](https://docs.aws.amazon.com/cloudwatch/)

---

## ✅ Resumen Final

Para conectarte al nuevo AWS necesitas:

1. **Account ID** del nuevo AWS
2. **Región** del nuevo AWS
3. **Credenciales** de acceso (Access Key + Secret Key)
4. **Endpoint de RDS** y credenciales de base de datos
5. **ARNs de IAM Roles** (Task Execution Role y Task App Role)
6. **IDs de Security Groups** (Backend, Frontend, RDS)
7. **VPC ID** y **Subnet IDs**
8. **Configuración SMTP** para emails
9. **JWT Secret Key** nuevo
10. **Dominios permitidos** para CORS

Una vez tengas toda esta información, puedes comenzar con el despliegue siguiendo los pasos indicados arriba.

