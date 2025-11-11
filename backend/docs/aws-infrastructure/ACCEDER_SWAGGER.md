# Cómo Acceder al Swagger del Backend

## 🔗 URL del Swagger

**URL Principal:**
```
http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/swagger
```

## ⚠️ Problema Actual

El ALB está redirigiendo `/swagger` al frontend en lugar del backend. Esto requiere configuración en el ALB.

## 🔧 Soluciones

### Opción 1: Configurar Regla en el ALB (Recomendado)

Agregar una regla en el Application Load Balancer que redirija `/swagger*` al Target Group del backend.

**Pasos:**
1. Consola AWS → EC2 → Load Balancers
2. Seleccionar: `sanmarino-alb`
3. Ir a la pestaña "Rules"
4. Crear nueva regla:
   - **Priority:** Un número alto (ej: 100)
   - **Condition:** Path is `/swagger*`
   - **Action:** Forward to → Backend Target Group

### Opción 2: Usar Path Específico

Si el backend tiene un path específico configurado en el ALB, úsalo.

### Opción 3: Acceso Directo (Solo para Desarrollo)

Si tienes acceso a la red interna o VPN:
```
http://<IP_PRIVADA_BACKEND>:5002/swagger
```

## 🔐 Autenticación en Swagger

Una vez que puedas acceder al Swagger:

1. **Obtener Token JWT:**
   - Endpoint: `POST /api/Auth/login`
   - Body: `{"email": "tu-email@ejemplo.com", "password": "tu-password"}`
   - Copiar el `token` de la respuesta

2. **Autenticar en Swagger:**
   - Haz clic en el botón "Authorize" 🔓
   - Pega SOLO el token (sin "Bearer ")
   - Swagger agregará automáticamente "Bearer " antes del token
   - Haz clic en "Authorize"

3. **Probar Endpoints:**
   - Ahora puedes probar todos los endpoints protegidos

## 📋 Endpoints Disponibles

### Sin Autenticación:
- `GET /api/Auth/ping`
- `GET /api/Auth/ping-simple`
- `POST /api/Auth/login`
- `POST /api/Auth/register`

### Con Autenticación (requiere token):
- `GET /api/Users`
- `GET /api/Auth/session`
- `GET /api/Auth/profile`
- Todos los demás endpoints de la API

## 🎨 Características del Swagger

- ✅ Tema oscuro personalizado
- ✅ Filtro de búsqueda
- ✅ Deep linking
- ✅ Soporte para JWT Bearer Token
- ✅ Soporte para archivos (IFormFile)

## 📝 URLs Adicionales

- **Swagger JSON:** `/swagger/v1/swagger.json`
- **Descargar JSON:** `/swagger/download`

## 🐛 Troubleshooting

**Si no puedes acceder a Swagger:**

1. Verifica que el backend esté corriendo:
   ```bash
   curl http://sanmarino-alb-878335997.us-east-2.elb.amazonaws.com/api/Auth/ping
   ```

2. Verifica la configuración del ALB:
   - Debe tener una regla para `/swagger*` → Backend

3. Verifica logs del backend:
   - CloudWatch Logs → `/ecs/sanmarino-back-task`


