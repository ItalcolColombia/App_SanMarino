# 🔒 Plan de Cambios - Mejoras de Ciberseguridad

## 📋 Resumen de Vulnerabilidades Identificadas

### Vulnerabilidades de Nivel Medio (2)
1. **Falta de Content-Security-Policy (CSP)** en frontend
2. **Falta de cabecera Anti-Clickjacking** adecuada (X-Frame-Options / frame-ancestors)

### Vulnerabilidades de Nivel Bajo (3)
3. **Divulgación de marcas de tiempo Unix** en archivos JS
4. **Falta de X-Content-Type-Options** (ya existe en backend, falta en frontend)
5. **Strict-Transport-Security no configurado** en frontend

### Alertas Informativas
6. Información sensible en URL
7. Comentarios sospechosos
8. Uso de localStorage

---

## 🔍 Análisis del Estado Actual

### Backend (.NET API)
**Archivo:** `backend/src/ZooSanMarino.API/Middleware/SecurityHeadersMiddleware.cs`

**✅ Ya implementado:**
- ✅ X-Frame-Options: DENY
- ✅ X-Content-Type-Options: nosniff
- ✅ Content-Security-Policy (CSP) con frame-ancestors 'none'
- ✅ HSTS (solo cuando es HTTPS)
- ✅ X-XSS-Protection
- ✅ Referrer-Policy
- ✅ Permissions-Policy

**⚠️ Mejoras necesarias:**
- ⚠️ CSP tiene `'unsafe-inline'` y `'unsafe-eval'` (necesario para Swagger, pero debe estar documentado)
- ⚠️ HSTS solo se aplica si `context.Request.IsHttps` es true (debe forzarse en producción)

### Frontend (Angular + Nginx)
**Archivo:** `frontend/nginx.conf`

**✅ Ya implementado:**
- ✅ X-Frame-Options: SAMEORIGIN (debe cambiarse)
- ✅ X-Content-Type-Options: nosniff
- ✅ X-XSS-Protection
- ✅ Referrer-Policy
- ✅ server_tokens off

**❌ Faltante:**
- ❌ Content-Security-Policy (CSP) - **NO EXISTE**
- ❌ Strict-Transport-Security (HSTS) - **NO EXISTE**
- ⚠️ X-Frame-Options debería ser DENY o mejor aún, usar solo frame-ancestors en CSP

### Angular Build
**Archivos:** `frontend/angular.json`, `frontend/tsconfig.json`

**❌ Faltante:**
- ❌ Configuración para evitar timestamps Unix en archivos JS
- ❌ Configuración para minimizar información sensible en builds

---

## 📝 Cambios Detallados a Realizar

### 1. Backend - Mejoras en SecurityHeadersMiddleware.cs

**Archivo:** `backend/src/ZooSanMarino.API/Middleware/SecurityHeadersMiddleware.cs`

**Cambios:**
1. **HSTS siempre en producción:**
   - Agregar configuración para forzar HSTS en producción incluso si no se detecta HTTPS automáticamente
   - Usar variable de entorno o configuración para determinar si estamos en producción

2. **Mejorar CSP:**
   - Documentar por qué se usan `'unsafe-inline'` y `'unsafe-eval'` (Swagger UI)
   - Opcionalmente, crear CSP más estricto para rutas que no sean Swagger
   - Asegurar que `frame-ancestors 'none'` esté presente

3. **Agregar validación de entorno:**
   - Detectar si estamos en producción para aplicar políticas más estrictas

**Líneas a modificar:**
- Línea 38-45: Mejorar CSP con comentarios explicativos
- Línea 64-68: Mejorar lógica de HSTS para producción

---

### 2. Frontend - Actualizar nginx.conf

**Archivo:** `frontend/nginx.conf`

**Cambios:**
1. **Agregar Content-Security-Policy (CSP):**
   ```nginx
   add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' https:; frame-ancestors 'none';" always;
   ```
   - Nota: `'unsafe-inline'` y `'unsafe-eval'` son necesarios para Angular en modo desarrollo/producción
   - `frame-ancestors 'none'` previene clickjacking

2. **Agregar Strict-Transport-Security (HSTS):**
   ```nginx
   add_header Strict-Transport-Security "max-age=31536000; includeSubDomains; preload" always;
   ```
   - Solo debe aplicarse cuando se usa HTTPS (se puede condicionar)

3. **Cambiar X-Frame-Options:**
   - Opción A: Cambiar a `DENY` (más estricto)
   - Opción B: Eliminar X-Frame-Options y usar solo `frame-ancestors 'none'` en CSP (recomendado)
   - **Recomendación:** Usar solo CSP con frame-ancestors (más moderno y flexible)

4. **Agregar cabeceras adicionales de seguridad:**
   ```nginx
   add_header Permissions-Policy "geolocation=(), microphone=(), camera=(), payment=(), usb=(), magnetometer=(), gyroscope=()" always;
   ```

**Líneas a modificar:**
- Línea 12: Cambiar o eliminar X-Frame-Options
- Después de línea 15: Agregar CSP, HSTS y Permissions-Policy

---

### 3. Frontend - Configurar Angular Build

**Archivo:** `frontend/angular.json`

**Cambios:**
1. **Deshabilitar source maps en producción:**
   - Ya está configurado (`sourceMap: false` en producción)
   - Verificar que también esté en configuración `docker`

2. **Configurar output hashing:**
   - Ya está configurado (`outputHashing: "media"`)
   - Esto ayuda a evitar cacheo de archivos antiguos

3. **Optimización de builds:**
   - Ya está configurado (`optimization: true`)
   - Esto minimiza el código y reduce información sensible

**Archivo:** `frontend/tsconfig.json` y `frontend/tsconfig.app.json`

**Cambios:**
1. **Asegurar que no se incluyan comentarios en producción:**
   - Verificar configuración de `removeComments` en el build
   - Angular CLI lo hace automáticamente en modo producción

2. **Configurar para no incluir información de debug:**
   - Asegurar que `sourceMap: false` en producción
   - Verificar que no se incluyan timestamps en los builds

**Nota sobre timestamps Unix:**
- Los timestamps en archivos JS suelen venir de:
  - Source maps (ya deshabilitados en producción)
  - Comentarios de build (Angular los elimina en producción)
  - Variables de entorno con fechas
- Si persisten, pueden venir de librerías externas y no es crítico

---

### 4. Documentación y Configuración Adicional

**Archivo:** `backend/src/ZooSanMarino.API/appsettings.Production.json` (si existe)

**Cambios:**
1. Agregar configuración para forzar HTTPS y HSTS en producción

**Archivo:** `frontend/nginx.conf`

**Consideraciones:**
1. Si el frontend se sirve detrás de un proxy/load balancer con HTTPS:
   - HSTS debe configurarse en el proxy, no en nginx
   - O configurar nginx para detectar el header `X-Forwarded-Proto`

---

## 🎯 Resumen de Archivos a Modificar

### Archivos a Modificar:
1. ✅ `backend/src/ZooSanMarino.API/Middleware/SecurityHeadersMiddleware.cs`
   - Mejorar HSTS para producción
   - Documentar CSP
   - Asegurar frame-ancestors

2. ✅ `frontend/nginx.conf`
   - Agregar CSP completo
   - Agregar HSTS
   - Cambiar/eliminar X-Frame-Options (usar solo CSP)
   - Agregar Permissions-Policy

3. ⚠️ `frontend/angular.json` (verificación)
   - Verificar que sourceMap esté deshabilitado en producción/docker
   - Verificar optimizaciones

### Archivos a Revisar (sin cambios esperados):
- `backend/src/ZooSanMarino.API/Program.cs` (ya usa el middleware correctamente)
- `frontend/Dockerfile` (ya está bien configurado)

---

## ✅ Checklist de Implementación

### Backend
- [ ] Mejorar lógica de HSTS en SecurityHeadersMiddleware
- [ ] Documentar por qué CSP tiene unsafe-inline/unsafe-eval
- [ ] Verificar que frame-ancestors 'none' esté presente
- [ ] Probar que las cabeceras se apliquen correctamente

### Frontend
- [ ] Agregar CSP completo en nginx.conf
- [ ] Agregar HSTS en nginx.conf
- [ ] Cambiar/eliminar X-Frame-Options (usar solo CSP)
- [ ] Agregar Permissions-Policy
- [ ] Verificar configuración de Angular build
- [ ] Probar que las cabeceras se apliquen correctamente

### Testing
- [ ] Verificar cabeceras con herramientas como:
  - `curl -I https://tu-dominio.com`
  - Security Headers (https://securityheaders.com)
  - OWASP ZAP o similar
- [ ] Verificar que la aplicación funcione correctamente con las nuevas políticas
- [ ] Verificar que Swagger UI funcione (si se usa en producción)

---

## 🔐 Políticas de Seguridad Propuestas

### Content-Security-Policy (CSP)
```
default-src 'self';
script-src 'self' 'unsafe-inline' 'unsafe-eval';
style-src 'self' 'unsafe-inline';
img-src 'self' data: https:;
font-src 'self' data:;
connect-src 'self' https:;
frame-ancestors 'none';
```

**Notas:**
- `'unsafe-inline'` y `'unsafe-eval'` son necesarios para Angular
- `frame-ancestors 'none'` previene clickjacking
- `connect-src 'self' https:` permite llamadas API a HTTPS

### Strict-Transport-Security (HSTS)
```
max-age=31536000; includeSubDomains; preload
```

**Notas:**
- 1 año de duración
- Incluye subdominios
- Preload para listas de HSTS del navegador

---

## 📌 Notas Importantes

1. **CSP y Angular:**
   - Angular requiere `'unsafe-inline'` y `'unsafe-eval'` para funcionar correctamente
   - Esto es una limitación conocida de Angular
   - Alternativa: Usar nonce-based CSP (más complejo de implementar)

2. **HSTS:**
   - Solo debe aplicarse cuando se usa HTTPS
   - Si se aplica en HTTP, puede causar problemas
   - En producción detrás de un load balancer, verificar configuración

3. **X-Frame-Options vs frame-ancestors:**
   - `frame-ancestors` en CSP es más moderno y flexible
   - Si se usa `frame-ancestors`, X-Frame-Options es redundante
   - Recomendación: Usar solo `frame-ancestors` en CSP

4. **Timestamps en JS:**
   - Si persisten después de los cambios, pueden venir de librerías externas
   - No es crítico para seguridad, pero se puede investigar más si es necesario

---

## 🚀 Próximos Pasos

1. Revisar este plan
2. Aprobar cambios
3. Implementar cambios en los archivos
4. Probar en entorno de desarrollo
5. Desplegar a producción
6. Verificar con herramientas de seguridad

---

**Fecha de creación:** $(date)
**Última actualización:** $(date)

