# Firma JWT de producción: clave propia por TaskDef y guarda de arranque (22-sep-2026)

## Problema

El usuario reporta que la credencial JWT de producción "no está aplicada". En ECS la configuración
real llega por variables de entorno de la TaskDef (`JwtSettings__Key`, …), que pisan a
`appsettings.json`. Pero `appsettings.json` **viaja dentro de la imagen** (`Dockerfile`: `COPY ./src`),
está versionado en git y su `JwtSettings:Key` es la clave de desarrollo (`...Development_Only...`,
101 bytes). Si la TaskDef no define `JwtSettings__Key`, .NET cae en esa clave **sin avisar**:
producción firma con una clave que tiene cualquiera con acceso al repo.

Agravante: los snapshots versionados en `backend/deploy/*.json` traen `JwtSettings__Key` en texto plano
(121 bytes en `ecs-taskdef-new-aws.json`, familia `sanmarino-back-task`). Si esa es la clave viva, ya
está en el historial de git ⇒ hay que tratarla como filtrada y **rotarla**.

Atenuante (no reemplaza la rotación): desde B1 un token vale solo si su `jti` está en
`sesiones_activas`; un token falsificado con un `jti` inventado se rechaza igual. Pero la firma es la
primera frontera y no debe depender de eso.

Limitación: las credenciales AWS locales están vencidas (`InvalidClientTokenId`) ⇒ no se puede leer ni
modificar la TaskDef viva desde acá. El usuario aplica el cambio en AWS.

## Enfoque

1. **Guarda de arranque (solo `Production`)**: si la clave efectiva es la de desarrollo o un
   placeholder de los ejemplos del repo, la API **no arranca** con un mensaje claro (sin imprimir la
   clave). Decisión pura en `Application/Calculos/JwtClaveProduccionCalculos.cs` + tests xUnit;
   `Program.cs` solo la llama después de `jwt.EnsureValid()`.
   - Marcas rechazadas: `Development` (appsettings.json / Development.json.example), `YOUR_`
     (appsettings*.json.example), `REEMPLAZAR` (el ejemplo nuevo), `CHANGE_ME`.
   - Las marcas con `_` no pueden salir de una clave base64 estándar (`+/=`), y `Development` tiene 11
     letras ⇒ probabilidad de falso positivo sobre una clave aleatoria despreciable.
   - En `Development`/`Testing` no cambia nada (el dev sigue firmando con su clave local).
   - No se sube el mínimo de 32 bytes (bajo riesgo de voltear una clave productiva válida de 32–63).
2. **Ejemplo completo para la TaskDef**: `backend/deploy/jwt-produccion.example.md` con el fragmento
   de `containerDefinitions[0]` (Secrets Manager recomendado; variable directa como alternativa), el
   comando para generar una clave de 64 bytes aleatorios, el permiso IAM que necesita
   `ecsTaskExecutionRole` y la verificación posterior. Placeholder `REEMPLAZAR_...` ⇒ si se olvida
   cambiarlo, la guarda impide que producción firme con él.

## Archivos

| Archivo | Cambio |
|---|---|
| `backend/src/ZooSanMarino.Application/Calculos/JwtClaveProduccionCalculos.cs` | Nuevo: `MotivoRechazo(string? clave)` → `null` si sirve, mensaje si no. |
| `backend/src/ZooSanMarino.API/Program.cs` | Llamar la guarda cuando `IsProduction()`. |
| `backend/tests/ZooSanMarino.Application.Tests/JwtClaveProduccionCalculosTests.cs` | Nuevo: xUnit. |
| `backend/deploy/jwt-produccion.example.md` | Nuevo: ejemplo completo + pasos. |

Sin cambios de BD, migraciones, frontend ni contratos HTTP. No se toca la TaskDef ni se despliega.

## ⚠️ Orden de despliegue (obligatorio)

1. Primero poner `JwtSettings__Key` real en la TaskDef (nueva revisión) y forzar el deploy de la imagen
   **actual**.
2. Recién después desplegar este commit. Si se despliega antes y la TaskDef no tiene la clave, la tarea
   nueva no arranca y ECS hace **rollback silencioso** a la versión vieja (sigue sirviendo, pero con la
   clave del repo). Es el comportamiento buscado —fail-closed— pero hay que saberlo.
3. Rotar la clave invalida todos los tokens vivos: todos vuelven a iniciar sesión. Hacerlo en horario
   de baja operación (la app móvil con cola offline pendiente también pide login).

## Casos de prueba

- Clave de desarrollo del repo (contiene `Development_Only`) → rechazada.
- `YOUR_JWT_SECRET_KEY_HERE`, `REEMPLAZAR_CON_CLAVE_GENERADA`, `CHANGE_ME...` → rechazadas.
- Vacía / null / espacios → rechazada.
- Mayúsculas/minúsculas distintas de la marca → rechazada.
- Clave base64 aleatoria de 64 bytes → aceptada (varias semillas).
- El mensaje de rechazo no contiene la clave.
- `dotnet build` 0 errores + `dotnet test` de Application.Tests.

---

## Fase 2 — Clave nueva en cada deploy (22-sep-2026, decisión del usuario)

El usuario eligió rotar la clave **en cada deploy** (frente a rotación a demanda o mensual), aceptando
los costos explicados: posibles 401 sueltos durante el rollout y el cambio de IAM.

### Enfoque

- **Secrets Manager es obligatorio** (la opción de variable directa desaparece del ejemplo): secreto
  `sanmarino/produccion/jwt-key`. La TaskDef toma `JwtSettings__Key` ← `AWSCURRENT` y
  `JwtSettings__PreviousKey` ← `AWSPREVIOUS` (`<arn>::AWSPREVIOUS:`; Fargate lo soporta).
- **Pipeline** (`deploy-production.yml`, job del back, entre «Obtener task definition» y el render):
  1. `jq -e` verifica que la TaskDef tome las dos claves del secreto y que no queden en `environment`.
     Si no, corta **antes** de tocar el secreto o el servicio.
  2. Genera 64 bytes (`openssl rand`), los enmascara (`::add-mask::`), los guarda con
     `put-secret-value` desde un archivo `umask 077` que un `trap` borra. Secrets Manager mueve sola la
     etiqueta: nueva = `AWSCURRENT`, la de antes = `AWSPREVIOUS`.
- **API**: firma con `Key` (sin cambios en `AuthService`); valida con `Key` + `PreviousKey`
  (`IssuerSigningKeys`, decisión pura en `JwtRotacionClaveCalculos`). Los tokens previos al deploy
  siguen valiendo hasta vencer ⇒ el deploy no desloguea a nadie.
- `JwtOptions.PreviousKey` opcional; `EnsureValid` exige ≥ 32 bytes si viene. En `Production` la guarda
  también rechaza una `PreviousKey` del repo (valida tokens).

### Archivos (además de la fase 1)

| Archivo | Cambio |
|---|---|
| `Application/Options/JwtOptions.cs` | `PreviousKey` + validación de largo. |
| `Application/Calculos/JwtRotacionClaveCalculos.cs` | Nuevo: `ClavesDeValidacion(clave, anterior)`. |
| `Application/Calculos/JwtClaveProduccionCalculos.cs` | `MotivoRechazo(clave, nombre)`: mensaje con el nombre evaluado. |
| `API/Infrastructure/JwtAuthenticationConfiguration.cs` | `IssuerSigningKeys` (actual + anterior). |
| `API/Program.cs` | Guarda de producción también para `PreviousKey`. |
| `.github/workflows/deploy-production.yml` | `JWT_SECRET_ID` + pasos «Verificar…» y «Rotar clave JWT». |
| `backend/deploy/jwt-produccion.example.md` | Reescrito: setup único + funcionamiento por deploy. |
| Tests | `JwtRotacionClaveCalculosTests` + ajuste de `JwtClaveProduccionCalculosTests` (workflow y ejemplo usan el mismo secreto). |

### Costos / bordes aceptados

- Ventana del rollout: token emitido por tarea nueva que cae en una vieja ⇒ 401 ⇒ re-login.
- Dos deploys en menos de `DurationInMinutes` ⇒ los tokens de dos claves atrás dejan de valer.
- Prerrequisito de despliegue: setup de AWS hecho (secreto con 2 versiones, IAM de los dos roles,
  revisión de TaskDef). Sin él, el paso «Verificar» corta el job limpio (sin rollback de ECS).

### Casos de prueba (fase 2)

- `ClavesDeValidacion`: con anterior → [actual, anterior]; sin anterior / vacía / igual → [actual].
- `EnsureValid`: sin anterior OK; anterior de 32 bytes OK; anterior de 31 bytes → excepción.
- Motivo de rechazo nombra `JwtSettings:PreviousKey` / `JwtSettings__PreviousKey`.
- Workflow y ejemplo nombran el mismo secreto y `JwtSettings__PreviousKey`.
- Harness con el `Configure` real: token firmado con la actual y con la anterior → válido; con otra
  clave → inválido; sin anterior configurada, el de la anterior → inválido.
- Expresión `jq` del workflow contra TaskDefs de prueba (correcta, sin PreviousKey, PreviousKey sin
  AWSPREVIOUS, clave aún en environment, secreto de otro nombre).

---

## Fase 3 — Sin nada nuevo de AWS (22-sep-2026, decisión del usuario)

El usuario **no administra AWS**: no puede crear secretos, tocar IAM ni editar la TaskDef en la
consola. Pide quitar todo lo que necesite servicios o administración de AWS que hoy no se usan, y que
lo existente siga funcionando. Se conserva la rotación en cada deploy, pero hecha **solo con lo que el
pipeline ya hace**: descargar la TaskDef, modificarla y registrar una revisión nueva (mismos permisos
de `github-actions-deploy` que ya usa para cambiar la imagen).

### Se quita (fase 2)

- Secrets Manager (`sanmarino/produccion/jwt-key`), los permisos IAM nuevos y la verificación `jq` que
  exigía `secrets` en la TaskDef (con ella, el próximo deploy habría cortado).
- `JWT_SECRET_ID` del job.
- El fallo de arranque por una `PreviousKey` inválida: pasa a **ignorarse** (no valida tokens) en vez de
  tumbar la API — una anterior rara nunca debe bloquear un deploy que el usuario no puede arreglar.

### Se hace

- `backend/scripts/rotar-clave-jwt-taskdef.js` (+ test `node --test`): sobre el JSON de la TaskDef ya
  descargado, en el contenedor `backend`: la `JwtSettings__Key` que había pasa a
  `JwtSettings__PreviousKey`, y `JwtSettings__Key` recibe 64 bytes nuevos (`crypto.randomBytes`).
  Enmascara ambas (`::add-mask::`), no imprime valores. Si la TaskDef ya trae `JwtSettings__Key` en
  `secrets`, no toca nada (respeta una gestión por Secrets Manager si alguien la pusiera).
- Workflow: un paso «Rotar clave JWT en la TaskDef» entre «Obtener task definition» y «Actualizar
  imagen»; corre el test del script antes (si falla, no hay deploy).
- API: `ClavesDeValidacion(clave, anterior, esProduccion)` ignora la anterior vacía, igual, de menos
  de 32 bytes o —en producción— del repo. `Configure(options, jwt, esProduccion)`.
- Se conserva la guarda de `Key` en producción: el pipeline garantiza una clave aleatoria en cada
  revisión, así que no puede bloquear un deploy del pipeline.
- Ejemplo reescrito: no hay setup; qué hace cada deploy, dónde vive la clave, cómo rotar a demanda
  (correr el workflow a mano desde GitHub).

### Costos / bordes

- La clave vive en `environment` de la TaskDef, como hoy (visible para quien lea la TaskDef). Mejora:
  cambia en cada deploy, así que la filtrada en los snapshots de `backend/deploy/*.json` muere en el
  primer deploy.
- Deploys manuales (`make deploy-backend`, `backend/deploy/*.ps1`) no rotan.
- Mismos bordes de rollout que la fase 2 (401 sueltos en la ventana; dos deploys en < 60 min).

### Casos de prueba (fase 3)

- Script: con clave previa → Key nueva + PreviousKey = la previa; sin clave previa → solo Key; una
  PreviousKey vieja se reemplaza (nunca quedan dos); otras variables y otros contenedores intactos;
  `secrets` con Key → sin cambios; contenedor ausente → error; salida sin valores.
- `ClavesDeValidacion`: anterior corta / del repo en producción → ignorada; del repo fuera de
  producción → aceptada.
- Harness con el `Configure` real y smoke del binario en `Production`.
- Simulación del paso completo del workflow sobre una TaskDef de prueba + parseo YAML.
