# Validación de Permisos AWS - ECR y ECS

**Fecha:** 24-Feb-2026  
**Usuario validado:** `moisesmurillo@sanmarino.com.co`  
**Cuenta:** 196080479890  
**Región:** us-east-2

---

## 1. IDENTIDAD AWS

| Campo | Valor |
|-------|-------|
| **User ID** | AIDAS3J2OV2JKVBKOMNUN |
| **Account** | 196080479890 |
| **ARN** | arn:aws:iam::196080479890:user/moisesmurillo@sanmarino.com.co |

---

## 2. PERMISOS VALIDADOS (operaciones exitosas)

### ECR - Repositorio Frontend (`sanmarino/zootecnia/granjas/frontend`)

| Operación | Estado | Notas |
|-----------|--------|-------|
| `ecr:DescribeRepositories` | ✅ Permitido | Puede leer info del repo |
| `ecr:GetAuthorizationToken` | ✅ Permitido | Login a ECR funciona |
| `ecr:BatchGetImage` | ✅ Permitido | Puede leer/manifest (HEAD) |
| `ecr:InitiateLayerUpload` | ✅ Permitido | Puede iniciar subida de capas |
| `ecr:PutImage` | ❓ No verificado directamente | Fallo 403 en push Docker |

### ECS - Cluster y Servicios

| Operación | Estado | Notas |
|-----------|--------|-------|
| `ecs:DescribeClusters` | ✅ Permitido | Cluster devSanmarinoZoo activo |
| `ecs:DescribeServices` | ✅ Permitido | Servicio frontend activo |
| `ecs:UpdateService` | ✅ Permitido | Puede forzar nuevo despliegue |
| `ecs:DescribeTaskDefinition` | ✅ Permitido | TD sanmarino-front-task:42 |
| `ecs:RegisterTaskDefinition` | ✅ Permitido (implícito) | Se usa en deploy |

### IAM (no autorizado a consultar)

| Operación | Estado |
|-----------|--------|
| `iam:ListAttachedUserPolicies` | ❌ AccessDenied |
| `iam:ListUserPolicies` | ❌ AccessDenied |
| `iam:ListGroupsForUser` | ❌ AccessDenied |
| `iam:SimulatePrincipalPolicy` | ❌ AccessDenied |

---

## 3. POLÍTICAS DE REPOSITORIO ECR

**Estado actual (alineado):**

| Repositorio | Política |
|-------------|----------|
| Backend | Sin política |
| Frontend | Sin política |

Ambos repositorios usan solo los permisos IAM del usuario para ECR. No hay políticas a nivel de repositorio.

---

## 4. CONFIGURACIÓN REPOSITORIOS

| Atributo | Backend | Frontend |
|----------|---------|----------|
| imageTagMutability | MUTABLE | MUTABLE |
| scanOnPush | false | false |
| encryptionType | AES256 | AES256 |
| Política de repo | **Ninguna** | **Ninguna** |

---

## 5. ANÁLISIS DEL ERROR 403

### Síntoma

```
failed to push .../sanmarino/zootecnia/granjas/frontend:TAG: 
unexpected status from HEAD request to .../manifests/TAG: 403 Forbidden
```

### Momento del fallo

1. ✅ Login a ECR — éxito
2. ✅ Build de imagen — éxito
3. ✅ Push de capas (layers) — éxito
4. ✅ Push de manifest — según logs "done"
5. ❌ **HEAD request** (verificación post-push) — **403 Forbidden**

### Causas probables

1. **Política de repositorio en frontend**  
   El backend no tiene política y funciona; el frontend sí tiene. Una política mal alineada con las acciones reales del registro puede restringir el HEAD de verificación que hace buildx.

2. **Permisos IAM específicos por recurso**  
   Es posible que las políticas IAM permitan acciones solo sobre ciertos repos (p. ej. `sanmarino/zootecnia/granjas/backend`) y no sobre el de frontend. No se puede confirmar porque no hay permiso para listar o simular políticas.

3. **Comportamiento de Docker Buildx**  
   Buildx hace un HEAD al manifest tras el push para validar. Ese HEAD puede usar `ecr:BatchGetImage` u otra acción distinta a las que se probaron directamente.

---

## 6. ACCIONES RECOMENDADAS

### Opción A: ✅ Aplicada — Política eliminada

La política del repositorio frontend fue eliminada. Backend y frontend están alineados (ambos sin política de repositorio).

### Opción B: Revisar IAM con el administrador

1. Verificar que el usuario tenga permisos ECR amplios, por ejemplo:
   - `AmazonEC2ContainerRegistryPowerUser`, o
   - Una política con `ecr:*` sobre `arn:aws:ecr:us-east-2:196080479890:repository/sanmarino/zootecnia/granjas/*`
2. Confirmar que no existan `Deny` explícitos para el repositorio frontend.

### Opción C: Crear un nuevo repositorio de prueba

1. Crear un repo nuevo para frontend (p. ej. `sanmarino/zootecnia/granjas/frontend-v2`).
2. No añadir ninguna política de repositorio.
3. Actualizar el script de deploy para usar ese repo y probar el push.

---

## 7. ESTADO ACTUAL DEL SERVICIO ECS FRONTEND

| Campo | Valor |
|-------|-------|
| Cluster | devSanmarinoZoo |
| Servicio | sanmarino-front-task-service-zp2f403l |
| Estado | ACTIVE |
| Running tasks | 1 |
| Task Definition | sanmarino-front-task:42 |

---

## 8. CONCLUSIÓN

- Los permisos validados (ECR y ECS) permiten leer, iniciar subida de capas y actualizar el servicio.
- El 403 ocurre en el paso de verificación del manifest tras el push.
- La diferencia más evidente entre backend y frontend es la existencia de una política de repositorio solo en frontend.
- Se recomienda probar primero **quitar la política del repositorio frontend** (Opción A) y reintentar el despliegue.
