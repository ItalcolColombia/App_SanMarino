# ✅ Configuración Frontend → Backend vía ALB

## 📊 Estado Actual

### Frontend
- ✅ **Configuración**: `/api` (rutas relativas)
- ✅ **ALB DNS**: `sanmarino-alb-878335997.us-east-2.elb.amazonaws.com`
- ✅ **Target Group**: `sanmarino-frontend-tg` (puerto 80)

### Backend
- ⚠️ **IP Dinámica**: 3.137.143.15:5002 (cambia al reiniciar)
- ⚠️ **Target Group**: `sanmarino-backend-tg` (puerto 5002)
- ❌ **Problema**: NO está registrado en el Target Group del ALB

---

## ⚠️ Problema

**El backend usa IP dinámica** (cambia al reiniciar). La IP actual es `3.137.143.15` pero antes era `3.17.161.5`.

**El frontend está configurado correctamente** para usar el ALB (`/api`), pero el backend NO está registrado en el Target Group del ALB.

---

## ✅ Solución: Registrar Backend en ALB

El backend debe estar registrado en el Target Group `sanmarino-backend-tg` para que el ALB pueda enrutar las peticiones.

### Opción 1: Configurar el Servicio ECS (Recomendado)

El servicio ECS del backend debe configurarse para que automáticamente registre las tareas en el Target Group.

**Información necesaria**:
- **Backend IP Privada**: 172.31.40.91
- **Puerto**: 5002
- **Target Group**: `sanmarino-backend-tg`

**Pasos en AWS Console**:

1. Ve a: **ECS Console** → **us-east-2**
2. Cluster: `devSanmarinoZoo`
3. Service: `sanmarino-back-task-service-75khncfa`
4. Click en **"Update"**
5. En la sección **"Load balancing"**:
   - Activa **Application Load Balancer**
   - Selecciona el ALB: `sanmarino-alb`
   - Target Group: `sanmarino-backend-tg`
   - Production listener port: 80
   - Path pattern: `/api/*`
6. Click en **"Update"**

### Opción 2: Registrar Target Manualmente (Temporal)

Si necesitas acceso inmediato, puedes registrar la IP manualmente:

1. Ve a: **EC2 Console** → **Load Balancers** → **us-east-2**
2. Busca el ALB: `sanmarino-alb`
3. Ve a la pestaña **"Target groups"**
4. Selecciona: `sanmarino-backend-tg`
5. Click en **"Register targets"**
6. Ingresa:
   - **IP**: 172.31.40.91
   - **Port**: 5002
7. Click en **"Register"**

---

## ✅ Después de Configurar

**El frontend ya está correcto**, solo necesita que el backend esté registrado en el ALB.

### URLs de Acceso

**Frontend**:
```
http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/
```

**Backend vía ALB**:
```
http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api
http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/swagger
http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/health
```

---

## 📝 Ventajas de Usar ALB

1. ✅ **IP Estable**: El DNS del ALB no cambia
2. ✅ **No necesitas reconfigurar el frontend** cuando cambia la IP del backend
3. ✅ **Balanceo de carga**: Si hay múltiples instancias del backend
4. ✅ **Health checks**: El ALB detecta si el backend está saludable
5. ✅ **HTTPS fácil**: Se configura SSL/TLS en el ALB, no en cada contenedor

---

## 🔧 Configuración Actual del Frontend

**Archivo**: `frontend/src/environments/environment.prod.ts`

```typescript
export const environment = {
  production: true,
  apiUrl: '/api'  // ✅ Correcto - Usa ALB
};
```

**El frontend NO necesita cambios**. Está configurado para usar rutas relativas que van al ALB.

---

## ⚠️ IMPORTANTE

**NO uses la IP directa** del backend (`http://3.137.143.15:5002/...`) porque:
- ❌ Cambia cuando se reinicia el servicio
- ❌ Tendrías que reconfigurar el frontend cada vez
- ❌ Pierdes las ventajas del ALB

**Siempre usa el ALB**: `http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/...`

---

**Fecha**: 27 de octubre de 2025  
**Estado**: Frontend OK ✅ | Backend necesita registro en ALB ⏳

