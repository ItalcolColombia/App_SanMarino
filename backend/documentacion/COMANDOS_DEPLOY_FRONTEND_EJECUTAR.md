# Comandos que uso para el despliegue del frontend

**Repo ECR:** `sanmarino/zootecnia/granjas/frontend`  
**Tags que ves en el repo:** `20260126-2022`, `latest` (última imagen que sí está en ECR)

---

## Comando único (lo que ejecuto yo)

Desde la raíz del proyecto:

```bash
cd /Users/chelsycardona/Documents/App_SanMarino
make deploy-frontend
```

Ese `make deploy-frontend` hace todo lo que está abajo.

---

## Comandos equivalentes paso a paso

Si quieres ejecutarlos uno por uno (o pasárselos al administrador para que los ejecute):

### 1. Login a ECR

```bash
aws ecr get-login-password --region us-east-2 | docker login --username AWS --password-stdin 196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend
```

*(Debe salir: `Login Succeeded`.)*

### 2. Ir a la carpeta del frontend

```bash
cd /Users/chelsycardona/Documents/App_SanMarino/frontend
```

### 3. Build y push de la imagen (el paso que falla con 403)

El tag se genera con la fecha/hora, por ejemplo `20260224-1530`. Aquí uso un tag fijo para que sea reproducible:

```bash
export TAG=$(date +%Y%m%d-%H%M)
export ECR_URI="196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend"

docker buildx build \
  --platform linux/amd64 \
  --provenance=false \
  --sbom=false \
  -t ${ECR_URI}:${TAG} \
  -t ${ECR_URI}:latest \
  --push \
  .
```

**Error que sale:**  
`unexpected status from HEAD request to .../manifests/TAG: 403 Forbidden`

### 4. Resto del deploy (solo se ejecuta si el paso 3 termina bien)

```bash
# Copiar Task Definition
cp deploy/ecs-taskdef.json ecs-taskdef.json

# Actualizar el tag en el JSON (macOS)
sed -i '' "s|${ECR_URI}:[^\"]*|${ECR_URI}:${TAG}|g" ecs-taskdef.json

# Registrar Task Definition
NEW_TD_ARN=$(aws ecs register-task-definition --cli-input-json file://ecs-taskdef.json --query 'taskDefinition.taskDefinitionArn' --output text --region us-east-2)

# Actualizar servicio ECS
aws ecs update-service --cluster devSanmarinoZoo --service sanmarino-front-task-service-zp2f403l --task-definition $NEW_TD_ARN --force-new-deployment --region us-east-2

# Esperar estabilización
aws ecs wait services-stable --cluster devSanmarinoZoo --services sanmarino-front-task-service-zp2f403l --region us-east-2
```

---

## Resumen para el administrador

| Qué | Valor |
|-----|--------|
| Comando que ejecuto | `make deploy-frontend` (o los pasos de arriba) |
| Repositorio ECR | `sanmarino/zootecnia/granjas/frontend` |
| Último tag que sí está en el repo | `20260126-2022`, `latest` |
| Dónde falla | En `docker buildx build ... --push .` (paso 3), con **403 Forbidden** en el HEAD al manifest |
| Región | `us-east-2` |
| Cuenta | `196080479890` |

Puedes ejecutar tú mismo desde tu máquina:

```bash
cd /Users/chelsycardona/Documents/App_SanMarino
make deploy-frontend
```

y compartir la salida completa (sobre todo el error 403) con el administrador.
