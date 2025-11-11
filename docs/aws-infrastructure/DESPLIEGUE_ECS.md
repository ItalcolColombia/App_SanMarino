# 🚀 Despliegue Frontend San Marino - AWS ECS

## 📋 Configuración Actual

### Información AWS
- **Account ID**: 196080479890
- **Región**: us-east-2
- **Cluster**: devSanmarinoZoo
- **Service**: sanmarino-front-task-service-zp2f403l
- **Task Definition Family**: sanmarino-front-task
- **Container Name**: frontend
- **Puerto**: 80
- **ECR URI**: 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend

### Configuración de Backend
El frontend está configurado para conectarse al backend en:
- **URL**: `http://3.145.143.253:5002/api`
- Configurado en: `src/environments/environment.prod.ts`

---

## 🔧 Archivos Configurados

### 1. Dockerfile
- Multi-stage build (deps, build, runtime)
- Build con configuración `docker`
- Nginx como servidor web
- Puerto 80 expuesto
- Health check configurado

### 2. nginx.conf
- Servidor en puerto 80
- Cache para assets estáticos
- Fallback a index.html para SPA
- Sin proxy (backend llamado directamente)

### 3. environment.prod.ts
```typescript
apiUrl: 'http://3.145.143.253:5002/api'
```
- Configurado para apuntar al backend
- Puede cambiarse si cambia la IP del backend

### 4. angular.json
- Configuración `docker` definida
- Output a `dist/browser`

---

## 🚀 Proceso de Despliegue

### Paso 1: Login a ECR
```bash
aws ecr get-login-password --region us-east-2 | docker login --username AWS --password-stdin 196080479890.dkr.ecr.us-east-2.amazonaws.com
```

### Paso 2: Construir Imagen
```bash
cd frontend
docker buildx build --platform linux/amd64 -t 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend:latest --push .
```

### Paso 3: Registrar Task Definition
```bash
aws ecs register-task-definition --cli-input-json file://ecs-taskdef.json --region us-east-2
```

### Paso 4: Actualizar Servicio
```bash
aws ecs update-service --cluster devSanmarinoZoo --service sanmarino-front-task-service-zp2f403l --task-definition sanmarino-front-task --force-new-deployment --region us-east-2
```

---

## ✅ Verificación

### Ver estado del servicio
```bash
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-front-task-service-zp2f403l --region us-east-2
```

### Ver logs
```bash
aws logs tail /ecs/sanmarino-front-task --follow --region us-east-2
```

### Obtener IP del frontend
```bash
aws ecs list-tasks --cluster devSanmarinoZoo --service-name sanmarino-front-task-service-zp2f403l --region us-east-2 --desired-status RUNNING
```

---

## 🔍 Troubleshooting

### Frontend no responde
1. Verificar que la tarea esté RUNNING
2. Revisar logs en CloudWatch
3. Verificar Security Group permite puerto 80
4. Verificar Health Checks

### Error de conexión al backend
1. Verificar que la IP del backend en environment.prod.ts.ccen sea correcta
2. Verificar que el backend esté corriendo
3. Rebuild del frontend si cambia la IP

### Tarea no inicia
1. Verificar que la imagen existe en ECR
2. Verificar plataforma linux/amd64
3. Revisar eventos del servicio

---

## 📝 Notas Importantes

1. **Backend IP**: Si la IP del backend cambia, actualizar `environment.prod.ts` y rebuild
2. **Plataforma**: La imagen DEBE ser linux/amd64
3. **Puerto**: El frontend usa puerto 80 internamente
4. **Build**: Usar configuración `docker` de Angular

---

## 🌐 URLs Esperadas

Una vez desplegado:
- **Frontend**: `http://<IP_FRONTEND>/`
- **Backend**: `http://3.145.143.253:5002/api` (desde el frontend)

