# Verificación de Conectividad Backend-RDS en AWS

## 📋 Información Actual

**RDS Database:**
- Host: `sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com`
- Region: `us-east-2`
- Port: `5432`
- Database: `sanmarinoapp`
- Username: `postgres`

## 🔍 Pasos de Verificación

### 1. Verificar Región del Backend

**Si el backend está en ECS:**
```bash
# Listar clusters ECS y verificar región
aws ecs list-clusters --region us-east-2

# Listar servicios
aws ecs list-services --cluster <CLUSTER_NAME> --region us-east-2

# Obtener detalles del servicio
aws ecs describe-services --cluster <CLUSTER_NAME> --services <SERVICE_NAME> --region us-east-2
```

**Verificar región del Task Definition:**
```bash
aws ecs describe-task-definition --task-definition <TASK_DEFINITION_NAME> --region us-east-2
```

### 2. Verificar Security Group de RDS

```bash
# Obtener detalles de la instancia RDS
aws rds describe-db-instances --db-instance-identifier sanmarinoapp --region us-east-2

# Obtener Security Groups del RDS
aws rds describe-db-instances \
  --db-instance-identifier sanmarinoapp \
  --region us-east-2 \
  --query 'DBInstances[0].VpcSecurityGroups[*].VpcSecurityGroupId' \
  --output table
```

### 3. Verificar Security Group del Backend (ECS Tasks)

**Si el backend está en ECS con Task Definition:**

```bash
# Obtener Network Configuration del servicio ECS
aws ecs describe-services \
  --cluster <CLUSTER_NAME> \
  --services <SERVICE_NAME> \
  --region us-east-2 \
  --query 'services[0].networkConfiguration.awsvpcConfiguration' \
  --output json
```

**Obtener Security Groups del Task Definition:**
```bash
aws ecs describe-task-definition \
  --task-definition <TASK_DEFINITION_NAME> \
  --region us-east-2 \
  --query 'taskDefinition.containerDefinitions[0].linuxParameters.networkConfiguration' \
  --output json
```

### 4. Verificar Reglas del Security Group de RDS

```bash
# Listar Security Groups y encontrar el de RDS
aws ec2 describe-security-groups --region us-east-2 --output table

# Ver reglas de entrada del Security Group de RDS
aws ec2 describe-security-groups \
  --group-ids <SECURITY_GROUP_ID_RDS> \
  --region us-east-2 \
  --query 'SecurityGroups[0].IpPermissions' \
  --output json
```

### 5. Verificar Reglas del Security Group del Backend

```bash
# Ver reglas de salida del Security Group del Backend
aws ec2 describe-security-groups \
  --group-ids <SECURITY_GROUP_ID_BACKEND> \
  --region us-east-2 \
  --query 'SecurityGroups[0].IpPermissionsEgress' \
  --output json
```

## ⚠️ Problemas Comunes y Soluciones

### Problema 1: Diferentes Regiones

**Síntoma:** Backend en una región (ej: `us-east-1`) y RDS en otra (`us-east-2`)

**Solución:**
- Mover el backend a la misma región que RDS (`us-east-2`)
- O migrar RDS a la región del backend

### Problema 2: Security Group no permite tráfico

**Síntoma:** Timeout al conectar a RDS

**Solución - Agregar regla al Security Group de RDS:**
```bash
# Permitir tráfico desde el Security Group del Backend
aws ec2 authorize-security-group-ingress \
  --group-id <SECURITY_GROUP_ID_RDS> \
  --protocol tcp \
  --port 5432 \
  --source-group <SECURITY_GROUP_ID_BACKEND> \
  --region us-east-2
```

### Problema 3: VPC diferente

**Síntoma:** Backend y RDS en VPCs diferentes

**Solución:**
- Verificar que ambos estén en la misma VPC
- O configurar VPC Peering

### Problema 4: RDS no es público accesible

**Síntoma:** No se puede conectar desde fuera de la VPC

**Solución:**
- Si el backend está fuera de la VPC, configurar RDS como público accesible
- O mover el backend a la misma VPC que RDS

## 🔧 Comandos de Diagnóstico Rápido

### Verificar si RDS es público
```bash
aws rds describe-db-instances \
  --db-instance-identifier sanmarinoapp \
  --region us-east-2 \
  --query 'DBInstances[0].PubliclyAccessible' \
  --output text
```

### Verificar Subnet Group de RDS
```bash
aws rds describe-db-instances \
  --db-instance-identifier sanmarinoapp \
  --region us-east-2 \
  --query 'DBInstances[0].DBSubnetGroup.Subnets[*].SubnetIdentifier' \
  --output table
```

### Verificar VPC de RDS
```bash
aws rds describe-db-instances \
  --db-instance-identifier sanmarinoapp \
  --region us-east-2 \
  --query 'DBInstances[0].DBSubnetGroup.VpcId' \
  --output text
```

### Test de conectividad desde el backend (si tienes acceso SSH)
```bash
# Desde dentro del contenedor/instancia del backend
psql -h sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com \
     -U postgres \
     -d sanmarinoapp \
     -c "SELECT version();"
```

## 📝 Checklist de Verificación

- [ ] Backend y RDS están en la misma región (`us-east-2`)
- [ ] Security Group de RDS permite tráfico en puerto 5432
- [ ] Security Group de RDS permite tráfico desde el Security Group del Backend
- [ ] Security Group del Backend permite tráfico saliente al puerto 5432
- [ ] Backend y RDS están en la misma VPC (o hay VPC Peering configurado)
- [ ] RDS es accesible públicamente (si el backend está fuera de la VPC)
- [ ] Las credenciales son correctas
- [ ] El endpoint de RDS es correcto

## 🚀 Comando para Agregar Regla de Seguridad (si es necesario)

```bash
# Reemplaza <SECURITY_GROUP_ID_RDS> y <SECURITY_GROUP_ID_BACKEND> con los IDs reales

# Opción 1: Permitir desde Security Group específico (recomendado)
aws ec2 authorize-security-group-ingress \
  --group-id <SECURITY_GROUP_ID_RDS> \
  --protocol tcp \
  --port 5432 \
  --source-group <SECURITY_GROUP_ID_BACKEND> \
  --region us-east-2

# Opción 2: Permitir desde cualquier IP (menos seguro, solo para testing)
aws ec2 authorize-security-group-ingress \
  --group-id <SECURITY_GROUP_ID_RDS> \
  --protocol tcp \
  --port 5432 \
  --cidr 0.0.0.0/0 \
  --region us-east-2
```

## 📞 Endpoint de Debug

El backend tiene endpoints para debug:

```bash
# Ver configuración de conexión (sin contraseña)
curl http://localhost:5002/debug/config/conn

# Test de conexión a BD
curl http://localhost:5002/db-ping
```


