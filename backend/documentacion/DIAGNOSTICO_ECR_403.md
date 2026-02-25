# Diagnóstico profundo: Error 403 ECR al hacer push

## Resumen
El usuario `moisesmurillo@sanmarino.com.co` recibe **403 Forbidden** al intentar push/pull con Docker a ECR, aunque la API de AWS funciona correctamente.

## Hallazgos

### ✅ Lo que SÍ funciona
| Operación | Resultado |
|-----------|-----------|
| `aws ecr get-authorization-token` | OK |
| `aws ecr batch-get-image` (frontend) | OK |
| `aws ecr batch-get-image` (backend) | OK |
| `aws ecr describe-images` | OK |
| `aws ecr describe-repositories` | OK |

### ❌ Lo que FALLA con 403
| Operación | Error |
|-----------|-------|
| `docker pull` (frontend) | 403 HEAD manifests |
| `docker pull` (backend) | 403 HEAD manifests |
| `docker push` (frontend) | 403 HEAD manifests |
| `docker push` (backend) | 403 HEAD manifests |
| `curl -H "Authorization: Basic $TOKEN" HEAD .../manifests/latest` | 403 |

### Análisis
- El 403 ocurre en el **Docker Registry API** (HTTP HEAD/GET a `/v2/.../manifests/...`)
- La **API de AWS** (BatchGetImage, etc.) funciona con las mismas credenciales
- El token de `get-login-password` se usa correctamente
- Probado: auth embebido en config.json, DOCKER_CONFIG temporal, credHelpers — todos fallan
- **Backend y frontend** tienen el mismo comportamiento (no es específico del repo)

## Causa raíz probable
El endpoint del **Registry API de ECR** parece estar rechazando las solicitudes del protocolo Docker (HEAD/GET) aunque el usuario tenga `ecr:BatchGetImage`. Posibles causas:

1. **Política IAM restrictiva** — Alguna condición (Condition) en la política podría estar bloqueando el acceso vía Registry API
2. **SCP (Service Control Policy)** — Si la cuenta está en AWS Organizations, un SCP podría restringir ECR
3. **Permission boundary** — El usuario podría tener un permission boundary que no incluye el acceso al Registry API
4. **Diferencia API vs Registry** — ECR podría mapear el Registry API a acciones IAM diferentes no documentadas

## Acciones recomendadas para el administrador AWS

### 1. Verificar políticas del usuario
```bash
# Listar políticas adjuntas (requiere permisos IAM)
aws iam list-attached-user-policies --user-name moisesmurillo@sanmarino.com.co
aws iam list-user-policies --user-name moisesmurillo@sanmarino.com.co
```

### 2. Asignar política ECR amplia temporalmente
Para descartar permisos, asignar la política managed:
```
AmazonEC2ContainerRegistryFullAccess
```

Si con esta política el push funciona, el problema es de permisos granulares.

### 3. Revisar SCP y Permission Boundaries
- En AWS Organizations → Service control policies
- En IAM → User → Permission boundary

### 4. Crear rol para CodeBuild (workaround)
Si no se resuelve pronto, crear un proyecto CodeBuild con un rol que tenga `AmazonEC2ContainerRegistryFullAccess` y ejecutar el build allí. El código se sube (zip/S3 o CodeCommit) y CodeBuild hace el build+push con su rol.

## Script de deploy actual
El script `frontend/scripts/deploy-frontend-ecs.sh` está correctamente configurado con:
- Login usando solo registry host
- `--provenance=false --sbom=false` para evitar attestations
- Pasos 3-7 (Task Definition, ECS update) funcionan — el bloqueo es solo en el push de la imagen
