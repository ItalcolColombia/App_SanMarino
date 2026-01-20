# 🔧 Corrección de Headers de Seguridad en Producción

## 🐛 Problema Identificado

OWASP ZAP reportó que los siguientes headers no están presentes en producción:
- ❌ `Strict-Transport-Security` Header Not Set
- ❌ `X-Content-Type-Options` Header Missing

**URL afectada**: `https://zootecnico.sanmarino.com.co/login`

## 🔍 Análisis

El problema es que:
1. El frontend (nginx) está detrás de un Load Balancer/Proxy (AWS ALB) que termina HTTPS
2. Nginx está escuchando en puerto 80 (HTTP) pero el proxy está usando HTTPS
3. Los headers de seguridad no se están aplicando correctamente en todas las rutas

## ✅ Soluciones Implementadas

### 1. Mejora de HSTS en nginx.conf

**Problema**: HSTS solo se aplicaba si nginx detectaba HTTPS directamente, pero en producción nginx recibe HTTP del proxy.

**Solución**: Mejorada la detección de HTTPS vía proxy usando `X-Forwarded-Proto`:

```nginx
# Aplicar HSTS siempre que el proxy indique HTTPS
set $hsts_header "";
if ($http_x_forwarded_proto = 'https') {
  set $hsts_header "max-age=31536000; includeSubDomains; preload";
}
if ($scheme = 'https') {
  set $hsts_header "max-age=31536000; includeSubDomains; preload";
}
add_header Strict-Transport-Security $hsts_header always;
```

### 2. Asegurar X-Content-Type-Options en todas las rutas

**Problema**: El header `X-Content-Type-Options` estaba configurado globalmente pero puede no aplicarse en todas las rutas específicas.

**Solución**: Agregado explícitamente en todas las `location` blocks:

```nginx
# En cada location block
add_header X-Content-Type-Options "nosniff" always;
```

### 3. Mejora de detección de HTTPS en Backend

**Problema**: El backend solo verificaba `X-Forwarded-Proto`, pero algunos proxies usan otros headers.

**Solución**: Agregada verificación adicional de `X-Forwarded-Ssl`:

```csharp
var forwardedSsl = context.Request.Headers["X-Forwarded-Ssl"].FirstOrDefault();
var isHttpsViaSslHeader = string.Equals(forwardedSsl, "on", StringComparison.OrdinalIgnoreCase);
```

## 📋 Cambios Realizados

### Frontend (nginx.conf)
1. ✅ Mejorada detección de HTTPS vía proxy para HSTS
2. ✅ Agregado `X-Content-Type-Options` en todas las rutas:
   - Assets estáticos (`location ~* \.(js|css|...)`)
   - HTML (`location ~* \.html$`)
   - Ruta raíz (`location /`)
   - Healthcheck (`location /health`)

### Backend (SecurityHeadersMiddleware.cs)
1. ✅ Mejorada detección de HTTPS vía proxy
2. ✅ Agregada verificación de `X-Forwarded-Ssl` header

## 🧪 Pruebas

### Verificar Headers en Producción

```bash
# Verificar HSTS
curl -I https://zootecnico.sanmarino.com.co/login | grep -i "strict-transport-security"

# Verificar X-Content-Type-Options
curl -I https://zootecnico.sanmarino.com.co/login | grep -i "x-content-type-options"
```

### Resultado Esperado

```
Strict-Transport-Security: max-age=31536000; includeSubDomains; preload
X-Content-Type-Options: nosniff
```

## 🚀 Despliegue

1. **Rebuild del frontend** con el nuevo `nginx.conf`
2. **Redeploy** en ECS
3. **Verificar** que los headers estén presentes

### Comandos de Despliegue

```bash
# 1. Build de la imagen
cd frontend
docker buildx build --platform linux/amd64 \
  -t 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend:latest \
  --push .

# 2. Actualizar servicio ECS
aws ecs update-service \
  --cluster devSanmarinoZoo \
  --service sanmarino-front-task-service-zp2f403l \
  --task-definition sanmarino-front-task \
  --force-new-deployment \
  --region us-east-2
```

## 📝 Notas Importantes

1. **Puertos 80 y 443**: Los puertos abiertos reportados por Nmap son **normales** - son los puertos HTTP/HTTPS estándar. No es una vulnerabilidad.

2. **HSTS en Producción**: HSTS solo debe aplicarse cuando hay HTTPS. En desarrollo local sin HTTPS, no se aplicará (comportamiento correcto).

3. **Proxy/Load Balancer**: Si el proxy no está enviando `X-Forwarded-Proto: https`, los headers pueden no aplicarse. Verificar la configuración del ALB/CloudFront.

4. **Cache del Navegador**: Después del despliegue, puede ser necesario limpiar la caché del navegador para ver los nuevos headers.

---

**Fecha**: 2025-12-02
**Estado**: ✅ Listo para despliegue

