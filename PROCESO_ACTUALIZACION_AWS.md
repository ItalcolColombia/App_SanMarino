# ✅ Proceso de Actualización AWS - Validación

Documento de referencia para validar y ejecutar actualizaciones en AWS usando el Makefile.

---

## 📋 Pre-requisitos

| Requisito | Verificación |
|-----------|--------------|
| Docker Desktop | `docker info` → debe responder sin error |
| AWS CLI | `aws --version` → debe estar instalado |
| Credenciales AWS | `aws sts get-caller-identity` → cuenta 196080479890 |
| Región | `us-east-2` (Ohio) |

---

## 🔄 Proceso de Actualización

### Opción 1: Solo Backend

```bash
make deploy-backend
```

**Flujo (7 pasos):**

| Paso | Acción | Duración aprox. |
|------|--------|-----------------|
| 1/7 | Login a ECR | ~10 s |
| 2/7 | Build imagen Docker (linux/amd64) y push a ECR | **~20-25 min** |
| 3/7 | Actualizar Task Definition con nuevo tag | < 1 s |
| 4/7 | Registrar Task Definition en ECS | ~3 s |
| 5/7 | Actualizar servicio ECS (force-new-deployment) | ~2 s |
| 6/7 | Esperar estabilización del servicio | **2-3 min** |
| 7/7 | Verificación (runningCount > 0) | ~10 s |

**Archivos involucrados:**
- `backend/scripts/deploy-backend-ecs.sh`
- `backend/ecs-taskdef-new-aws.json`

---

### Opción 2: Solo Frontend

```bash
make deploy-frontend
```

**Flujo (7 pasos):** similar al backend, usando:
- `frontend/scripts/deploy-frontend-ecs.sh`
- `frontend/deploy/ecs-taskdef.json` (o `ecs-taskdef.json` generado)

---

### Opción 3: Backend + Frontend

```bash
make deploy-all
```

Ejecuta primero `deploy-backend`, luego `deploy-frontend`.

---

## ✅ Criterios de Éxito

### Backend
- [ ] Mensaje `[SUCCESS] Despliegue completado exitosamente`
- [ ] `runningCount` > 0
- [ ] Health check responde: `http://IP:5002/health`
- [ ] Swagger accesible: `http://IP:5002/swagger`

### Frontend
- [ ] Mensaje `✓ Despliegue exitoso`
- [ ] `runningCount` > 0
- [ ] ALB accesible: `http://[ALB_DNS]/`

---

## 🔍 Verificación Post-Despliegue

```bash
# Estado del servicio Backend
aws ecs describe-services --cluster devSanmarinoZoo \
  --services sanmarino-back-task-service-75khncfa \
  --region us-east-2 \
  --query 'services[0].{running:runningCount,desired:desiredCount}'

# Estado del servicio Frontend
aws ecs describe-services --cluster devSanmarinoZoo \
  --services sanmarino-front-task-service-zp2f403l \
  --region us-east-2 \
  --query 'services[0].{running:runningCount,desired:desiredCount}'

# URL del ALB
aws elbv2 describe-load-balancers --region us-east-2 \
  --query 'LoadBalancers[?contains(LoadBalancerName, `sanmarino`)].DNSName' \
  --output text
```

---

## ⚠️ Puntos a Validar

1. **ecs-taskdef-new-aws.json** debe existir en `backend/` (referenciado por el script).
2. **Connection string RDS** en la Task Definition apunta a producción.
3. **Tiempo total Backend:** ~25-30 min (el build Docker es el cuello de botella).
4. **Orden recomendado:** Backend primero, luego Frontend (el frontend consume la API del backend).

---

## 🛠️ Troubleshooting

| Problema | Acción |
|----------|--------|
| "Fallo en login a ECR" | Verificar credenciales AWS y permisos ECR |
| "Fallo en docker buildx build" | Verificar `docker buildx` y builder multi-plataforma |
| "El servicio no está corriendo" | Revisar logs: `aws logs tail /ecs/sanmarino-back-task --region us-east-2` |
| Timeout en `services-stable` | Puede tardar más; revisar health checks y seguridad en el target group |
