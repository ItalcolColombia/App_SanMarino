# Reporte: Error de Conectividad Backend-RDS

## 📋 Información del Problema

**Descripción:**
El backend desplegado en ECS no puede conectarse a la instancia RDS de desarrollo, resultando en errores 401/500 en el endpoint de login y otros endpoints que requieren acceso a la base de datos.

**Fecha del Reporte:** $(date)

---

## 🔍 Información Técnica

### Backend (ECS)
- **Cluster:** `devSanmarinoZoo`
- **Servicio:** `sanmarino-back-task-service-75khncfa`
- **Región:** `us-east-2`
- **Security Group:** `sg-8f1ff7fe`
- **VPC:** `vpc-8ae456e1`
- **Subnets:** 
  - `subnet-16cbb35a`
  - `subnet-ebdfcf91`
  - `subnet-89cf15e2`
  - `subnet-0068701d28ed1c03c`

### RDS (Base de Datos)
- **Endpoint:** `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- **Región:** `us-east-1`
- **Base de Datos:** `sanmarinoappdev`
- **Puerto:** `5432`
- **Usuario:** `repropesa01`

### ⚠️ Problema Identificado
- **Backend en:** `us-east-2`
- **RDS en:** `us-east-1`
- **Diferentes regiones:** Requiere que RDS sea públicamente accesible o VPC Peering configurado

---

## 🔄 Pasos para Reproducir el Error

### Paso 1: Verificar que el Backend está en Ejecución

**Comando:**
```bash
aws ecs describe-services \
  --cluster devSanmarinoZoo \
  --services sanmarino-back-task-service-75khncfa \
  --region us-east-2 \
  --query 'services[0].{Status:status,Running:runningCount,Desired:desiredCount,TaskDefinition:taskDefinition}' \
  --output table
```

**Resultado Esperado:**
- Status: `ACTIVE`
- RunningCount >= 1
- DesiredCount >= 1

**Si no está corriendo:** El servicio necesita ser iniciado.

---

### Paso 2: Obtener la URL del Backend

**Comando:**
```bash
# Obtener URL del ALB o endpoint público
aws elbv2 describe-load-balancers \
  --region us-east-2 \
  --query 'LoadBalancers[?contains(LoadBalancerName, `sanmarino`)].{Name:LoadBalancerName,DNS:DNSName}' \
  --output table
```

**O verificar en:**
- Consola AWS → ECS → Clusters → `devSanmarinoZoo` → Services → Ver detalles del servicio → Network → Ver endpoint público

---

### Paso 3: Probar Endpoint de Health Check

**URL:** `http://<BACKEND_URL>/health` o `http://<BACKEND_URL>/db-ping`

**Comando cURL:**
```bash
curl -v http://<BACKEND_URL>/db-ping
```

**Resultado Esperado (si funciona):**
```json
{
  "status": "ok",
  "db": "reachable"
}
```

**Resultado Actual (ERROR):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "detail": "DB unreachable: <mensaje de error de conexión>"
}
```

---

### Paso 4: Probar Endpoint de Login

**URL:** `POST http://<BACKEND_URL>/api/Auth/login`

**Comando cURL:**
```bash
curl -X POST http://<BACKEND_URL>/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "testpassword"
  }' \
  -v
```

**Resultado Esperado (si funciona):**
- Status: `200 OK` o `401 Unauthorized` (credenciales incorrectas, pero la conexión a BD funciona)

**Resultado Actual (ERROR):**
```json
{
  "message": "Error interno del servidor",
  "status": 500
}
```

O en los logs del backend aparece:
```
Npgsql.NpgsqlException: Connection refused
o
Timeout expired
o
No route to host
```

---

### Paso 5: Verificar Logs del Backend

**Comando:**
```bash
# Obtener nombre del log group
aws logs describe-log-groups \
  --region us-east-2 \
  --log-group-name-prefix "/ecs/sanmarino" \
  --query 'logGroups[*].logGroupName' \
  --output table

# Ver logs recientes (reemplaza LOG_GROUP_NAME)
aws logs tail <LOG_GROUP_NAME> \
  --region us-east-2 \
  --follow \
  --since 10m
```

**En la Consola AWS:**
1. CloudWatch → Log groups
2. Busca: `/ecs/sanmarino` o nombre del servicio
3. Abre el stream más reciente
4. Busca errores relacionados con:
   - `Npgsql`
   - `Connection`
   - `Database`
   - `PostgreSQL`

**Errores Típicos Encontrados:**
```
Npgsql.NpgsqlException (0x80004005): Connection refused
o
Timeout waiting for connection
o
No route to host: reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com
```

---

### Paso 6: Verificar Configuración de Security Groups

#### 6.1. Security Group del Backend

**Comando:**
```bash
aws ec2 describe-security-groups \
  --group-ids sg-8f1ff7fe \
  --region us-east-2 \
  --query 'SecurityGroups[0].{GroupId:GroupId,GroupName:GroupName,OutboundRules:IpPermissionsEgress}' \
  --output json
```

**Verificar:**
- ✅ Debe tener regla de salida (Outbound) que permita tráfico TCP en puerto 5432
- ✅ O debe permitir todo el tráfico saliente (0.0.0.0/0)

**Resultado Actual:** ✅ El backend tiene salida permitida (`0.0.0.0/0`)

---

#### 6.2. Security Group de RDS

**Pasos en Consola AWS:**
1. Ve a: https://console.aws.amazon.com/rds/
2. Selecciona región: **us-east-1**
3. Databases → Busca instancia: `reproductoras-pesadas...`
4. Haz clic en la instancia
5. Pestaña **"Connectivity & security"**
6. Anota el **Security Group ID** (ej: `sg-xxxxx`)

**Comando para verificar reglas:**
```bash
# Reemplaza <RDS_SECURITY_GROUP_ID> con el ID obtenido
aws ec2 describe-security-groups \
  --group-ids <RDS_SECURITY_GROUP_ID> \
  --region us-east-1 \
  --query 'SecurityGroups[0].{GroupId:GroupId,InboundRules:IpPermissions[?FromPort==`5432`]}' \
  --output json
```

**Verificar:**
- ❌ **PROBLEMA:** No hay regla de entrada (Inbound) que permita tráfico desde `sg-8f1ff7fe`
- ❌ O no hay regla que permita tráfico TCP en puerto 5432 desde ninguna IP/SG

---

### Paso 7: Verificar Accesibilidad Pública del RDS

**En Consola AWS:**
1. RDS → Databases → Instancia `reproductoras-pesadas...`
2. Pestaña **"Connectivity & security"**
3. Verifica el campo **"Publicly accessible"**

**Resultado Actual:**
- Si es **"No"**: El RDS no es accesible desde fuera de su VPC
- Como el backend está en otra región (us-east-2), el RDS **debe ser público** o tener VPC Peering

---

### Paso 8: Verificar VPC Peering (si RDS no es público)

**Comando:**
```bash
# Verificar si existe VPC Peering entre VPCs
aws ec2 describe-vpc-peering-connections \
  --region us-east-1 \
  --filters "Name=status-code,Values=active" \
  --query 'VpcPeeringConnections[*].{ID:VpcPeeringConnectionId,Status:Status.Code,AccepterVPC:AccepterVpcInfo.VpcId,RequesterVPC:RequesterVpcInfo.VpcId}' \
  --output table
```

**Verificar:**
- Si existe un VPC Peering activo entre:
  - VPC del Backend: `vpc-8ae456e1` (us-east-2)
  - VPC del RDS: `<VPC_ID_RDS>` (us-east-1)

---

## ✅ Solución Requerida

Para que el backend pueda conectarse al RDS, se necesita:

### Opción 1: Hacer RDS Públicamente Accesible (Recomendado para Dev)

1. **Modificar RDS para hacerlo público:**
   - RDS → Databases → Seleccionar instancia
   - Actions → Modify
   - Network & Security → Marcar "Publicly accessible"
   - Apply immediately

2. **Agregar regla al Security Group de RDS:**
   ```bash
   aws ec2 authorize-security-group-ingress \
     --group-id <RDS_SECURITY_GROUP_ID> \
     --protocol tcp \
     --port 5432 \
     --source-group sg-8f1ff7fe \
     --region us-east-1
   ```

### Opción 2: Configurar VPC Peering (Más seguro, más complejo)

1. Crear VPC Peering entre VPC del backend y VPC del RDS
2. Configurar Route Tables en ambas VPCs
3. Agregar regla al Security Group de RDS que permita tráfico desde el Security Group del backend

---

## 📝 Comandos de Verificación Post-Solución

Una vez aplicada la solución, verificar:

### 1. Verificar Regla Agregada
```bash
aws ec2 describe-security-groups \
  --group-ids <RDS_SECURITY_GROUP_ID> \
  --region us-east-1 \
  --query 'SecurityGroups[0].IpPermissions[?FromPort==`5432`]' \
  --output table
```

### 2. Probar Conectividad desde el Backend
```bash
curl http://<BACKEND_URL>/db-ping
```

**Resultado Esperado:**
```json
{
  "status": "ok",
  "db": "reachable"
}
```

### 3. Reiniciar el Servicio Backend
```bash
aws ecs update-service \
  --cluster devSanmarinoZoo \
  --service sanmarino-back-task-service-75khncfa \
  --force-new-deployment \
  --region us-east-2
```

---

## 🔗 Referencias

- **Backend Security Group:** `sg-8f1ff7fe` (us-east-2)
- **RDS Endpoint:** `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- **Base de Datos:** `sanmarinoappdev`
- **Documentación Completa:** `backend/docs/aws-infrastructure/CONFIGURAR_RDS_DESARROLLO.md`

---

## 📧 Contacto

Para aplicar la solución, el administrador de AWS necesita:
1. Acceso a RDS en región `us-east-1`
2. Permisos para modificar Security Groups
3. Permisos para modificar configuración de RDS (si se hace público)

**Comandos listos para ejecutar:** Ver sección "Solución Requerida" arriba.



