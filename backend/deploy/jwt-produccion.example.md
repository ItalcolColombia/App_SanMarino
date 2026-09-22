# JWT de producción — ejemplo completo para la TaskDef de ECS

> Plan y motivo: [`fase_de_desarrollo/jwt_firma_produccion_plan.md`](../../fase_de_desarrollo/jwt_firma_produccion_plan.md).
> **Nunca** pegues la clave real en este archivo, ni en ningún `.json` de `backend/deploy/`: así se filtró
> la anterior (los snapshots de TaskDef versionados la traen en texto plano).

| Dato | Valor |
|---|---|
| Región | `us-east-2` |
| Cluster | `devSanmarinoZoo` |
| Servicio | `sanmarino-back-task-service-75khncfa` |
| Familia de TaskDef | `sanmarino-back-task` |
| Rol de ejecución | `ecsTaskExecutionRole` |
| Algoritmo | HS256 (`JwtAuthenticationConfiguration` solo acepta `HmacSha256`) |

La API lee `JwtSettings:*` de la configuración; en ECS eso llega como variables de entorno con doble
guion bajo (`JwtSettings__Key`), que **pisan** a `appsettings.json`. Si `JwtSettings__Key` falta, .NET
usa la clave de desarrollo que viaja en la imagen. Desde este cambio, en `Production` eso **no arranca**.

---

## 1. Generar la clave (64 bytes aleatorios → 88 caracteres base64)

PowerShell. La clave va directo a Secrets Manager y **no se muestra en pantalla**:

```powershell
$b = New-Object byte[] 64
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
$clave = [Convert]::ToBase64String($b)
aws secretsmanager create-secret --region us-east-2 `
  --name sanmarino/produccion/jwt-key `
  --description "Clave HS256 de firma JWT del API ZooSanMarino" `
  --secret-string $clave
Remove-Variable clave, b
```

La respuesta trae el `ARN` del secreto (termina en `-XXXXXX`, 6 caracteres que agrega AWS). Anotalo.

Para **rotar** más adelante: mismo bloque, pero `aws secretsmanager put-secret-value --secret-id
sanmarino/produccion/jwt-key --secret-string $clave` y forzar un deploy (paso 4).

## 2. Permiso para que ECS lea el secreto

`ecsTaskExecutionRole` necesita esta política inline (IAM → Roles → ecsTaskExecutionRole → Add
permissions → Create inline policy → JSON):

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

Sin este permiso la tarea no arranca (`ResourceInitializationError: unable to pull secrets`).

## 3. Fragmento de la TaskDef — `containerDefinitions[0]`

### Opción A (recomendada): clave desde Secrets Manager

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
    }
  ]
}
```

- **Sacá `JwtSettings__Key` de `environment`**: no puede quedar en los dos lados.
- Issuer y Audience se dejan **iguales** a los actuales para no romper la app móvil ni el front.
- El resto de las variables que ya tiene la TaskDef (conexión, CORS, `ReverseProxy__*`, etc.) no se tocan.

### Opción B: variable directa (solo si no se puede usar Secrets Manager)

```json
{
  "environment": [
    { "name": "ASPNETCORE_ENVIRONMENT",         "value": "Production" },
    { "name": "JwtSettings__Key",               "value": "REEMPLAZAR_CON_CLAVE_GENERADA_DE_64_BYTES" },
    { "name": "JwtSettings__Issuer",            "value": "ZooSanMarino.API" },
    { "name": "JwtSettings__Audience",          "value": "ZooSanMarino.Client" },
    { "name": "JwtSettings__DurationInMinutes", "value": "60" }
  ]
}
```

Reemplazá el placeholder por la salida de `[Convert]::ToBase64String($b)`. Si se olvida, la API
**no arranca** con ese valor (la guarda reconoce `REEMPLAZAR`). Desventaja: la clave queda visible para
cualquiera con `ecs:DescribeTaskDefinition` y en cualquier snapshot que se guarde.

## 4. Aplicar (consola de AWS, lo más simple)

1. ECS → Task definitions → `sanmarino-back-task` → última revisión → **Create new revision**.
2. Contenedor → Environment variables: `JwtSettings__Key` → tipo **ValueFrom** → el ARN del paso 1
   (opción A) o **Value** → la clave (opción B).
3. Create. Anotá el número de revisión nuevo.
4. ECS → Clusters → `devSanmarinoZoo` → servicio `sanmarino-back-task-service-75khncfa` → Update →
   esa revisión → **Force new deployment** → Update.

El workflow de deploy baja **la última revisión** de la familia y solo le cambia la imagen, así que
la clave queda puesta para todos los deploys siguientes.

## 5. Verificar (no confiar en el "completado" del CLI)

```bash
# ¿Qué revisión corre y terminó el rollout?
aws ecs describe-services --cluster devSanmarinoZoo --services sanmarino-back-task-service-75khncfa --region us-east-2 \
  --query 'services[0].deployments[].{Status:status,Rollout:rolloutState,TaskDef:taskDefinition}'

# ¿Esa revisión trae la clave desde el secreto? (solo nombres, no valores)
aws ecs describe-task-definition --task-definition sanmarino-back-task --region us-east-2 \
  --query 'taskDefinition.containerDefinitions[0].{secrets:secrets[].name,env:environment[?starts_with(name,`JwtSettings`)].name}'
```

Esperado: `rolloutState = COMPLETED` en la revisión nueva, `secrets` con `JwtSettings__Key` y `env` **sin**
`JwtSettings__Key`. Después: iniciar sesión en la web y en la app móvil.

## ⚠️ Antes de hacerlo

- **Todos vuelven a iniciar sesión.** Cambiar la clave invalida todos los tokens vivos. Hacerlo en
  horario de baja operación; la app móvil con registros offline pendientes también pide login.
- **Orden:** primero la clave en la TaskDef (con la imagen actual). Recién después desplegar el commit
  que agrega la guarda. Al revés, si la TaskDef no tiene la clave, la tarea nueva no arranca y ECS
  hace rollback silencioso a la versión anterior.
- La clave que hoy figura en `backend/deploy/*.json` está en el historial de git: tratala como
  filtrada aunque siga siendo la de producción.
