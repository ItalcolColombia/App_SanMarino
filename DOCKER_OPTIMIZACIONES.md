# Optimizaciones de Dockerización - Backend y Frontend

## 📋 Resumen de Mejoras Implementadas

Este documento resume todas las optimizaciones y buenas prácticas aplicadas a la dockerización del backend y frontend de la aplicación San Marino.

---

## 🔧 Backend (.NET 9.0)

### Mejoras en Dockerfile

#### 1. **Multi-stage Build Optimizado**
- ✅ Separación de etapas: `restore` → `build` → `final`
- ✅ Mejor aprovechamiento del cache de Docker
- ✅ Restore separado para cachear dependencias independientemente del código

#### 2. **Seguridad**
- ✅ Usuario no-root (`appuser`) para ejecutar la aplicación
- ✅ Permisos correctos en archivos y directorios
- ✅ Variables de entorno de seguridad configuradas

#### 3. **Optimizaciones de Tamaño**
- ✅ Exclusión de tests en la imagen final (reducción significativa de tamaño)
- ✅ Limpieza de cache de apt en una sola capa
- ✅ Build optimizado con flags específicos:
  - `--runtime linux-x64`
  - `--self-contained false`
  - `--no-restore` para builds más rápidos

#### 4. **Variables de Entorno**
- ✅ `DOTNET_RUNNING_IN_CONTAINER=true`
- ✅ `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`
- ✅ `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`

#### 5. **Healthcheck**
- ✅ Healthcheck configurado con intervalos apropiados
- ✅ Timeout y retries optimizados

---

## 🎨 Frontend (Angular + Nginx)

### Mejoras en Dockerfile

#### 1. **Multi-stage Build Optimizado**
- ✅ Etapa `deps`: Instalación de dependencias con cache optimizado
- ✅ Etapa `build`: Build de producción con variables optimizadas
- ✅ Etapa `runtime`: Nginx Alpine (imagen ligera)

#### 2. **Optimizaciones de Build**
- ✅ Uso de `corepack` para yarn (más eficiente)
- ✅ `yarn cache clean` después de instalar dependencias
- ✅ `NODE_OPTIONS="--max-old-space-size=4096"` para builds grandes
- ✅ Limpieza de `node_modules` después del build

#### 3. **Seguridad**
- ✅ Nginx ejecutándose como usuario `nginx` (no-root)
- ✅ Permisos correctos en todos los directorios
- ✅ Headers de seguridad en nginx.conf

#### 4. **Configuración de Nginx Optimizada**

##### Compresión Gzip
- ✅ Gzip habilitado con nivel 6
- ✅ Tipos MIME optimizados para compresión
- ✅ `gzip_vary on` para mejor cache

##### Cache Estratégico
- ✅ Assets estáticos (JS, CSS, imágenes): Cache de 1 año (immutable)
- ✅ HTML: No cache (para permitir actualizaciones)
- ✅ Logs deshabilitados para assets estáticos

##### Seguridad
- ✅ `server_tokens off` (ocultar versión de nginx)
- ✅ Headers de seguridad:
  - `X-Frame-Options: SAMEORIGIN`
  - `X-Content-Type-Options: nosniff`
  - `X-XSS-Protection: 1; mode=block`
  - `Referrer-Policy: strict-origin-when-cross-origin`
- ✅ Bloqueo de archivos ocultos (`.htaccess`, etc.)

##### Healthcheck Endpoint
- ✅ Endpoint `/health` para monitoreo

---

## 📁 .dockerignore Mejorados

### Backend
- ✅ Exclusión de tests, documentación, scripts de deployment
- ✅ Exclusión de archivos de IDE y temporales
- ✅ Exclusión de archivos de build (`bin/`, `obj/`)

### Frontend
- ✅ Exclusión de `node_modules`, `dist/`, archivos de test
- ✅ Exclusión de documentación y scripts de deployment
- ✅ Exclusión de archivos de configuración local

**Beneficio**: Reducción significativa del contexto de build y tiempo de construcción.

---

## 🐳 Docker Compose Optimizado

### Backend
- ✅ Límites de recursos configurados (CPU y memoria)
- ✅ Healthcheck con intervalos apropiados
- ✅ Logging con rotación (max-size: 10m, max-file: 3)
- ✅ Variables de entorno optimizadas
- ✅ BuildKit habilitado para mejor rendimiento

### Frontend
- ✅ Límites de recursos optimizados para nginx (ligero)
- ✅ Healthcheck configurado
- ✅ Logging con rotación
- ✅ Red compartida (`app-network`)

---

## 📊 Beneficios de las Optimizaciones

### Tamaño de Imágenes
- **Backend**: Reducción al excluir tests y optimizar layers
- **Frontend**: Imagen final solo con nginx + assets (muy ligera)

### Seguridad
- ✅ Ambos contenedores ejecutándose como usuarios no-root
- ✅ Headers de seguridad en frontend
- ✅ Permisos mínimos necesarios

### Rendimiento
- ✅ Mejor cache de Docker (builds más rápidos)
- ✅ Compresión gzip en frontend (menor ancho de banda)
- ✅ Cache estratégico de assets estáticos

### Mantenibilidad
- ✅ Código más limpio y organizado
- ✅ Comentarios explicativos
- ✅ Variables de entorno bien documentadas

---

## 🚀 Comandos Útiles

### Build de imágenes
```bash
# Backend
cd backend
docker build -t sanmarino-backend:latest .

# Frontend
cd frontend
docker build -t sanmarino-frontend:latest .
```

### Build con BuildKit (recomendado)
```bash
DOCKER_BUILDKIT=1 docker-compose build
```

### Ver tamaño de imágenes
```bash
docker images | grep sanmarino
```

### Ejecutar con docker-compose
```bash
# Backend
cd backend
docker-compose up -d

# Frontend
cd frontend
docker-compose up -d
```

### Ver logs
```bash
docker-compose logs -f backend
docker-compose logs -f frontend
```

### Healthcheck manual
```bash
# Backend
curl http://localhost:5002/health

# Frontend
curl http://localhost:8080/health
```

---

## ⚠️ Notas Importantes

1. **Variables de Entorno**: Asegúrate de tener un archivo `.env` en el directorio `backend/` con todas las variables necesarias.

2. **Puerto 80 en Nginx**: Nginx necesita permisos especiales para el puerto 80. La imagen oficial de nginx maneja esto internamente usando capabilities de Linux.

3. **Recursos**: Los límites de recursos en docker-compose son sugerencias. Ajusta según las necesidades de tu entorno.

4. **BuildKit**: Para mejor rendimiento, usa BuildKit:
   ```bash
   export DOCKER_BUILDKIT=1
   ```

5. **Cache**: Las capas de Docker se cachean automáticamente. Si cambias código fuente pero no dependencias, el build será más rápido.

---

## 🔍 Verificación de Optimizaciones

### Verificar usuario no-root
```bash
# Backend
docker exec sanmarino-backend whoami
# Debe mostrar: appuser

# Frontend (nginx ya usa usuario nginx por defecto)
docker exec sanmarino-frontend ps aux | grep nginx
```

### Verificar tamaño de imágenes
```bash
docker images --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}"
```

### Verificar healthcheck
```bash
docker inspect --format='{{.State.Health.Status}}' sanmarino-backend
docker inspect --format='{{.State.Health.Status}}' sanmarino-frontend
```

---

## 📝 Próximas Mejoras Sugeridas

1. **Multi-arch builds**: Soporte para ARM64 (Apple Silicon, etc.)
2. **Scan de seguridad**: Integrar `docker scan` o Trivy
3. **CI/CD**: Automatizar builds y scans en pipeline
4. **Read-only filesystem**: Habilitar cuando sea posible (requiere ajustes)
5. **Secrets management**: Usar Docker secrets en lugar de .env para producción

---

**Última actualización**: $(date)
**Versión**: 1.0


