# CI/CD Pipeline — San Marino App (Producción)

Fecha de configuración: 2026-05-21  
Repositorio: https://github.com/ItalcolColombia/App_SanMarino  
Archivo del pipeline: `.github/workflows/deploy-production.yml`

---

## Resumen

El pipeline automatiza el despliegue a producción en AWS cuando un Pull Request es mergeado a la rama `main-produccion`. Detecta qué cambió (backend, frontend o ambos) y despliega solo lo necesario, construyendo imágenes Docker y actualizando los servicios ECS mediante rolling deployment sin downtime.

---

## Flujo de trabajo

```
Developer → crea PR desde su rama hacia main-produccion
                    ↓
            PR aprobado y mergeado
                    ↓
     [Job 1] Detectar cambios en el PR
      ├── backend/** cambió?  → [Job 2] Deploy Backend → ECR → ECS
      └── frontend/** cambió? → [Job 3] Deploy Frontend → ECR → ECS
```

Los jobs 2 y 3 corren en **paralelo** si ambos servicios tienen cambios.

---

## Trigger (cuándo se ejecuta)

| Evento | Condición | Resultado |
|--------|-----------|-----------|
| `pull_request` tipo `closed` | PR **mergeado** a `main-produccion` | Detecta cambios y despliega |
| `pull_request` tipo `closed` | PR cerrado sin merge (rechazado) | **No despliega** |
| `workflow_dispatch` | Ejecución manual desde GitHub Actions | Despliega según inputs del usuario |

> Un PR cerrado sin merge (rechazado) **no activa el despliegue** gracias a la condición `github.event.pull_request.merged == true`.

---

## Infraestructura AWS involucrada

| Recurso | Valor |
|---------|-------|
| Región | `us-east-2` |
| Cuenta AWS | `196080479890` |
| Cluster ECS | `devSanmarinoZoo` |
| ECR Backend | `sanmarino/zootecnia/granjas/backend` |
| ECR Frontend | `sanmarino/zootecnia/granjas/frontend` |
| Servicio ECS Backend | `sanmarino-back-task-service-75khncfa` |
| Servicio ECS Frontend | `sanmarino-front-task-service-zp2f403l` |
| Task Family Backend | `sanmarino-back-task` |
| Task Family Frontend | `sanmarino-front-task` |

---

## Autenticación AWS — OIDC (sin claves estáticas)

El pipeline **no usa `AWS_ACCESS_KEY_ID` ni `AWS_SECRET_ACCESS_KEY`**. En su lugar usa OpenID Connect (OIDC), que permite que GitHub solicite un token temporal a AWS asumiendo un rol IAM.

### Rol IAM

```
ARN: arn:aws:iam::196080479890:role/github-actions-deploy
```

### Trust Policy del rol (configurada en AWS IAM)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Federated": "arn:aws:iam::196080479890:oidc-provider/token.actions.githubusercontent.com"
      },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "token.actions.githubusercontent.com:aud": "sts.amazonaws.com"
        },
        "StringLike": {
          "token.actions.githubusercontent.com:sub": "repo:ItalcolColombia/App_SanMarino:ref:refs/heads/main-produccion"
        }
      }
    }
  ]
}
```

### OIDC Provider en IAM

Debe existir en AWS IAM → Identity Providers:
- **URL:** `https://token.actions.githubusercontent.com`
- **Audience:** `sts.amazonaws.com`

### Permisos mínimos que necesita el rol

- `AmazonEC2ContainerRegistryPowerUser` — push de imágenes a ECR
- `ecs:DescribeTaskDefinition`
- `ecs:RegisterTaskDefinition`
- `ecs:UpdateService`
- `ecs:DescribeServices`
- `iam:PassRole` — para el execution role de ECS

---

## Descripción de cada Job

### Job 1 — Detectar cambios (`changes`)

Usa `dorny/paths-filter@v3` para comparar los archivos del PR contra la base `main-produccion` y determinar si hubo cambios en `backend/**` o `frontend/**`.

- Solo corre si el PR fue mergeado o si el trigger es manual
- Expone dos outputs: `backend` y `frontend` (valores `true` / `false`)
- Los jobs 2 y 3 dependen de estos outputs para decidir si corren

### Job 2 — Deploy Backend (`deploy-backend`)

**Condición:** `backend == 'true'` (o activación manual con input `deploy_backend`)

Pasos:
1. Checkout del código
2. Autenticación en AWS vía OIDC
3. Login a Amazon ECR
4. Pull de la imagen `latest` anterior (para usar como cache de Docker)
5. Build de la imagen con dos tags: `<git-sha>` y `latest`
6. Push de ambos tags a ECR
7. Descarga la task definition actual de ECS (para no sobreescribir secrets de producción)
8. Actualiza solo el campo `image` con el nuevo SHA
9. Registra la nueva revisión de task definition
10. Actualiza el servicio ECS con rolling deployment (espera estabilidad, máx 15 min)

### Job 3 — Deploy Frontend (`deploy-frontend`)

**Condición:** `frontend == 'true'` (o activación manual con input `deploy_frontend`)

Idéntico al backend en su flujo, con sus propios valores de ECR, ECS service y task family. El Dockerfile del frontend compila Angular con `--configuration docker` (usa `environment.prod.ts`).

---

## Estrategia de tags de imagen Docker

Cada build genera **dos tags**:

| Tag | Ejemplo | Propósito |
|-----|---------|-----------|
| SHA del commit | `abc1234def...` | Trazabilidad — saber exactamente qué código está corriendo |
| `latest` | `latest` | Referencia rápida + cache para el siguiente build |

```
196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend:<sha>
196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/backend:latest
```

---

## Estrategia de despliegue ECS

Se usa **rolling deployment** con la siguiente lógica:

1. Se descarga la task definition vigente desde ECS con `aws ecs describe-task-definition`
2. Se eliminan los campos de solo lectura (`taskDefinitionArn`, `revision`, `status`, etc.)
3. Se reemplaza únicamente el campo `image` del contenedor con el nuevo SHA
4. Se registra como nueva revisión en ECS
5. Se actualiza el servicio ECS con `force-new-deployment: true`
6. El pipeline espera hasta 15 minutos a que el servicio esté estable

> Este enfoque garantiza que los secrets y variables de entorno de producción (que viven en la task definition de ECS) **nunca pasan por GitHub Actions**.

---

## Despliegue manual

Desde GitHub: **Actions → Deploy to Production → Run workflow**

Inputs disponibles:
- `deploy_backend` (boolean, default: `true`) — desplegar el backend
- `deploy_frontend` (boolean, default: `true`) — desplegar el frontend

Útil para forzar un redespliegue sin hacer cambios de código.

---

## Variables de entorno del pipeline

Definidas en la sección `env` global del workflow:

```yaml
AWS_REGION: us-east-2
AWS_ACCOUNT_ID: "196080479890"
ECS_CLUSTER: devSanmarinoZoo
```

No se usan GitHub Secrets para credenciales AWS gracias a OIDC.

---

## Historial de cambios importantes

| Fecha | Cambio | Motivo |
|-------|--------|--------|
| 2026-05-14 | Primera configuración del pipeline | Automatizar despliegue a AWS ECS |
| 2026-05-21 | Fix: eliminar detección de cambios basada en diff de ramas | `dorny/paths-filter` comparaba `main` vs `main-produccion`; tras fast-forward merge el diff era 0 y los jobs se saltaban siempre |
| 2026-05-21 | Cambio de trigger: `push` → `pull_request [closed]` | El trigger correcto es el merge del PR; con eventos `pull_request` la detección de cambios funciona correctamente |

---

## Solución de problemas frecuentes

### El pipeline no se ejecuta al hacer merge del PR

- Verificar que el PR tiene `main-produccion` como rama destino (base)
- Confirmar que el PR fue **mergeado** y no solo cerrado

### Error en el step "Configurar credenciales AWS (OIDC)"

```
Could not assume role with OIDC
```

Causas posibles:
1. El OIDC provider no está configurado en AWS IAM para `token.actions.githubusercontent.com`
2. La trust policy del rol no incluye el repositorio `ItalcolColombia/App_SanMarino`
3. El rol `github-actions-deploy` no existe en la cuenta `196080479890`

### Los jobs de backend/frontend se saltan aunque hubo cambios

Ocurre si `dorny/paths-filter` no detecta cambios. Con el trigger `pull_request` esto no debería pasar porque compara correctamente el PR branch vs la base. Si ocurre, usar el trigger manual (`workflow_dispatch`) como alternativa.

### El despliegue ECS falla o tarda más de 15 minutos

- Revisar los logs del contenedor en CloudWatch: **ECS → Cluster devSanmarinoZoo → Servicio → Logs**
- El servicio puede fallar si la nueva imagen tiene errores de inicio (health check falla)
- Aumentar `wait-for-minutes` en el workflow si el build de la imagen es muy pesado

---

## Archivos relacionados

| Archivo | Descripción |
|---------|-------------|
| `.github/workflows/deploy-production.yml` | Definición completa del pipeline |
| `backend/Dockerfile` | Imagen Docker del backend (.NET 9) |
| `frontend/Dockerfile` | Imagen Docker del frontend (Angular 20 + nginx) |
| `backend/documentacion/COMANDOS_ADMIN_AWS_ECR.md` | Comandos útiles para ECR |
| `backend/documentacion/ESTADO_CONFIGURACION_AWS.md` | Estado general de la infraestructura AWS |
| `backend/documentacion/CHECKLIST_MIGRACION_AWS.md` | Checklist de configuración inicial AWS |
