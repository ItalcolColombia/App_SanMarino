# Configurar Conexión Backend-RDS de Desarrollo

## 📋 Información del RDS

**RDS de Desarrollo:**
- Endpoint: `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- Región: **us-east-1**
- Base de datos: `sanmarinoappdev`
- Usuario: `repropesa01`
- Password: `dcc4M5fyV3x*`
- Puerto: `5432`

**Backend (ECS):**
- Región: **us-east-2**
- Security Group: `sg-8f1ff7fe`
- VPC: `vpc-8ae456e1`

## ⚠️ Consideraciones Importantes

El RDS está en **us-east-1** y el Backend en **us-east-2**. Para que funcione:

1. **Opción 1 (Recomendada para dev):** RDS debe ser **públicamente accesible**
2. **Opción 2:** VPC Peering entre ambas VPCs

## 🔧 Pasos para Configurar

### Paso 1: Obtener Security Group de RDS

1. Ve a la consola AWS: https://console.aws.amazon.com/rds/
2. Selecciona región: **us-east-1**
3. Ve a **Databases** → Busca instancia con endpoint `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
4. Haz clic en la instancia
5. Pestaña **"Connectivity & security"**
6. Anota el **Security Group ID** (ejemplo: `sg-xxxxx`)

### Paso 2: Verificar Accesibilidad Pública

En la misma pestaña "Connectivity & security":
- Verifica que **"Publicly accessible"** esté en **"Yes"**
- Si está en **"No"**, modifica la instancia RDS para hacerla pública:
  1. Selecciona la instancia
  2. Acciones → Modificar
  3. Configuración de red → Marca "Publicly accessible"
  4. Continuar → Modificar inmediatamente

### Paso 3: Agregar Regla al Security Group de RDS

Ejecuta este comando (reemplaza `<RDS_SECURITY_GROUP_ID>`):

```bash
aws ec2 authorize-security-group-ingress \
  --group-id <RDS_SECURITY_GROUP_ID> \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
  --region us-east-1
```

**Nota:** Si prefieres permitir desde cualquier IP (solo para desarrollo):

```bash
aws ec2 authorize-security-group-ingress \
  --group-id <RDS_SECURITY_GROUP_ID> \
  --protocol tcp \
  --port 5432 \
  --cidr 0.0.0.0/0 \
  --region us-east-1
```

### Paso 4: Verificar la Regla

```bash
aws ec2 describe-security-groups \
  --group-ids <RDS_SECURITY_GROUP_ID> \
  --region us-east-1 \
  --query 'SecurityGroups[0].IpPermissions[?FromPort==`5432`]' \
  --output table
```

### Paso 5: Reiniciar el Backend

Una vez configurado, reinicia el servicio ECS:

```bash
aws ecs update-service \
  --cluster devSanmarinoZoo \
  --service sanmarino-back-task-service-75khncfa \
  --force-new-deployment \
  --region us-east-2
```

## ✅ Configuración del Backend Actualizada

Los archivos de configuración ya fueron actualizados:
- `appsettings.json`
- `appsettings.Development.json`

Connection String:
```
Host=reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com;Port=5432;Username=repropesa01;Password=dcc4M5fyV3x*;Database=sanmarinoappdev;SSL Mode=Require;Trust Server Certificate=true;Timeout=15;Command Timeout=30
```

## 🔍 Verificación Final

1. Revisa los logs del backend en CloudWatch
2. Prueba el endpoint: `GET /db-ping`
3. Intenta hacer login

Si aún no funciona:
- Verifica que RDS esté en estado "Available"
- Verifica que el Security Group tenga la regla correcta
- Revisa los logs de conexión en RDS



