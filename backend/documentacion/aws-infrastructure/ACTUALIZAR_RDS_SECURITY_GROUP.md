# 🔒 Actualizar Security Group de RDS - Base de Datos Externa

## 🚨 Problema Identificado

El backend cambió de IP cuando se configuró el ALB:
- **IP Anterior**: 3.145.143.253
- **IP Actual**: 3.147.69.215

El RDS está en otro AWS account (`sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com`), y su Security Group solo permite conexiones desde IPs específicas.

---

## ✅ Solución

### Paso 1: Agregar Nueva IP al Security Group de RDS

Necesitas acceder al AWS account donde está el RDS y actualizar su Security Group.

#### IP que debe agregarse:
```
3.147.69.215/32
```

#### Puerto:
```
5432 (PostgreSQL)
```

### Paso 2: Configuración del Security Group en la otra cuenta

1. Ve a **EC2** → **Security Groups** en el AWS account donde está el RDS
2. Encuentra el Security Group del RDS: `sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com`
3. Agrega una regla de Inbound:
   - **Type**: PostgreSQL (o Custom TCP)
   - **Port**: 5432
   - **Source**: `3.147.69.215/32`
   - **Description**: Backend San Marino ECS (Nueva IP con ALB)

### Paso 3: (Opcional) Eliminar IP Antigua

Si ya no se necesita, puedes eliminar la regla para la IP antigua `3.145.143.253/32`.

---

## ⚠️ Problema de Diseño

### El Problema Real

**Las IPs de las tareas ECS cambian cada vez que se reinician**. Esto significa que tendrías que actualizar el Security Group de RDS cada vez que:
- El servicio se actualiza
- El servicio se reinicia
- La tarea falla y se crea una nueva

### Solución Mejorada (Recomendada)

En lugar de permitir por IP específica, deberías:

#### Opción 1: Permitir desde el Security Group de ECS (Cross-Account)

Si el RDS y ECS están en cuentas diferentes:
1. **En el AWS account del RDS**:
   - Agrega una regla en el Security Group de RDS
   - **Type**: PostgreSQL
   - **Port**: 5432
   - **Source**: El Security Group ID de ECS (`sg-8f1ff7fe`) desde el account `196080479890`

Para hacer esto cross-account, el formato es:
```
sg-8f1ff7fe/196080479890
```

#### Opción 2: VPN o VPC Peering

Configurar conexión de red entre las dos cuentas AWS.

---

## 🔍 Verificación

Una vez agregado el Security Group, verifica que el backend pueda conectarse:

```bash
# Verificar logs del backend
aws logs tail /ecs/sanmarino-back-task --region us-east-2 --since 5m | grep -i "error\|exception\|connection"

# Verificar health check
curl http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/health
```

---

## 📋 Información del Backend Actual

- **Account**: 196080479890
- **Región**: us-east-2
- **Security Group ECS**: sg-8f1ff7fe
- **IP Pública Actual**: 3.147.69.215
- **IP Privada Actual**: 172.31.77.212

---

## 📝 Nota Importante

**Las IPs de las tareas ECS seguirán cambiando**. Para evitar este problema constantemente:

1. **Mejor solución**: Configurar el RDS para permitir desde el Security Group de ECS (cross-account)
2. **Solución temporal**: Cada vez que cambie la IP, actualizar el Security Group de RDS

---

## 🚀 Comando para Obtener IP Actual

Si en el futuro necesitas obtener la IP actual del backend:

```bash
TASK_ARN=$(aws ecs list-tasks --cluster devSanmarinoZoo --service-name sanmarino-back-task-service-75khncfa --region us-east-2 --query 'taskArns[0]' --output text)
ENI_ID=$(aws ecs describe-tasks --cluster devSanmarinoZoo --tasks $TASK_ARN --region us-east-2 --query 'tasks[0].attachments[0].details[?name==`networkInterfaceId`].value' --output text)
PUBLIC_IP=$(aws ec2 describe-network-interfaces --network-interface-ids $ENI_ID --region us-east-2 --query 'NetworkInterfaces[0].Association.PublicIp' --output text)
echo "IP Pública actual: $PUBLIC_IP"
```

---

**Fecha**: 27 de Octubre 2025  
**IP que agregar**: 3.147.69.215/32

