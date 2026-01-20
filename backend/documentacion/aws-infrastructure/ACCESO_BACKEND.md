# 🔗 Cómo Acceder al Backend

## ✅ Opción 1: IP Directa (FUNCIONA AHORA)

**URL Base**: http://3.137.143.15:5002

### Endpoints Disponibles:

- **Health Check**: http://3.137.143.15:5002/health
- **Swagger API**: http://3.137.143.15:5002/swagger
- **API Base**: http://3.137.143.15:5002/api
- **Login**: http://3.137.143.15:5002/api/Auth/login

### ⚠️ IMPORTANTE
Esta IP puede cambiar cuando se reinicie el servicio ECS. No es estable.

---

## ⏳ Opción 2: ALB (Pendiente Configuración)

**URL Base**: http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api

### Endpoints Disponibles:

- **Health Check**: http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/health
- **Swagger API**: http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/swagger
- **API Base**: http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api
- **Login**: http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/Auth/login

### ⚠️ Estado Actual
❌ NO funciona todavía - El backend no está registrado en el Target Group del ALB

---

## 📝 Para Usar AHORA

**Usa la Opción 1** (IP directa):
- http://3.137.143.15:5002/swagger

---

## 🔧 Para Habilitar el ALB

Necesitas registrar el backend en el Target Group:
- Target Group: `sanmarino-backend-tg`
- Target: IP 172.31.40.91, Puerto 5002

Ver: `CONFIGURACION_FRONTEND_BACKEND_ALB.md`

