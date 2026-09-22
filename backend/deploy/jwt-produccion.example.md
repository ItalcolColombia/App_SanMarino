# JWT de producción — clave nueva en cada deploy (ejemplo completo)

> Plan y motivo: [`fase_de_desarrollo/jwt_firma_produccion_plan.md`](../../fase_de_desarrollo/jwt_firma_produccion_plan.md).
> **Nunca** pegues una clave real en este archivo ni en ningún `.json` de `backend/deploy/`: así se
> filtró la anterior (los snapshots de TaskDef versionados la traen en texto plano).

| Dato | Valor |
|---|---|
| Región | `us-east-2` |
| Cluster | `devSanmarinoZoo` |
| Servicio | `sanmarino-back-task-service-75khncfa` |
| Familia de TaskDef | `sanmarino-back-task` (contenedor `backend`, Fargate) |
| Secreto | `sanmarino/produccion/jwt-key` |
| Rol de ejecución (lee el secreto) | `ecsTaskExecutionRole` |
| Rol del pipeline (rota el secreto) | `github-actions-deploy` |
| Algoritmo | HS256 (`JwtAuthenticationConfiguration` solo acepta `HmacSha256`) |

## Cómo funciona

1. Cada push a `main-produccion` corre `.github/workflows/deploy-production.yml`. En el job del back,
   **antes** del deploy:
   - **Verifica** que la TaskDef tome `JwtSettings__Key` y `JwtSettings__PreviousKey` del secreto. Si no,
     corta el job sin tocar nada.
   - **Rota**: genera 64 bytes aleatorios (enmascarados, nunca salen en el log) y los guarda como versión
     nueva del secreto. Secrets Manager mueve sola la etiqueta: la nueva queda `AWSCURRENT` y la de
     antes pasa a `AWSPREVIOUS`.
2. ECS lee los secretos **al arrancar cada tarea**:
   - `JwtSettings__Key` ← `AWSCURRENT` → la API **firma** con esta.
   - `JwtSettings__PreviousKey` ← `AWSPREVIOUS` → la API **valida** también con esta.
3. Resultado: los tokens emitidos antes del deploy siguen valiendo hasta vencer
   (`JwtSettings__DurationInMinutes`, hoy 60). **El deploy no desloguea a nadie.**

`appsettings.json` no participa: la variable de la TaskDef lo pisa, y si faltara, la API se niega a
arrancar en `Production` con la clave del repo.

---

## Setup (una sola vez)

### 1. Crear el secreto con dos versiones

PowerShell. La clave va directo a Secrets Manager y **no se muestra en pantalla**:

```powershell
function Nueva-ClaveJwt {
  $b = New-Object byte[] 64
  [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
  [Convert]::ToBase64String($b)
}
aws secretsmanager create-secret --region us-east-2 `
  --name sanmarino/produccion/jwt-key `
  --description "Clave HS256 de firma JWT del API ZooSanMarino (rota en cada deploy)" `
  --secret-string (Nueva-ClaveJwt) --query ARN --output text
aws secretsmanager put-secret-value --region us-east-2 `
  --secret-id sanmarino/produccion/jwt-key --secret-string (Nueva-ClaveJwt) --query VersionId --output text
```

El primer comando imprime el **ARN completo** (termina en `-XXXXXX`, 6 caracteres que agrega AWS):
anotalo. El segundo deja creada la versión `AWSPREVIOUS`; sin ella, una tarea que arranque antes del
primer deploy del pipeline falla con `ResourceInitializationError`.

### 2. Permiso de lectura para ECS — `ecsTaskExecutionRole`

IAM → Roles → `ecsTaskExecutionRole` → Add permissions → Create inline policy → JSON:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "LeerClaveJwtProduccion",
      "Effect": "Allow",
      "Action": "secretsmanager:GetSecretValue",
      "Resource": "arn:aws:secretsmanager:us-east-2:<ACCOUNT_ID>:secret:sanmarino/produccion/jwt-key-*"
    }
  ]
}
```

### 3. Permiso de rotación para el pipeline — `github-actions-deploy`

Mismo camino, sobre el rol `github-actions-deploy`:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "RotarClaveJwtProduccion",
      "Effect": "Allow",
      "Action": "secretsmanager:PutSecretValue",
      "Resource": "arn:aws:secretsmanager:us-east-2:<ACCOUNT_ID>:secret:sanmarino/produccion/jwt-key-*"
    }
  ]
}
```

### 4. TaskDef — fragmento de `containerDefinitions[0]` (contenedor `backend`)

```json
{
  "environment": [
    { "name": "ASPNETCORE_ENVIRONMENT",         "value": "Production" },
    { "name": "JwtSettings__Issuer",            "value": "ZooSanMarino.API" },
    { "name": "JwtSettings__Audience",          "value": "ZooSanMarino.Client" },
    { "name": "JwtSettings__DurationInMinutes", "value": "60" }
  ],
  "secrets": [
    {
      "name": "JwtSettings__Key",
      "valueFrom": "arn:aws:secretsmanager:us-east-2:<ACCOUNT_ID>:secret:sanmarino/produccion/jwt-key-XXXXXX"
    },
    {
      "name": "JwtSettings__PreviousKey",
      "valueFrom": "arn:aws:secretsmanager:us-east-2:<ACCOUNT_ID>:secret:sanmarino/produccion/jwt-key-XXXXXX::AWSPREVIOUS:"
    }
  ]
}
```

- `-XXXXXX` = el sufijo real del ARN del paso 1. En `PreviousKey` va el ARN completo seguido de
  `::AWSPREVIOUS:` (clave JSON vacía, etapa `AWSPREVIOUS`, versión vacía).
- **Sacá `JwtSettings__Key` de `environment`**: el pipeline corta el deploy si sigue ahí.
- Issuer y Audience se dejan **iguales** a los actuales para no romper la app móvil ni el front.
- El resto de las variables que ya tiene la TaskDef (conexión, CORS, `ReverseProxy__*`, etc.) no se tocan.

En la consola: ECS → Task definitions → `sanmarino-back-task` → última revisión → **Create new
revision** → contenedor `backend` → Environment variables → `JwtSettings__Key` y
`JwtSettings__PreviousKey` con tipo **ValueFrom** → Create.

### 5. Arrancar con la revisión nueva

ECS → `devSanmarinoZoo` → `sanmarino-back-task-service-75khncfa` → Update → la revisión nueva →
**Force new deployment**. A partir de acá, cada deploy del pipeline rota la clave solo.

## Verificar

```bash
# ¿La revisión que corre terminó el rollout?
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-back-task-service-75khncfa --region us-east-2 \
  --query 'services[0].deployments[].{Status:status,Rollout:rolloutState,TaskDef:taskDefinition}'

# ¿Toma las dos claves del secreto? (solo nombres, nunca valores)
aws ecs describe-task-definition --task-definition sanmarino-back-task --region us-east-2 \
  --query 'taskDefinition.containerDefinitions[0].{secrets:secrets[].name,env:environment[?starts_with(name,`JwtSettings`)].name}'

# ¿Rotó en el último deploy? (fecha y etiquetas de cada versión, sin valores)
aws secretsmanager list-secret-version-ids --secret-id sanmarino/produccion/jwt-key --region us-east-2 \
  --query 'Versions[].{Etapas:VersionStages,Creada:CreatedDate}'
```

Esperado: `rolloutState = COMPLETED`; `secrets` con `JwtSettings__Key` y `JwtSettings__PreviousKey`;
`env` **sin** claves; una versión `AWSCURRENT` con la fecha del último deploy. En el log del job:
`Clave JWT rotada (version …)`.

## Qué esperar en cada deploy

- **Nadie pierde la sesión por el deploy**: los tokens viejos se validan con `AWSPREVIOUS`.
- **Ventana del rollout (minutos)**: mientras conviven tareas viejas y nuevas detrás del ALB, un token
  recién emitido por una tarea nueva puede caer en una vieja (que no conoce la clave nueva) y dar 401.
  A lo sumo algún usuario vuelve a iniciar sesión una vez.
- **Dos deploys en menos de `DurationInMinutes`**: `AWSPREVIOUS` cubre un solo paso; los tokens de dos
  claves atrás dejan de valer y esos usuarios vuelven a entrar.
- **Rollback de ECS después de rotar**: las tareas del rollback también leen la clave nueva. Coherente.
- **`update-service --force-new-deployment` a mano** no rota: reinicia con la misma clave.

## Emergencia: la clave se filtró

Rotar **dos veces** (así ni `AWSCURRENT` ni `AWSPREVIOUS` son la filtrada) y forzar un deploy. Usa
la función `Nueva-ClaveJwt` del paso 1 (definila en la misma consola):

```powershell
aws secretsmanager put-secret-value --region us-east-2 --secret-id sanmarino/produccion/jwt-key --secret-string (Nueva-ClaveJwt) --query VersionId --output text
aws secretsmanager put-secret-value --region us-east-2 --secret-id sanmarino/produccion/jwt-key --secret-string (Nueva-ClaveJwt) --query VersionId --output text
aws ecs update-service --region us-east-2 --cluster devSanmarinoZoo --service sanmarino-back-task-service-75khncfa --force-new-deployment
```

Eso sí desloguea a todos. Además, la sesión sigue atada a `sesiones_activas`: un token falsificado con
un `jti` inventado se rechaza aunque la firma sea válida.
