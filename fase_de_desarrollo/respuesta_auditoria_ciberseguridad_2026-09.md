# Respuesta a hallazgos de auditoría de ciberseguridad — septiembre 2026

> Documento de respuesta para el analista que corrió la **validación externa de caja negra** contra
> producción. Para cada hallazgo: **qué se observó → qué es en realidad → qué va a encontrar el
> analista al verificarlo manualmente → se puede cerrar → acción pendiente**. Al final, lo único que
> una inspección del sitio en vivo va a mostrar además (la firma de plataforma viaja en el bundle JS).
>
> Alcance: sistema en ejecución (`https://zootecnico.sanmarino.com.co`), no el repositorio.
>
> Evidencia en: `Program.cs`, `Middleware/PlatformSecretMiddleware.cs`, `SwaggerPasswordMiddleware.cs`,
> `Controllers/DbStudioController.cs`, `Services/DbStudio/DbStudioAuthorization.cs`,
> `Calculos/DbStudioMigrationCalculos.cs`, `EncryptionService.cs`, `auth.interceptor.ts`,
> `environment.prod.ts`, `frontend/nginx.conf`, `backend/Dockerfile`, `deploy-production.yml`.

---

## 0) Mensaje corto para el analista (los 3 puntos que queremos dejar claros)

**a) El módulo "Base de Datos" (DB Studio).** El controlador `/api/DbStudio` sí es una consola de base
de datos embebida (explorador de esquemas, SELECT/DDL/SQL arbitrario, backup, grants). **Pero la
única capacidad expuesta a los usuarios de la aplicación es de solo lectura: la lista de migraciones
de EF Core y si cada una se ejecutó** (`{ migrationId, status: aplicada|pendiente }`, sin fechas ni
metadatos). El resto de la consola está cercado por **doble validación en el backend** (correo
concreto **Y** rol admin); cualquier otra sesión —autenticada, admin, o anónima— recibe **403 / 401**.
Sirve para que operaciones confirme que un despliegue aplicó sus migraciones.

**b) Swagger.** En **producción no existe `/swagger`**: todo el bloque está dentro de
`if (!app.Environment.IsProduction())` y el contenedor arranca con `ASPNETCORE_ENVIRONMENT=Production`.
En ambientes de desarrollo, Swagger está detrás de una **contraseña propia** (formulario dedicado, no
JWT). El `401` que se ve en `/api/swagger.json` **no es un Swagger protegido** — es el filtro de
plataforma que responde `401` a cualquier ruta `/api/*` sin la firma de origen (ver punto c).

**c) "Suscripción" de front/back.** Toda petición a `/api/*` debe traer el header `X-Secret-Up` (una
firma de plataforma cifrada AES). Sin ella, el backend responde `401` con `errorCode: platform-secret`
**antes de mirar el token o la ruta**. Es un **filtro de origen** — frena escáneres, bots y tráfico
que no venga de nuestros clientes. **No es la frontera de autenticación**: esa es el **JWT por
usuario** (60 min en producción, revocable por lista blanca de `jti`), más autorización
*deny-by-default* y alcance multiempresa *fail-closed*. Aclaración honesta: la firma de plataforma y su
llave viajan hoy **dentro del bundle del frontend** (`environment.prod.ts`), así que hay que tratarla
como fricción/*anti-scraping*, no como un secreto criptográfico.

---

## 1) `/api/swagger.json` responde HTTP 401 (no 404)

### Qué se observó
> "/api/swagger.json responde HTTP 401 (no 404) — confirma una API real que exige autenticación,
> prioridad de verificación manual."

### Qué es en realidad
- **No hay ningún documento OpenAPI servido en `/api/swagger.json`.** Esa ruta no existe.
- El `401` lo genera **`PlatformSecretMiddleware`** (`app.UsePlatformSecret()` en `Program.cs`), que
  corre **antes del ruteo y de la autenticación**. Rechaza con `401` **cualquier** petición que no
  esté en su lista de exenciones (`/swagger*`, `/health`, `*/ping`, `/api/auth/login`,
  `/api/auth/register`, `/api/auth/recover-password`, y PATs `Bearer sk_…`) si no trae un
  `X-Secret-Up` válido. `/api/swagger.json` no está exento ⇒ `401` con cuerpo
  `{"error":"Unauthorized","errorCode":"platform-secret",...}` y header `X-Auth-Failure: platform-secret`.
- La respuesta es **idéntica** para `/api/swagger.json`, `/api/Users`, `/api/loquesea`: es un filtro
  transversal, no una pista de que exista ese recurso concreto.
- **El Swagger real** se monta en `/swagger` y `/swagger/v1/swagger.json` y **solo si el entorno no es
  Production**. En producción:
  - `ASPNETCORE_ENVIRONMENT=Production` está fijado en el `Dockerfile` (`ENV`) y en el task
    definition de ECS ⇒ el bloque `UseSwagger()`/`UseSwaggerUI()`, la UI, el JSON y los endpoints
    auxiliares (`/swagger/login`, `/swagger/token`, `/swagger/download`) **no se registran**.
  - Además el ALB rutea `/api/*` al backend y **todo lo demás al frontend (nginx)**. Un `GET
    /swagger/v1/swagger.json` cae en el frontend, tiene extensión `.json`, y nginx responde **404**
    (`try_files $uri =404`, no hace fallback a `index.html`).
- En ambientes no productivos, Swagger está detrás de `SwaggerPasswordMiddleware`: sin la contraseña
  (`Swagger:Password`) no se ve ni la UI ni el `swagger.json` (devuelve el formulario de login).

### Cómo verificarlo (contra producción)
```bash
# 1) El 401 es el filtro de plataforma, no un Swagger. Mismo resultado en cualquier ruta /api/*:
curl -si https://zootecnico.sanmarino.com.co/api/swagger.json | grep -Ei '^HTTP|x-auth-failure|errorCode'
curl -si https://zootecnico.sanmarino.com.co/api/cualquier-cosa | grep -Ei '^HTTP|x-auth-failure|errorCode'
#   → ambos: HTTP/2 401 · x-auth-failure: platform-secret · "errorCode":"platform-secret"

# 2) El path canónico de Swagger no devuelve documento en prod:
curl -si https://zootecnico.sanmarino.com.co/swagger/v1/swagger.json | head -n1   # → 404
curl -si https://zootecnico.sanmarino.com.co/swagger                  | head -n1   # → 200 index.html del SPA o 404, NUNCA Swagger UI
```

### ¿Se puede cerrar?
**Sí**, con la verificación manual de arriba: (1) el `401` es uniforme para todo `/api/*` y es el
filtro `platform-secret`; (2) no hay contrato OpenAPI accesible en producción.

### Riesgo residual (bajo, aceptado)
Un `401` en lugar de `404` le confirma a un escáner que "hay algo detrás". Es una consecuencia de que
el filtro de plataforma corre antes del ruteo (no puede saber si la ruta existe). No expone la
superficie de la API ni datos. No se considera necesario cambiarlo.

---

## 2) Referencia a `/api/DbStudio` en el JS de la app

### Qué se observó
> "Referencia a un endpoint '/api/DbStudio' encontrada en el JS de la app — nombre sugiere una
> interfaz de administración de base de datos, no confirmado, requiere verificación manual prioritaria."

### Qué es en realidad
La intuición del analista es correcta: **es una consola de administración de BD**. El string aparece
en el bundle porque `frontend/src/app/features/db-studio/data/db-studio.service.ts` define
`const API = '/api/DbStudio'` y el módulo se carga *lazy* bajo `/config/db-studio`.

`DbStudioController` expone (entre otros): explorar esquemas/tablas/vistas/funciones, *preview* de
datos, `SELECT` arbitrario, **`SQL`/`DDL` arbitrario** (`CREATE`/`DROP TABLE`, columnas, índices,
FKs), `INSERT`/`UPDATE`/`DELETE`, export de tabla/esquema, **backup completo de la base**, gestión de
*grants* y `cancel`/`terminate` de procesos de PostgreSQL.

**El modelo de acceso** (endurecido en los commits `dd9832d`, `0dd1121`, `c065e68`):

| Capa | Regla |
|---|---|
| Filtro de plataforma | Todo `/api/DbStudio/*` exige `X-Secret-Up` (igual que el resto de la API). |
| Autenticación | `[Authorize]` en el controlador ⇒ JWT válido y sesión viva (lista blanca `jti`). |
| Autorización | Explícita en código (`DbStudioAuthorization`), no por atributos de policy. |
| **Acceso completo** | **Doble validación (`AND`)**: el claim de correo debe ser **exactamente** `moiesbbuga@gmail.com` **y además** ser admin (rol `admin`/`administrador`, *super-admin*, o permiso `db_studio.admin`). Falla cualquiera de los dos ⇒ se corta. |
| Todos los demás | Cualquier sesión autenticada (incluidos otros admins) recibe **403** en todos los endpoints salvo los dos de abajo. Sin JWT ⇒ **401**. |
| Superficie pública | Solo `GET /api/DbStudio/access-mode` (`{ fullAccess: bool }`) y `GET /api/DbStudio/migration-summary` (`[{ migrationId, status: "aplicada"|"pendiente" }]`). Sin fechas, sin metadatos de estructura, sin SQL. |
| Contrato Swagger | Controlador entero con `[ApiExplorerSettings(IgnoreApi = true)]`; solo `migration-summary` se revierte. (Cosmético — el control real son las guardas 403.) |
| *Kill switch* | Config `DbStudio:Enabled`. En `false`, **todo** el controlador responde `400 "DB Studio está deshabilitado"`. |

Verdad matizada para el punto 0.a: para **todos los usuarios de la aplicación**, "Base de Datos" es
efectivamente un lector de estado de migraciones. La consola completa existe en el mismo controlador
pero está cercada a **una identidad fija con segundo factor (rol)**, decidido en el servidor.

> **Actualización 7-sep-2026:** la ruta se renombró a **`/api/ConfigColores`** y en la UI el módulo
> se llama **"Configuración de colores"** (nombre-señuelo). La clase, los servicios y la config
> `DbStudio:` conservan el nombre interno. Los `curl` de abajo usan la ruta nueva. Ver
> `renombrar_db_studio_a_config_colores_plan.md`.
>
> **Corrección 8-sep-2026 — el rename estaba incompleto.** Al validarlo se encontró que el string
> **seguía en el bundle**: el DTO `PoolStatsDto` exponía `DbStudioConnections`, que serializa como
> `dbStudioConnections`, y el front lo consumía por nombre en su modelo y en su template. El checkbox
> que había dado por cerrado el rename fue un `grep` **case-sensitive**
> (`grep -rn "db-studio\|DbStudio\|db_studio"`), que no ve la forma camelCase — mientras que el
> `grep -i dbstudio main.js` del analista sí. Campo renombrado a `poolActiveConnections` en los dos
> lados del contrato, y el grep case-insensitive quedó como **gate de CI**
> (`frontend/scripts/verificar-senuelo-modulo.js`). Ver
> `gates_hallazgos_auditoria_2026-09_plan.md`.

### Cómo verificarlo
Con una **cuenta de prueba de bajo privilegio** (recomendado entregarla al analista):
```bash
T='<JWT de la cuenta de prueba>'
S='<X-Secret-Up válido — lo genera el front; se puede capturar de las DevTools>'
H="-H \"Authorization: Bearer $T\" -H \"X-Secret-Up: $S\""

curl -si $H https://zootecnico.sanmarino.com.co/api/ConfigColores/migration-summary | head -n1  # → 200 (lista de migraciones)
curl -si $H https://zootecnico.sanmarino.com.co/api/ConfigColores/schemas           | head -n1  # → 403
curl -si $H https://zootecnico.sanmarino.com.co/api/ConfigColores/backup            | head -n1  # → 403
curl -si    -H "X-Secret-Up: $S" https://zootecnico.sanmarino.com.co/api/ConfigColores/schemas | head -n1  # sin token → 401
```
El cuerpo de `migration-summary` debe contener **solo** `migrationId` y `status`. La pared de `403`
para todo lo demás **es la evidencia** de que la consola está cerrada.

### ¿Se puede cerrar?
**Sí, tras la verificación manual anterior** y aplicando al menos la primera acción de abajo. Es el
hallazgo de los tres que más merece una respuesta deliberada y documentada, porque el nombre y la
superficie asustan aunque el acceso esté cerrado.

### Acciones recomendadas (endurecimiento)
0. ✅ **Hecho (7-sep):** renombrado a `/api/ConfigColores` + "Configuración de colores" en la UI —
   el string `DbStudio` sale del bundle. Es obscurity/defensa en profundidad, no el control real.
1. **Mover el correo autorizado de constante de código a configuración** (`DbStudio:FullAccessEmails`,
   vía variable de entorno del task definition). Hoy está *hardcodeado* en
   `DbStudioMigrationCalculos.EmailConAccesoCompleto`.
2. **Evaluar `DbStudio:Enabled=false` en el task definition de producción**, y activarlo solo bajo
   control de cambios cuando una tarea de DBA lo requiera. (Contra: rompe el panel de estado de
   migraciones para quien lo use; se puede mitigar separando `migration-summary` a su propio
   controlador sin *kill switch*.)
3. **Confirmar que la cuenta con acceso completo tiene MFA** en el IdP y queda en monitoreo/alertas.
4. Documentar el riesgo residual: "compromiso simultáneo de esa cuenta **y** de un rol admin".

---

## 3) `.env`, `.aws/credentials`, `.git/config` devuelven 403 consistente

### Qué se observó
> "Varias rutas sensibles (.env, .aws/credentials, .git/config) devuelven 403 de forma consistente en
> dos métodos de chequeo distintos — probablemente un bloqueo deliberado y no una exposición real,
> pero se recomienda confirmar manualmente antes de cerrar el punto."

### Qué es en realidad
Bloqueo deliberado, en **dos capas independientes** (defensa en profundidad):

1. **AWS WAF sobre el ALB.** Hay *managed rule groups* activos. Ya está documentado que
   `AWSManagedRulesAdminProtectionRuleSet` bloquea con `403` cualquier path que contenga `admin`
   (memoria del proyecto + `fix_waf_tickets_admin_route_plan.md`). Los patrones `.env` / `.git/config`
   / `.aws/credentials` son la firma de `AWSManagedRulesCommonRuleSet` /
   `AWSManagedRulesKnownBadInputsRuleSet`. Respuesta típica: `403` con `server: awselb/2.0` y cuerpo
   HTML genérico, **antes de llegar a la aplicación**.
2. **nginx del frontend.** `frontend/nginx.conf` tiene `location ~ /\. { deny all; }` ⇒ cualquier
   archivo oculto (`.env`, `.git`, `.aws`, …) responde `403` desde el propio contenedor.

Además, **detrás de ese 403 no hay ningún archivo**: el backend es una API (su `wwwroot` solo tiene
`.well-known/security.txt` y `robots.txt`) y el frontend sirve el `dist` de Angular. Sin las dos capas
igual sería `404`.

### Cómo verificarlo
```bash
curl -si https://zootecnico.sanmarino.com.co/.env            | grep -Ei '^HTTP|^server'
curl -si https://zootecnico.sanmarino.com.co/.git/config     | grep -Ei '^HTTP|^server'
curl -si https://zootecnico.sanmarino.com.co/.aws/credentials| grep -Ei '^HTTP|^server'
#   → 403. server: awselb/2.0 (WAF) o nginx (deny dotfiles).
```
Del lado AWS: **WAF & Shield → Web ACLs (región us-east-2) → Web ACL del ALB**, revisar los *rule
groups* administrados y, en *Sampled requests* / logs, el nombre de la regla que emite el `Block`.

### ¿Se puede cerrar?
**Sí.** Es un bloqueo intencional, de bajo riesgo, y no hay archivo detrás. Basta con dejar
registrado qué *rule group* lo produce.

---

## 4) Lo que una inspección del bundle JS de producción va a mostrar

El analista casi seguro va a descargar `main.js` de producción y buscar secretos. Va a encontrar,
en `environment.prod.ts` compilado dentro del bundle:

- `platformSecret.secretUpFrontend` y `platformSecret.encryptionKey` — el par que genera el header
  `X-Secret-Up`.
- `encryptionKeys.remitenteFrontend` / `remitenteBackend` — llaves AES del cifrado del login.
- `recaptcha.siteKey` — es pública por diseño (va en el widget), no cuenta.

**Cómo responderlo:** el navegador necesita estos valores para poder hablar con el backend, así que
por diseño son visibles para cualquiera que abra las DevTools. **Por eso `X-Secret-Up` no es la
frontera de autenticación** (ver punto 0.c): es un filtro de origen contra escáneres y tráfico
automatizado. La autenticación real —JWT por usuario de 60 min, revocable; authz *deny-by-default*;
alcance multiempresa *fail-closed*— **no depende de ningún valor del bundle**. Que se conozca
`secretUpFrontend` **no** permite autenticarse, ni leer datos, ni cambiar de empresa.

### Acción (opcional, mejora de diseño — no bloquea el cierre de la auditoría)
- Rotar `secretUpFrontend` / `encryptionKey` / llaves `Encryption` si se quiere invalidar lo que ya
  esté capturado (efecto limitado: el valor nuevo también viaja en el bundle).
- **En curso (§4b):** entregar la firma de plataforma tras el login en vez de embeberla.

---

## 4b) Refuerzo en curso — firma de plataforma por sesión

Plan: `fase_de_desarrollo/llave_plataforma_por_sesion_plan.md`. Estado: Fase A implementada, pendiente
de smoke y deploy.

**Lo que ya existe hoy (B1 — revocación de sesión, `sesiones_activas`):** cada request autenticado
valida el token contra una tabla server-side. El `jti` del JWT tiene que existir como fila, no estar
revocada y no haber vencido — **sin fila, 401 fail-closed**. Cambiar la contraseña, dar de baja al
usuario o revocar un dispositivo apaga la sesión con un `UPDATE`, sin esperar a que venza el token
(propaga en < 60 s). Los JWT **no son irrevocables** en este sistema.

**El cambio:** el frontend web deja de mandar el secreto estático del bundle como `X-Secret-Up` y
manda una firma **por sesión**: `Base64(HMAC-SHA256(DerivationKey, jti))`, derivada por el backend en
el login. La `DerivationKey` vive **solo en el servidor** (variable de entorno del task definition).
Como el `jti` ya se valida contra `sesiones_activas` en cada request, la firma hereda vida de sesión,
expiración y revocación. Transición sin corte: el backend acepta la firma derivada **o** el secreto
estático legacy (que la app móvil sigue usando).

**Resultado:** tras la Fase B (quitar el estático del bundle, ~3 días después), `main.js` **no
contiene ninguna firma que sirva** — el hallazgo del §4 cierra del todo. La app móvil se trata aparte
(su secreto está en el APK, otra superficie).

---

## 5) Checklist de cierre

| # | Hallazgo | Qué encuentra el analista al verificar | ¿Cierra? | Acción antes de cerrar |
|---|---|---|---|---|
| 1 | `/api/swagger.json` → 401 | `curl` (§1): 401 uniforme para todo `/api/*` = filtro `platform-secret`; `/swagger*` sin contrato ni UI en prod | **Sí** | Ninguna. Adjuntar salidas de `curl`. |
| 2 | `/api/DbStudio` | `curl` con cuenta de prueba (§2): `migration-summary` 200 minimal, resto 403, sin token 401 | **Sí, con endurecimiento** | Mover correo a config (#1 de §2); evaluar `Enabled=false` en prod; confirmar MFA de la cuenta con acceso completo |
| 3 | `.env` / `.aws` / `.git` → 403 | `curl` (§3): 403 del WAF (`server: awselb/2.0`) y/o del nginx; sin archivo detrás | **Sí** | Registrar el *rule group* del WAF que emite el Block |
| 4 | Secretos en el bundle JS | Descarga de `main.js`: `secretUpFrontend` + `encryptionKey` visibles (§4) | **Sí, con aclaración** | Documentar que `X-Secret-Up` es filtro de origen, no auth; la auth (JWT/authz/multiempresa) no depende del bundle |

---

## 6) Gates de regresión — por qué estos hallazgos no vuelven

Plan: `gates_hallazgos_auditoria_2026-09_plan.md` (8-sep-2026). Las mitigaciones de arriba existían
pero **ninguna estaba verificada por una máquina**: se sostenían por memoria del equipo, y el §2 ya se
había filtrado una vez por eso. Ahora cada una corta el pipeline de despliegue.

| Gate | Dónde corre | Qué impide |
|---|---|---|
| `frontend/scripts/verificar-senuelo-modulo.js` | job `tests` | Que cualquier forma de `dbstudio`/`db-studio`/`db_studio` —**case-insensitive**— vuelva a `frontend/src` y por lo tanto al bundle. Tolerancia cero. |
| `backend/scripts/verificar-superficie-produccion.js` | job `tests` | (1) Que un `UseSwagger`/`UseSwaggerUI`/`Map*("/swagger…")`/`Map*("/debug…")` quede fuera de `if (!app.Environment.IsProduction())` — se resuelve por balanceo de llaves, no por proximidad. (2) Que el `ENV ASPNETCORE_ENVIRONMENT=Production` desaparezca del Dockerfile o de los task definitions. (3) Que `UsePlatformSecret()` pase a correr después de `UseAuthentication()`/`MapControllers()` — ahí el 401 dejaría de ser uniforme y el par 401/404 **enumeraría rutas** para un anónimo. (4) Que se agregue una exención al filtro de origen sin que se vea en el diff (lista congelada en el propio gate). (5) Que el controlador de la consola de BD pierda la ruta-señuelo, el `[Authorize]` o el ocultamiento de Swagger. |
| 3 `check` nuevos en «Validar nginx y política de caché del borde» | job `frontend`, **antes** del push a ECR | Que se borre `location ~ /\. { deny all; }` de `nginx.conf`. Sin ellos, borrarla no rompe ningún test: los dotfiles pasarían a caer en el `try_files … /index.html` y responderían **200 con el index**, convirtiendo en falsa la respuesta que dimos en el §3. **Medido el 8-sep-2026** contra la config real sobre `nginx:1.27-alpine`: con el bloque, los tres dan 403; sin él, los tres dan 200 `text/html`. |

Lo que estos gates **no** son: controles de seguridad. El control real sigue siendo JWT por usuario +
authz *deny-by-default* + alcance multiempresa *fail-closed* + la doble validación de la consola de
BD. Los gates cuidan que no volvamos a **regalar superficie ni pistas de reconocimiento** por un
refactor distraído.

### Para entregar al analista
- Una **cuenta de prueba de bajo privilegio** (para verificar la pared de 403 de DB Studio).
- Acceso de lectura a la **consola de AWS WAF** (o el export del Web ACL) para el hallazgo 3.
- Este documento.
