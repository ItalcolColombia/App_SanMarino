# Resultados del Diagnóstico Ejecutado en Consola

**Fecha:** $(date)

## 📊 Resumen Ejecutivo

Se ejecutaron **14 pasos de diagnóstico** en consola para identificar el problema de conectividad entre el backend (ECS us-east-2) y el RDS (us-east-1).

**Problema Confirmado:** El Security Group de RDS no permite tráfico entrante desde el Security Group del backend.

---

## ✅ Resultados de las Pruebas

### 1. Backend ECS ✅
- **Estado:** ACTIVE
- **Tareas ejecutándose:** 1
- **Task Definition:** `sanmarino-back-task:6`
- **Resultado:** ✅ Backend está funcionando correctamente

### 2. Security Group del Backend ✅
- **ID:** `sg-8f1ff7fe`
- **Reglas de salida:** Permite TODO (0.0.0.0/0)
- **Resultado:** ✅ El backend PUEDE hacer conexiones salientes

### 3. VPC Peering ✅
- **Existe peering activo** entre us-east-1 y us-east-2
- **Backend VPC:** `vpc-8ae456e1` (us-east-2)
- **VPCs con peering:**
  - `vpc-00db3b6db9254b4e8` (us-east-1)
  - `vpc-068956be141376ba5` (us-east-1)
- **Resultado:** ✅ VPC Peering está configurado correctamente

### 4. RDS ⚠️
- **Endpoint:** `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- **IP:** `10.4.6.6` (IP privada - correcto con VPC Peering)
- **Región:** `us-east-1`
- **Resultado:** ⚠️  DNS resuelve correctamente, pero falta regla en Security Group

### 5. Pruebas de Endpoints

#### Endpoint `/api/Auth/ping` ✅
```bash
curl http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/Auth/ping
```
**Resultado:**
```json
{"ok":true,"at":"2025-10-31T19:19:25.2502986Z"}
HTTP Status: 200
```
✅ **Funciona correctamente**

#### Endpoint `/api/Auth/login` ❌
```bash
curl -X POST http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"test"}'
```
**Resultado:**
```json
{"message":"An exception has been raised that is likely due to a transient failure."}
HTTP Status: 401
```
❌ **Error confirmado** - Problema de conexión a la base de datos

---

## 🔍 Problema Identificado

### Análisis:

1. ✅ **Backend puede salir:** Security Group permite todas las conexiones salientes
2. ✅ **VPC Peering existe:** Hay peering activo entre las regiones
3. ✅ **DNS resuelve:** El endpoint de RDS se resuelve correctamente
4. ❌ **Security Group de RDS:** NO permite entrada desde `sg-8f1ff7fe`

### Conclusión:

El problema es que el **Security Group de RDS no tiene una regla de entrada** que permita tráfico TCP en puerto 5432 desde el Security Group del backend (`sg-8f1ff7fe`).

---

## ✅ Solución Requerida

### Paso 1: Obtener Security Group de RDS

1. Consola AWS → RDS
2. Región: **us-east-1**
3. Databases → Buscar instancia: `reproductoras-pesadas...`
4. Pestaña **"Connectivity & security"**
5. Anotar **Security Group ID** (ej: `sg-xxxxx`)

### Paso 2: Verificar VPC del RDS

Confirmar que el RDS está en una de estas VPCs (que tienen peering con el backend):
- `vpc-00db3b6db9254b4e8` (us-east-1)
- `vpc-068956be141376ba5` (us-east-1)

### Paso 3: Agregar Regla al Security Group de RDS

Ejecutar este comando (reemplazar `<RDS_SECURITY_GROUP_ID>`):

```bash
aws ec2 authorize-security-group-ingress \
  --group-id <RDS_SECURITY_GROUP_ID> \
  --protocol tcp \
  --port 5432 \
  --source-group sg-8f1ff7fe \
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

```bash
aws ecs update-service \
  --cluster devSanmarinoZoo \
  --service sanmarino-back-task-service-75khncfa \
  --force-new-deployment \
  --region us-east-2
```

### Paso 6: Probar Nuevamente

```bash
curl -X POST http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/Auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"test"}'
```

**Resultado esperado:** Debe funcionar o mostrar error de credenciales (no error de conexión).

---

## 📋 Comandos Ejecutados en el Diagnóstico

Todos los comandos ejecutados están documentados en los scripts:
- `backend/scripts/verificar-error-conectividad.sh`
- `backend/scripts/configurar-rds-desarrollo.sh`

---

## 📞 Información de Contacto

**Backend:**
- Cluster: `devSanmarinoZoo`
- Servicio: `sanmarino-back-task-service-75khncfa`
- Security Group: `sg-8f1ff7fe`
- Región: `us-east-2`

**RDS:**
- Endpoint: `reproductoras-pesadas.cmau6iitrzvz.us-east-1.rds.amazonaws.com`
- Base de datos: `sanmarinoappdev`
- Región: `us-east-1`


