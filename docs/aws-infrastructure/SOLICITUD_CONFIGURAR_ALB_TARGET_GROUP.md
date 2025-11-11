# 🔧 Solicitud: Registrar Backend en ALB Target Group

## 📍 Contexto

El backend de San Marino está corriendo pero **NO está registrado en el Target Group del ALB**, por lo que no es accesible a través del balanceador de carga.

---

## ⚠️ Problema Actual

- ✅ Backend corriendo en ECS (IP: 3.137.143.15:5002)
- ✅ ALB activo (sanmarino-alb-878335997.us-east-2.el在你.amazonaws.com)
- ❌ Backend NO registrado en Target Group del ALB
- ❌ El frontend no puede conectarse al backend vía ALB

---

## ✅ Solución: Registrar Backend en Target Group

### Información Necesaria:

**Target Group**:
- Nombre: `sanmarino-backend-tg`
- Región: `us-east-2`
- Puerto: 5002

**Backend Target**:
- IP Privada: `172.31.40.91`
- Puerto: `5002`
- Protocolo: TCP

---

## 📋 Pasos para Configurar (AWS Console)

### Opción 1: Registrar Target Manualmente (Más Rápido)

1. Inicia sesión en AWS Console: https://console.aws.amazon.com/
2. Asegúrate de estar en la región **`us-east-2`** (Ohio, USA)
3. Ve al servicio **EC2** → **Load Balancers** → **us-east-2**
4. En la sección izquierda, ve a **"Target Groups"**
5. Busca y selecciona: **`sanmarino-backend-tg`**
6. Haz click en la pestaña **"Targets"**
7. Haz click en **"Register targets"** (Registrar objetivos)
8. En **"Register targets"**:
   - Ingresa la IP: `172.31.40.91`
   - Selecciona el puerto: `5002`
   - Click en **"Include as pending below"**
9. Verifica que aparece en la lista y haz click en **"Register pending targets"**
10. Espera unos segundos para que el target esté saludable

### Opción 2: Configurar Servicio ECS (Recomendado para Automático)

1. Ve a: **ECS Console** → **us-east-2**
2. Cluster: **`devSanmarinoZoo`**
3. Service: **`sanmarino-back-task-service-75khncfa`**
4. Haz click en **"Update"**
5. Expandir **"Load balancing"**:
   - Activa **Application Load Balancer**
   - Selecciona el ALB: **`sanmarino-alb`**
   - Target Group: **`sanmarino-backend-tg`**
   - Listener: Default listener (80)
   - Listener Rule: `/api/*` → sanmarino-backend-tg
6. Haz click en **"Update"** al final de la página

---

## ✅ Verificación

Después de configurar, verifica que funcione:

### URL del ALB:
```
http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/health
```

Debería devolver: `{"status":"ok"}`

### O en el navegador:
```
http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/swagger
```

Debería mostrar la documentación Swagger de la API.

---

## 📊 Estado Esperado Después de la Configuración

### Target Group "sanmarino-backend-tg"
- ✅ Target registrado: 172.166.91:5002
- ✅ Health check: healthy (verde)
- ✅ Estado: In use

### Resultado
- ✅ Frontend puede conectarse al backend vía ALB
- ✅ No se necesita usar IPs directas (que cambian)
- ✅ URLs estables y consistentes

---

## 🔍 Información Técnica

### ALB
- **Nombre**: sanmarino-alb
- **DNS**: sanmarino-alb-878335997.us-east-2.elb.amazonaws.com
- **Región**: us-east-2
- **Estado**: Active

### Target Groups
- **Backend**: `sanmarino-backend-tg` (puerto 5002) ← NECESITA TARGET
- **Frontend**: `sanmarino-frontend-tg` (puerto 80)

### Backend
- **IP Privada**: 172.31.40.91
- **Puerto**: 5002
- **Security Group**: sg-8f1ff7fe

### Listener Rules del ALB
- `/api/*` → sanmarino-backend-tg
- `/*` → sanmarino-frontend-tg

---

## ⏱️ Tiempo Estimado

**5 minutos** (Opción 1 - Manual)  
**10 minutos** (Opción 2 - ECS Service)

---

## 🆘 Si hay problemas

1. Verifica que estás en la región correcta: **us-east-2**
2. Verifica que el Target Group sea: **`sanmarino-backend-tg`**
3. Verifica que la IP sea: **172.31.40.91**
4. Verifica que el puerto sea: **5002**
5. Espera 1-2 minutos para que el health check verifique el target

---

## 📝 Nota Importante

Esta configuración permite que el ALB enrute automáticamente las peticiones del frontend al backend. Sin esto, el frontend no puede conectarse al backend de manera estable.

---

**Fecha de solicitud**: 寒意 de octubre de 2025  
**Urgencia**: Media - Bloquea acceso del frontend al backend vía ALB  
**Prioridad**: Configurar después del Security Group del RDS

