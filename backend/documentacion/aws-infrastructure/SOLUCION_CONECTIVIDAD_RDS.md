# Solución de Conectividad Backend-RDS

## 📋 Información Obtenida

**Backend (ECS):**
- Cluster: `devSanmarinoZoo`
- Servicio: `sanmarino-back-task-service-75khncfa`
- Security Group: `sg-8f1ff7fe`
- VPC: `vpc-8ae456e1`
- Región: `us-east-2`

**RDS:**
- Instance: `sanmarinoapp`
- Endpoint: `sanmarinoapp.cfs22w804e5g.us-east-2.rds.amazonaws.com`
- Puerto: `5432`
- Región: `us-east-2`

## ✅ Estado Actual

- **Backend puede salir:** ✅ El Security Group `sg-8f1ff7fe` permite todo el tráfico saliente
- **RDS permite entrada:** ❓ **NECESITA VERIFICACIÓN**

## 🔧 Solución: Agregar Regla al Security Group de RDS

### Paso 1: Obtener Security Group de RDS

1. Ve a la consola AWS: https://console.aws.amazon.com/rds/
2. Selecciona región: **us-east-2**
3. Ve a **Databases** → Busca `sanmarinoapp`
4. Haz clic en la instancia
5. En la pestaña **"Connectivity & security"**, anota el **Security Group ID**

### Paso 2: Agregar Regla de Entrada

Ejecuta este comando (reemplaza `<RDS_SECURITY_GROUP_ID>` con el ID obtenido):

```bash
aws ec2 authorize-security-group-ingress \
  --group-id <RDS_SECURITY_GROUP_ID> \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
  --region us-east-2
```

### Paso 3: Verificar que la Regla se Agregó

```bash
aws ec2 describe-security-groups \
  --group-ids <RDS_SECURITY_GROUP_ID> \
  --region us-east-2 \
  --query 'SecurityGroups[0].IpPermissions[?FromPort==`5432`]' \
  --output table
```

### Paso 4: Probar Conectividad

Una vez agregada la regla, reinicia el servicio ECS del backend:

```bash
aws ecs update-service \
  --cluster devSanmarinoZoo \
  --service sanmarino-back-task-service-75khncfa \
  --force-new-deployment \
  --region us-east-2
```

## 📝 Notas

- Si el Security Group de RDS ya tiene una regla que permite tráfico desde `0.0.0.0/0`, funciona pero es menos seguro
- Es mejor usar reglas específicas entre Security Groups
- Si RDS y Backend están en VPCs diferentes, se necesita VPC Peering

## 🔍 Verificación Final

Después de agregar la regla, verifica que el backend puede conectarse:

1. Revisa los logs de ECS
2. Prueba el endpoint `/db-ping` del backend
3. Verifica que el login funcione correctamente

