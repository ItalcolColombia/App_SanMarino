# Cómo obtener más detalle del error al hacer push del frontend a ECR

---

## 1. Docker/buildx con más salida (en tu máquina)

Para ver todo lo que hace Docker y el mensaje exacto del registro:

```bash
# Variable para que Docker muestre más detalle
export DOCKER_BUILDKIT=1
export BUILDKIT_PROGRESS=plain

cd /Users/chelsycardona/Documents/App_SanMarino/frontend
export ECR_URI="196080479890.dkr.ecr.us-east-2.amazonaws.com/sanmarino/zootecnia/granjas/frontend"
export TAG=$(date +%Y%m%d-%H%M)

# Push con salida completa (sin ocultar nada)
docker buildx build \
  --platform linux/amd64 \
  --provenance=false \
  --sbom=false \
  -t ${ECR_URI}:${TAG} \
  -t ${ECR_URI}:latest \
  --push \
  . 2>&1 | tee /tmp/frontend-push-log.txt
```

Al final del archivo `/tmp/frontend-push-log.txt` verás la línea completa del error. Puedes compartir ese archivo o las últimas 50 líneas.

---

## 2. CloudTrail en AWS (quién ve el 403)

CloudTrail registra las llamadas a la API de ECR. Si está habilitado, el administrador puede ver qué llamada devuelve 403 y con qué código de error.

### En la consola AWS

1. **CloudTrail** → **Event history**.
2. Filtros sugeridos:
   - **Event name:** `PutImage` o `BatchGetImage` (el HEAD puede aparecer como una de estas).
   - **Resource name:** `sanmarino/zootecnia/granjas/frontend`.
   - **Time range:** día/hora en que hiciste el push.
3. Abrir el evento y revisar:
   - **Error code** (ej. `AccessDenied`, `403`).
   - **Error message** (mensaje que devuelve ECR).
   - **User identity** (usuario/rol que hizo la llamada).

### Con AWS CLI (si tienes permisos)

```bash
# Eventos recientes de la cuenta (ajusta StartTime/EndTime)
aws cloudtrail lookup-events \
  --lookup-attributes AttributeKey=ResourceType,AttributeValue=AWS::ECR::Repository \
  --start-time "2026-02-24T00:00:00Z" \
  --end-time "2026-02-25T00:00:00Z" \
  --region us-east-2 \
  --max-results 20 \
  --query 'Events[*].{Time:EventTime,Name:EventName,User:Username,Resource:Resources[0].ResourceName}' \
  --output table
```

Para ver el detalle de un evento (incluido el error):

```bash
# Sustituir EVENT_ID por el Id de un evento que corresponda al push
aws cloudtrail get-event-selectors --trail-name TU_TRAIL_NAME
# O desde Event history, descargar/ver el evento y buscar "errorCode", "errorMessage"
```

El administrador puede buscar por usuario `moisesmurillo@sanmarino.com.co` y por recurso `sanmarino/zootecnia/granjas/frontend` para ver el evento que devuelve 403 y el mensaje exacto.

---

## 3. Reproducir el “HEAD” con la CLI de ECR

El 403 aparece en una petición tipo “HEAD” al manifest. Eso suele corresponder a **BatchGetImage** (comprobar si la imagen existe). Puedes probar con tu usuario:

```bash
# Simular que “preguntamos” por una imagen con un tag que aún no existe (como hace el cliente tras el push)
aws ecr batch-get-image \
  --repository-name sanmarino/zootecnia/granjas/frontend \
  --image-ids imageTag=test-head-request \
  --region us-east-2 2>&1
```

- Si aquí recibes **403** (o AccessDenied), el detalle del error que muestre la CLI es el mismo tipo de problema que en el push.
- Si aquí devuelve **ImageNotFoundException** (imagen no existe) pero sin 403, entonces el 403 del push podría ser por otro motivo (por ejemplo, solo en ciertas condiciones o tras PutImage).

Guarda la salida completa (incluido el cuerpo del error) y compártela con el administrador.

---

## 4. Habilitar registro de acceso al registro ECR (si lo ofrece la cuenta)

En la consola de ECR no hay “access logging” como en S3/ALB, pero:

- **CloudTrail** es la fuente principal para ver qué llamada a la API de ECR falla y con qué código/mensaje.
- Si existe **VPC Flow Logs** o algún proxy delante de ECR, no suelen dar detalle útil de 403; lo útil es CloudTrail.

---

## 5. Resumen: qué pedir al administrador

1. **CloudTrail:** que busque eventos de ECR para el usuario `moisesmurillo@sanmarino.com.co` y el repositorio `sanmarino/zootecnia/granjas/frontend` en la fecha/hora del push, y que te pasen:
   - **errorCode**
   - **errorMessage**
   - **requestParameters** / **responseElements** (si no son sensibles)
2. **Tu prueba con `batch-get-image`:** ejecuta el comando de la sección 3, guarda la salida (incluido el error) y compártela.
3. **Log de build/push:** ejecuta el comando de la sección 1 y comparte las últimas líneas de `/tmp/frontend-push-log.txt` donde aparece el 403.

Con eso se puede ver exactamente qué está rechazando AWS (permiso, recurso, condición, etc.) cuando falla el push del frontend.

---

## 6. Nota

La prueba con `batch-get-image` por CLI puede devolver **ImageNotFound** y no 403, porque el 403 ocurre en la petición **HTTP** que hace el cliente Docker al registro (HEAD al manifest). Esa petición la registra **CloudTrail** como actividad del registro; el administrador debe revisar CloudTrail en la hora exacta del push para ver el evento con **errorCode** y **errorMessage** del 403.
