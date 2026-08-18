# Plan — B1: revocación de sesión (`jti` + `sesiones_activas` + renovación)

> **Estado:** plan (STEP 1). Nada implementado todavía.
> **Origen:** `tracker_estado.md` — bloque *«PWA — deuda conocida»*: *«**B1** revocación de sesión
> (`jti` + `sesiones_activas` + refresh) — el más urgente: una tablet perdida no se puede revocar y
> la jornada offline dura 16 h»*. Diseño esbozado en
> [`pwa_offline_first_plan.md`](pwa_offline_first_plan.md) §4.B fila B1 y decisión **D4** (§7, línea
> 299): *«Jornada 12-16 h — **con B1 (revocación) implementado**. 7 días sin revocación es una
> ventana de acceso que nadie puede cerrar»*.
>
> **Regla aplicada:** el código de HOY manda. Todo lo de §0 está verificado leyendo el árbol actual;
> donde el tracker contradice al código, gana el código y queda escrito.

---

## 0. Estado verificado del código (18-ago-2026) — leer antes de diseñar nada

### 0.1 Emisión del JWT

| Qué | Dónde | Hecho verificado |
|---|---|---|
| Emisión | `backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs:277-354` (`GenerateResponseAsync`) | Único emisor de JWT de usuario del sistema. Lo llaman `LoginAsync` (`:179`) y `RegisterAsync` (`:123`). |
| Claims | `AuthService.cs:308-341` | `NameIdentifier`, `sub`, `unique_name`, `email`, `firstName`, `surName`, `user_id` (hash del guid), `is_super_admin`, N× `role`, N× `company_id` + `company`, N× `permission`. **NO hay `jti`.** **NO hay `iat` explícito**. |
| Firma / vigencia | `AuthService.cs:344-354` | HS256 con `_jwt.Key`; `expires = UtcNow.AddMinutes(DurationInMinutes)`. |
| Duración real | `appsettings.json:9` y `appsettings.Development.json:9` | **60 min** (`JwtOptions.DurationInMinutes`; el default del POCO es 120, no se usa). |
| Opciones | `backend/src/ZooSanMarino.Application/Options/JwtOptions.cs` | `Key`, `Issuer`, `Audience`, `DurationInMinutes` + `EnsureValid()`. |

### 0.2 Validación del JWT

- `backend/src/ZooSanMarino.API/Program.cs:419-464`. Policy scheme **«Smart»** (`:424-433`) reenvía
  `Bearer sk_…` al esquema `ServiceToken` y **todo lo demás** a `AddJwtBearer` (`:434-459`).
- `TokenValidationParameters` (`:436-446`): valida issuer, audience, firma y **lifetime**, con
  `ClockSkew = TimeSpan.Zero`.
- **Ya existe un `opts.Events = new JwtBearerEvents { … }`** (`:448-458`) con `OnMessageReceived`
  (ignora el preflight `OPTIONS`). **Ese objeto es el punto de enganche natural de B1**: falta
  `OnTokenValidated`.
- Orden del pipeline: `UseRateLimiting()` `:671` → `UseAuthentication()` `:773` →
  `ActiveCompanyMiddleware` `:776` → `UseAuthorization()` `:778` → `MapControllers()` `:872`.
- `builder.Services.AddMemoryCache()` ya registrado en `Program.cs:163` (hoy solo lo usa
  `RateLimitingMiddleware`).

### 0.3 Lo que NO existe hoy

- **No hay refresh token en el backend.** `grep -r "refreshToken|RefreshToken|refresh_token"` sobre
  `backend/src` ⇒ **0 resultados**. El front tiene el campo declarado
  (`frontend/src/app/core/auth/auth.models.ts:49` y `:68`) y lo lee en
  `auth.service.ts:122` con el comentario textual *«El backend no retorna refreshToken por ahora»*:
  **siempre es `undefined`**.
- **No hay endpoint de logout.** `AuthController` expone login, register, change-password,
  change-email, session, profile, recover/reset-password, menu, email-status, ping. El logout es
  100 % cliente (`AuthService.logout()` → `TokenStorageService.clear()`).
- **No hay tabla de sesiones.** En `backend/src/ZooSanMarino.Domain/Entities/` lo único parecido es
  `ServiceToken.cs` (PAT de crones), `PasswordResetToken.cs`, `Login.cs` y `UserLogin.cs`.

### 0.4 Precedente que se copia: el PAT (`ServiceToken`)

Ya hay un token **revocable, hasheado y validado contra BD** en producción. B1 replica su forma:

| Pieza | Archivo |
|---|---|
| Entidad (`TokenHash`, `ExpiresAt`, `RevokedAt`, `LastUsedAt`) | `backend/src/ZooSanMarino.Domain/Entities/ServiceToken.cs` |
| Mapeo snake_case + índice único por hash | `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/ServiceTokenConfiguration.cs` |
| Emisión / revocación / validación | `backend/src/ZooSanMarino.Infrastructure/Services/ServiceTokenService.cs` |
| Cripto **pura** (CSPRNG, SHA-256 hex, `FixedTimeEquals`) | `backend/src/ZooSanMarino.Application/Calculos/ServiceTokenHasher.cs` |
| Handler de autenticación | `backend/src/ZooSanMarino.Infrastructure/Auth/ServiceTokenAuthHandler.cs` |
| Controller admin (**ruta sin «admin»** por el WAF) | `backend/src/ZooSanMarino.API/Controllers/ServiceTokensController.cs` |
| Migración idempotente `CREATE TABLE IF NOT EXISTS` | `backend/src/ZooSanMarino.Infrastructure/Migrations/20260703080337_AddServiceTokens.cs` |
| DbSet | `ZooSanMarinoContext.cs:148` (`public DbSet<ServiceToken> ServiceTokens => Set<ServiceToken>();`) |

### 0.5 El lado Angular

| Pieza | Archivo | Hecho |
|---|---|---|
| Sesión deslizante | `frontend/src/app/core/auth/session-timeout.service.ts` | Idle 5 min **solo con red**; heartbeat cada 90 s a `GET /api/session/heartbeat`; sin red **no cierra nada** salvo el tope de jornada; con outbox pendiente **nunca** cierra por tiempo. Corre fuera de la zona Angular. |
| Política pura | `frontend/src/app/core/auth/funciones/politica-sesion.funcion.ts` | `LIMITES_SESION_POR_DEFECTO = { inactividadMs: 5 min, jornadaOfflineMs: 16 h }`; motivos `inactividad` / `expirada` / `jornada_offline_vencida`. Tiene spec. |
| Discriminación de 401 | `frontend/src/app/core/auth/funciones/debe-cerrar-sesion-por-401.funcion.ts` | Lee `errorCode` del **cuerpo** (no la cabecera: CORS en dev). Espeja `PlatformSecretMiddleware.PlatformFailureValue = "platform-secret"`. |
| Interceptor | `frontend/src/app/core/auth/auth.interceptor.ts` | Adjunta `Authorization`, `X-Secret-Up`, `X-Active-Company[-Id]`, `X-Active-Pais[-Nombre]`. **No manda `X-Device-Id`.** |
| Storage | `frontend/src/app/core/auth/token-storage.service.ts:42` | ⚠️ Guarda `JSON.stringify(session)` **en claro**. Clave única `auth_session` ⇒ una sesión por dispositivo. `clear()` purga la caché de consultas **pero no el outbox**. |
| Guard | `frontend/src/app/core/auth/auth.guard.ts:8-15, 28-32` | Decodifica el `exp` local y, si venció, hace `auth.logout()` + redirect a `/login`. |
| Heartbeat | `auth.service.ts:255-257` | `GET {apiUrl}/session/heartbeat`. |

### 0.6 Dos afirmaciones del tracker/CLAUDE.md que el código desmiente

1. **«el storage está cifrado (AES)»** — `token-storage.service.ts:42` escribe JSON plano. El
   `EncryptionService` cifra el **transporte** (payload de login y respuestas), no el reposo. Ya está
   anotado como B9/D3 en el plan madre; se repite acá porque cambia el modelo de amenaza de B1: **el
   JWT de la tablet es legible con abrir DevTools**.
2. **«la jornada offline dura 16 h»** — cierto para `SessionTimeoutService`, **falso extremo a
   extremo**. `authGuard` está en el padre `daily-log` (`app.config.ts:131-133`) y en ~25 rutas más;
   apenas el JWT pasa sus **60 min**, la primera navegación a una pantalla protegida ejecuta
   `auth.logout()` y manda a `/login`, que sin red es un callejón sin salida (login exige backend y,
   en prod, reCAPTCHA contra Google). El propio comentario de la ruta `/diagnostico`
   (`app.config.ts:98-101`) nombra el escenario: *«sesión vencida sin red para renovarla»*.
   **Hoy la jornada de 16 h solo se sostiene si el operario no navega después del minuto 60.**
   Esto no es un detalle de B1: es la mitad del problema que B1 tiene que cerrar.

### 0.7 Restricciones de entorno que condicionan el diseño

- **`X-Device-Id`**: `RateLimitingCalculos.DeviceIdHeader` (`:30`) lo declara y
  `RateLimitingMiddleware.cs:71` lo lee, pero **ningún cliente lo manda**. El `deviceId` existe solo
  dentro del cuerpo del outbox (`outbox.service.ts:189`, `localStorage['italgranja.deviceId']`,
  persiste entre sesiones).
- **Gate de CI del front**: `frontend/scripts/verificar-lista-cacheable.js` **corta el build** si un
  endpoint pedido por la app no está en `ENDPOINTS_OPERATIVOS` ni en `EXCLUIDOS` de
  `frontend/src/app/shared/offline/funciones/decidir-cacheable.funcion.ts`. `auth`, `session`,
  `users` y `roles` **ya están en EXCLUIDOS** ⇒ si los endpoints nuevos cuelgan de esos prefijos,
  no hay que tocar la lista. Un prefijo nuevo (`/api/sesiones`) **rompería el CI**.
- **WAF**: `AdminProtection` devuelve 403 a cualquier path que contenga `admin`
  (`ServiceTokensController.cs:13`, memoria `waf-bloquea-rutas-admin`). Las rutas nuevas no llevan
  «admin».
- **ECS**: `Database__RunMigrations=true` ⇒ la migración se aplica sola al arrancar; si falla, la
  tarea muere con SIGSEGV y ECS revierte en silencio.

---

## 1. Enfoque arquitectónico y trade-offs

### 1.1 La pregunta que hay que contestar primero

«Revocar una sesión» son **dos** problemas distintos y solo uno tiene solución de servidor:

| Escenario | ¿Lo resuelve el servidor? |
|---|---|
| Tablet perdida que **en algún momento ve la red** (la encuentra alguien, la prenden en zona con señal, el ladrón navega) | **Sí, completamente.** Es el 100 % del valor de B1. |
| Tablet perdida que **nunca más se conecta** | **No, y ningún diseño de servidor puede.** Ver §6.2. |

Todo el plan se ordena alrededor de eso: el servidor deja de confiar en el token y pasa a confiar en
una **fila de BD**; el cliente aporta un tope que acota, pero no garantiza, la ventana offline.

### 1.2 Las tres formas de revocar, comparadas

| Opción | Cómo funciona | A favor | En contra | Veredicto |
|---|---|---|---|---|
| **(A) Lista negra de `jti`** | Tabla de `jti` revocados; el token vale salvo que esté en la lista | Barata; sin escritura en el login | **Fail-open**: si la consulta falla o la fila se borró por limpieza, el token revocado **pasa**. No permite listar «qué dispositivos tienen sesión» — que es literalmente lo que el jefe de operación va a preguntar cuando pierdan una tablet | ❌ |
| **(B) `users.token_version`** (contador) | Claim `tv` en el token; se compara contra la columna; `+1` revoca todo | Lo más barato de todo (una `int`, sin tabla, sin `jti`) | **Granularidad cero**: revocar la tablet perdida desloguea también la tablet buena del mismo operario y su sesión de oficina. No hay `last_seen_at`, ni device, ni auditoría | ❌ como solución, ✅ como **complemento** (§1.4) |
| **(C) `sesiones_activas` = lista blanca por `jti`** | El login **inserta** la fila; cada request exige que la fila exista, no esté revocada y no esté vencida | **Fail-closed** (sin fila ⇒ 401), granular por dispositivo, listable, auditable (`last_seen_at`, `ip`, `user_agent`), y sirve de base para el multi-slot que el tracker pide aparte | Una escritura en el login y una lectura por request (mitigable, §1.5); requiere limpieza periódica de filas vencidas | ✅ **elegida** |

**Se elige (C)**, que además es exactamente lo que dice el sketch del plan madre
(`sesiones_activas(user_id, jti, device_id, last_seen_at, revoked_at)`), con **(B) agregada como
interruptor de pánico** de un solo golpe.

### 1.3 Qué pasa con la vigencia del access token — la decisión de fondo

Hoy conviven dos números que se pelean: JWT de **60 min** y jornada offline de **16 h**. Con
revocación del lado del servidor, **la vigencia del token deja de ser el mecanismo de revocación** y
puede alinearse con la jornada. Dos caminos:

| | (i) JWT 60 min + refresh token rotativo | (ii) JWT = jornada (16 h) + revocación server-side |
|---|---|---|
| Revocación efectiva **con red** | Inmediata (se revoca la sesión; el refresh falla) | Inmediata (se revoca la sesión; **todo** request falla) |
| Ventana **sin red** | 60 min y el `authGuard` expulsa (§0.6.2). Habría que **debilitar el guard** para tolerar un token vencido mientras no hay red — o sea, mover la decisión a un lugar bypasseable igual | 16 h, que es la ventana que **D4 ya aceptó por escrito** |
| Complejidad | Alta: endpoint de refresh, rotación, detección de reuso, carrera de N pestañas refrescando a la vez, y **un secreto más en el storage plano** | Baja: un claim + una tabla + un hook |
| Riesgo si el token se filtra | Menor por tiempo… pero el refresh token filtrado es **peor** (renueva solo) | El token vale 16 h **y se apaga con un `UPDATE`** |
| Superficie de cambio | Backend + interceptor + cola de refresh + guard | Backend + config + un mensaje nuevo en el front |

**Se elige (ii)**: `Jwt:DurationInMinutes` **60 → 960 (16 h)**, alineado con
`LIMITES_SESION_POR_DEFECTO.jornadaOfflineMs`, **y solo porque B1 introduce la revocación en el mismo
cambio**. Sin `sesiones_activas`, subir la vigencia sería empeorar la seguridad; con ella, es lo que
convierte la vigencia larga en algo apagable.

> **Esto no relaja la sesión online.** Con red siguen mandando los 5 min de inactividad de
> `politica-sesion.funcion.ts`, que no se tocan. El token de 16 h solo importa cuando no hay red —
> que es el único caso en que hoy la app se rompe.

> **El «refresh token» del título de B1 queda EXPLÍCITAMENTE fuera** (§6.1). No aporta nada al
> escenario offline (renovar exige red) y agrega un secreto de larga vida a un storage sin cifrar.
> Si más adelante se quiere bajar la vigencia del access token, el refresh se construye **sobre**
> `sesiones_activas` (la fila ya es el registro de la sesión) sin rehacer nada.

### 1.4 Dónde se verifica: `JwtBearerEvents.OnTokenValidated`

Se engancha en el objeto `opts.Events` **que ya existe** en `Program.cs:448`, no en un middleware
nuevo. Razones:

- Corre **dentro** de `UseAuthentication()` (`:773`), o sea **antes** de `ActiveCompanyMiddleware`
  (`:776`) y de `UseAuthorization()` (`:778`): un token revocado nunca llega a resolver empresa
  activa ni a evaluar permisos.
- Cubre **todos** los endpoints de una sola vez, incluidos los que aún no existen. Un middleware con
  lista de rutas se desactualiza (es la misma deuda que el gate de la lista cacheable vino a tapar).
- **No toca el esquema `ServiceToken`**: los PAT tienen su propia revocación
  (`ServiceTokenService.ValidateAsync`) y siguen igual, byte a byte.
- ⚠️ **Coordinación**: hoy hay otra sesión de Claude Code editando `HttpCurrentUser.cs`,
  `ActiveCompanyMiddleware.cs`, `CompanyResolver.cs` y `EmpresaActivaCalculos.cs`. Este plan **no
  toca ninguno de esos cuatro**. `Program.cs` sí se toca (una propiedad en el `Events` ya existente y
  un `AddScoped`): coordinar antes de editar.

### 1.5 El costo por request, y cómo se paga

Verificar contra BD en cada request es una consulta más por request. Mitigación en dos capas:

1. **`IMemoryCache`** (ya registrado, `Program.cs:163`), clave `sesion:{jti}`:
   - **estado válido** ⇒ TTL corto (**60 s**). Peor caso: la revocación tarda ≤60 s en surtir efecto
     en esa tarea ECS.
   - **estado revocado/inexistente** ⇒ se cachea hasta el `exp` del token. Una vez que sabemos que
     un token está muerto, no hay razón para volver a preguntar: **no puede resucitar** (la
     reactivación de una sesión revocada no es una operación del sistema — se hace login de nuevo,
     que emite otro `jti`).
2. **`last_seen_at` NO se escribe en el camino caliente.** Se actualiza solo desde
   `GET /api/session/heartbeat` (el front ya lo llama cada 90 s mientras el usuario está activo) y
   con *throttle* de 5 min por `jti` usando la misma caché. Un `UPDATE` por request sería peor que
   el `SELECT` que estamos evitando.

**Consecuencia honesta a escribir en la UI:** *«la revocación surte efecto en menos de un minuto
desde que el dispositivo toque la red»*. No es instantánea y no hay que prometerlo. El caché es por
tarea ECS; con varias tareas la cota es la misma (60 s), no peor.

### 1.6 Fail-closed, con una excepción deliberada

- Token **sin claim `jti`** ⇒ **se acepta** durante una ventana de gracia igual a la vigencia del
  token anterior (60 min) y **después se rechaza**. Es el único camino de despliegue sin desloguear a
  todo el mundo en el instante del deploy (§4.5). La ventana se cierra borrando una constante, en un
  commit posterior y explícito.
- `jti` presente pero **sin fila** ⇒ 401 (`sesion_revocada`). Sin fila no hay sesión: es el corazón
  del fail-closed.
- Fila **`revoked_at IS NOT NULL`** ⇒ 401.
- Fila **`expires_at <= now()`** ⇒ 401 (redundante con la validación de `exp`, pero cierra el caso
  del token re-firmado si alguna vez se filtrara la llave — B8).
- **La BD no responde** ⇒ ⛔ **se acepta el token** y se loguea `Error`. Es la excepción: convertir
  una caída de RDS en un deslogueo masivo de todas las tablets en campo —con sus colas pendientes—
  es peor que el riesgo que evita. Queda documentado en el código y en §6.3.

---

## 2. Archivos a crear / modificar (rutas verificadas)

### 2.1 Backend — crear

| Archivo | Contenido |
|---|---|
| `backend/src/ZooSanMarino.Domain/Entities/SesionActiva.cs` | Entidad: `Id (long)`, `Jti (Guid)`, `UserId (Guid)`, `DeviceId (string?, 100)`, `IpAddress (string?, 64)`, `UserAgent (string?, 300)`, `CreatedAt`, `ExpiresAt`, `LastSeenAt?`, `RevokedAt?`, `RevokedByUserId (Guid?)`, `RevokedReason (string?, 200)`. Sin dependencias externas (regla de la capa Domain). |
| `backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/SesionActivaConfiguration.cs` | `ToTable("sesiones_activas","public")`; `UseIdentityAlwaysColumn`; **índice único** `ux_sesiones_activas_jti`; `ix_sesiones_activas_user_id`; índice parcial `ix_sesiones_activas_vivas` (`WHERE revoked_at IS NULL`). Columnas por `EFCore.NamingConventions` (snake_case), **no a mano** — mismo criterio que `ServiceTokenConfiguration.cs:9`. |
| `backend/src/ZooSanMarino.Application/Calculos/RevocacionSesionCalculos.cs` | **Lógica pura, sin EF.** Es el corazón testeable: `EstadoSesion` (enum) + `Evaluar(...)`, `EsSesionValida(...)`, `DebeActualizarUltimaVista(...)`, `MotivoParaCliente(...)`. Ver §5.1. |
| `backend/src/ZooSanMarino.Application/Interfaces/ISesionActivaService.cs` | `RegistrarAsync`, `EvaluarAsync`, `TocarAsync`, `RevocarAsync(long id, …)`, `RevocarTodasDelUsuarioAsync(Guid userId, …)`, `ListarDeUsuarioAsync`, `ListarMiasAsync`, `LimpiarVencidasAsync`. |
| `backend/src/ZooSanMarino.Application/DTOs/SesionActivaDto.cs` | `SesionActivaDto` (**sin** `jti` completo: solo los últimos 8 chars como etiqueta) + `RevocarSesionRequest(string? Motivo)`. |
| `backend/src/ZooSanMarino.Infrastructure/Services/SesionActivaService.cs` | Persistencia + `IMemoryCache`. Orquestador delgado: delega toda decisión en `RevocacionSesionCalculos`. Gemelo de `ServiceTokenService.cs`. |
| `backend/src/ZooSanMarino.Infrastructure/Migrations/<ts>_AddSesionesActivas.cs` | §3. |

### 2.2 Backend — modificar

| Archivo | Cambio | Riesgo |
|---|---|---|
| `AuthService.cs:308-322` | Agregar `new Claim(JwtRegisteredClaimNames.Jti, jti.ToString())` (+ `Iat`). El `jti` se genera **antes** del token para poder persistirlo. | Bajo. Es aditivo: ningún consumidor lee la lista de claims por posición. |
| `AuthService.cs:277-354` | `GenerateResponseAsync` registra la sesión (`ISesionActivaService.RegistrarAsync`) en la **misma transacción lógica** que el login. Se inyecta el service nuevo por constructor (`:31-43`). | Medio: `GenerateResponseAsync` también lo llama `RegisterAsync` (`:123`) — el alta de usuario devuelve un token, así que también crea sesión. Correcto y deseado. |
| `AuthService.cs:182-200` (`ChangePasswordAsync`) y `:729+` (`AdminResetPasswordAsync`) | Revocar **todas** las sesiones del usuario tras cambiar la contraseña. | Bajo, y es una corrección de seguridad por sí sola: hoy cambiar la contraseña **no invalida nada**. |
| `backend/src/ZooSanMarino.Infrastructure/Services/UserService.cs:385` (`IsActive = false`) y `DeleteAsync` | Revocar todas las sesiones al desactivar/eliminar un usuario. | Bajo. Hoy un usuario desactivado **sigue operando hasta que su token vence**. |
| `Program.cs:448-458` | Agregar `OnTokenValidated` al `JwtBearerEvents` **existente**. Resolver `ISesionActivaService` desde `ctx.HttpContext.RequestServices` (el handler es singleton, el service es scoped). Ante fallo: `ctx.Fail(...)` + `errorCode = "sesion-revocada"` + cabecera `X-Auth-Failure` (mismo contrato que `PlatformSecretMiddleware.cs:154-160`). | **Medio-alto** — es el punto por el que pasa todo request. Por eso la lógica vive en `Calculos` con tests y el hook queda de ~15 líneas. |
| `Program.cs` (zona DI, junto a `:401`) | `builder.Services.AddScoped<ISesionActivaService, SesionActivaService>();` | Nulo. |
| `ZooSanMarinoContext.cs` (junto a `:148`) | `public DbSet<SesionActiva> SesionesActivas => Set<SesionActiva>();` | Nulo. |
| `appsettings.json:9` + `appsettings.Development.json:9` | `Jwt.DurationInMinutes` **60 → 960**. Documentar el porqué en el propio JSON no se puede (no admite comentarios): va en el plan y en el doc-comment de `RevocacionSesionCalculos`. | **Alto si se despliega sin el resto.** Es el cambio que exige que B1 entre **completo o nada** (§6.4). |
| `backend/src/ZooSanMarino.API/Controllers/SessionController.cs` | `GET /api/session/heartbeat` pasa a tocar `last_seen_at` (throttled) y sigue devolviendo `{ ok, serverTimeUtc }` **byte a byte igual**. Nuevos: `GET /api/session/mias` (mis sesiones), `DELETE /api/session/mias/{id}` (cerrar la mía), `GET /api/session/de-usuario/{userId:guid}` y `DELETE /api/session/{id:long}` (administración). **Todo cuelga de `/api/session`**, que ya está en `EXCLUIDOS` de la lista cacheable ⇒ no se toca el gate de CI, y no contiene «admin» ⇒ no lo bloquea el WAF. | Bajo. |

**No se crea un controller nuevo.** Un `/api/sesiones` obligaría a tocar
`decidir-cacheable.funcion.ts` y arriesgaría el corte del CI para no ganar nada.

### 2.3 Backend — autorización de la revocación

- **Mis propias sesiones**: cualquier usuario autenticado, solo sobre filas con su `user_id`
  (comparado contra `ICurrentUser.UserGuid`, nunca contra un id del body — mismo criterio que
  `ServiceTokensController.cs:41-43`).
- **Sesiones de terceros**: gate = `SuperAdminLookup.EsSuperAdminAsync(...)` (dato
  `users.is_super_admin`, V23) **OR** permiso `usuarios.revocar_sesion`. La decisión es pura y va en
  `RevocacionSesionCalculos.PuedeRevocarSesionDeOtro(esSuperAdmin, permisos)` con sus tests. **No**
  se usa `[Authorize(Roles="Admin")]` a secas: `ServiceTokensController.cs:19` ya dejó escrito el
  `TODO` de que ese atajo es deuda; no se replica.

### 2.4 Frontend — crear

| Archivo | Contenido |
|---|---|
| `frontend/src/app/core/auth/funciones/device-id.funcion.ts` | Función que lee/crea `localStorage['italgranja.deviceId']`. **Extraída** del privado `OutboxService.deviceId()` (`outbox.service.ts:189-201`) para que outbox e interceptor usen **la misma** — centralizar helper duplicado al pasar (regla de clean code del repo). El outbox pasa a delegar; misma clave, mismo valor, cero cambio de comportamiento. |
| `frontend/src/app/core/auth/funciones/device-id.funcion.spec.ts` | Specs: crea y persiste; devuelve el mismo en la segunda llamada; storage bloqueado ⇒ `'desconocido'`. |
| `frontend/src/app/features/config/user-management/components/sesiones-usuario/sesiones-usuario.component.{ts,html,scss}` | Modal «Sesiones activas» de un usuario: lista (dispositivo, IP, user-agent resumido, inicio, última vez visto, estado) + botón *Revocar* con `ConfirmDialogService.ask()` y `ToastService`. **`changeDetection: ChangeDetectionStrategy.Eager` explícito** (tiene `subscribe` y estado mutable). Primitivas obligatorias del design system: nada de `confirm()`/`alert()`. |
| `frontend/src/app/core/services/session/session-admin.service.ts` | Cliente HTTP de los endpoints de §2.2. |

### 2.5 Frontend — modificar

| Archivo | Cambio |
|---|---|
| `auth.interceptor.ts:43-71` | Agregar `headers['X-Device-Id'] = obtenerDeviceId();`. ⚠️ **Efecto lateral real y querido**: `RateLimitingCalculos.IdentidadCliente` pasa a contar `/api/sync/*` **por dispositivo** en vez de por IP — que es lo que ese código dice que quiere (`RateLimitingCalculos.cs:64-73`) y hoy no puede hacer porque nadie manda la cabecera. **Ninguna otra ruta cambia** (`AlcanceDeRuta` solo usa el device en `Sync`). Va documentado en el commit. |
| `funciones/debe-cerrar-sesion-por-401.funcion.ts` | Reconocer el `errorCode` nuevo. `sesion-revocada` ⇒ **sí** cierra sesión (es el caso de autenticación por excelencia); `platform-secret` sigue **sin** cerrarla. Se agrega la constante y sus casos al `.spec` existente. |
| `funciones/politica-sesion.funcion.ts` | Motivo nuevo `'revocada'` + su mensaje: *«Un administrador cerró esta sesión. Iniciá sesión de nuevo.»*. Los mensajes existentes se conservan **byte a byte** (el archivo lo exige en su doc, `:99-101`). |
| `session-timeout.service.ts:164-185` | Al recibir 401 con `errorCode: 'sesion-revocada'`, terminar con motivo `'revocada'`. **La regla de oro no cambia: con `operacionesPendientes > 0` NO se purga** — la cola vive en IndexedDB y `TokenStorageService.clear()` no la toca (verificado: `offline-db.ts:150` limpia solo `STORE_CONSULTAS`). |
| `auth.guard.ts` | **Sin cambios de lógica.** Con el token de 16 h el guard deja de expulsar al minuto 60 sin tocarlo: el bug de §0.6.2 se cierra por la vigencia, no debilitando el guard. Se agrega solo un comentario que explique la relación. |
| `features/config/user-management/user-management.component.{ts,html}` | Acción «Sesiones activas» por fila (junto a las de restablecer contraseña / granjas) que abre el modal nuevo. |
| `frontend/src/app/features/profile/profile.component.*` | Sección «Mis dispositivos»: mis sesiones + «cerrar sesión en ese dispositivo». Es lo que hace que un operario que perdió la tablet pueda actuar **sin esperar a un administrador**. |

---

## 3. Cambios de BD / migración

**Una sola migración**, `<timestamp>_AddSesionesActivas`, **DDL puro e idempotente**, calcada de
`20260703080337_AddServiceTokens.cs` (que ya pasó por prod):

```sql
CREATE TABLE IF NOT EXISTS public.sesiones_activas (
    id                bigint GENERATED ALWAYS AS IDENTITY,
    jti               uuid                     NOT NULL,
    user_id           uuid                     NOT NULL,
    device_id         character varying(100)   NULL,
    ip_address        character varying(64)    NULL,
    user_agent        character varying(300)   NULL,
    created_at        timestamp with time zone NOT NULL,
    expires_at        timestamp with time zone NOT NULL,
    last_seen_at      timestamp with time zone NULL,
    revoked_at        timestamp with time zone NULL,
    revoked_by_user_id uuid                    NULL,
    revoked_reason    character varying(200)   NULL,
    CONSTRAINT pk_sesiones_activas PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_sesiones_activas_jti
    ON public.sesiones_activas (jti);

CREATE INDEX IF NOT EXISTS ix_sesiones_activas_user_id
    ON public.sesiones_activas (user_id);

-- El listado de la UI y la limpieza solo miran sesiones vivas.
CREATE INDEX IF NOT EXISTS ix_sesiones_activas_vivas
    ON public.sesiones_activas (user_id, expires_at)
    WHERE revoked_at IS NULL;
```

`Down()`: `DROP TABLE IF EXISTS public.sesiones_activas;`

**Reglas respetadas:**
- `IF NOT EXISTS` en tabla e índices (la BD de desarrollo la tocan varias sesiones a la vez).
- **Sin FK a `users`**: `ServiceToken.UserId` tampoco la tiene, y una FK con `ON DELETE` mal elegido
  convertiría el borrado de un usuario en un error de runtime en el arranque de ECS. La integridad la
  garantiza el service (el `user_id` sale del token ya validado).
- **Sin seeds ni DML** ⇒ no hay riesgo de que corra contra datos que no existen.
- **Sin triggers ni espejos** ⇒ no aplica la regla del histórico unificado.
- **No toca ninguna función SQL de cálculo** ⇒ **no** dispara el gate multipaís de paridad de saldos.
- Retención: filas con `expires_at < now() - 30 días` se borran con `LimpiarVencidasAsync`, invocada
  de forma perezosa (una vez por hora como mucho, desde el heartbeat). **Sin `HostedService`**: no hay
  ninguno en el proyecto y no se introduce un patrón nuevo por esto.

**Ordenamiento:** el timestamp debe quedar **después** de `20260818042406_SuperAdminPorDato` (última
migración del árbol). No hay dependencia de datos con ninguna anterior.

---

## 4. Reglas de negocio

### 4.1 Qué crea una sesión
Todo JWT de usuario emitido por `GenerateResponseAsync` — o sea `POST /api/Auth/login` y
`POST /api/Auth/register` — inserta **una** fila con su `jti`, `expires_at` = `exp` del token,
`device_id` de `X-Device-Id` (si vino), IP y user-agent. Los **PAT (`sk_…`) quedan fuera**: tienen su
propia revocación y su propio handler.

### 4.2 Qué invalida una sesión (efecto inmediato con red, ≤60 s por la caché)
1. Revocación explícita (por el propio usuario o por un administrador).
2. **Cambio de contraseña** — propio (`ChangePasswordAsync`) o por administrador
   (`AdminResetPasswordAsync`): revoca **todas** las del usuario. *Hoy no revoca nada.*
3. **Desactivación o borrado** del usuario (`UserService.cs:385`, `DeleteAsync`): revoca todas.
4. Vencimiento (`expires_at`), que es el mismo instante que el `exp` del JWT.

### 4.3 Quién puede revocar
| Actor | Alcance |
|---|---|
| Cualquier usuario autenticado | **Sus propias** sesiones (incluida la actual, que actúa como «logout de verdad»). |
| Super admin (`users.is_super_admin`) **o** permiso `usuarios.revocar_sesion` | Sesiones de **cualquier** usuario. |
| Nadie | «Des-revocar». No existe. Se vuelve a entrar y se emite otro `jti`. |

Multi-tenant: la decisión **no mira el nombre de la empresa**. Es super-admin (dato) o permiso
(dato). No hace falta flag nuevo en `companies` porque **la revocación no es una feature por
empresa**: es infraestructura de autenticación y aplica igual en los 4 países.

### 4.4 Qué pasa en el dispositivo al reconectar con la sesión revocada
1. Primer request con red ⇒ **401** con `errorCode: 'sesion-revocada'`.
2. `debeCerrarSesionPor401` lo distingue de `platform-secret` ⇒ `SessionTimeoutService` cierra con
   motivo `'revocada'` y toast propio.
3. `TokenStorageService.clear()` purga la **caché de consultas** (`purgarTodo` ⇒ solo
   `STORE_CONSULTAS`, `offline-db.ts:150`). **El outbox NO se borra.**
4. **Las capturas pendientes quedan en el dispositivo.** No se pierden, pero **no se pueden subir
   hasta que alguien inicie sesión**. Y como la partición es `{userId}|{companyId}|{paisId}`
   (`clave-particion.funcion.ts`, fail-closed), **si entra otro usuario, esas capturas no se drenan**:
   siguen ahí, invisibles, hasta que vuelva el usuario original. → **Regla de operación obligatoria:
   antes de revocar la sesión de una tablet que todavía se puede recuperar, mirá si tiene pendientes**
   (la pantalla `/diagnostico` los muestra). Se anota en el texto del diálogo de confirmación.

### 4.5 Despliegue: ventana de gracia (compatibilidad hacia atrás)
Al desplegar, **todos los tokens vivos son de antes** y no tienen `jti`. Sin ventana de gracia, el
deploy desloguearía a todo el mundo en el instante del arranque — incluidas tablets en campo con
capturas sin subir. Por eso:

- `RevocacionSesionCalculos.Evaluar(...)` devuelve `Legado` cuando **no hay `jti`** ⇒ **se acepta**.
- Ese estado se loguea (`Information`, con conteo) para poder verificar que se apaga solo.
- Como el token viejo dura 60 min, **a la hora del deploy ya no queda ninguno**. En un commit
  posterior y explícito, `Legado` pasa a rechazar. **No** se deja «para cuando haya tiempo»: entra en
  el checklist del tracker con su propia casilla.

---

## 5. Casos de prueba

### 5.1 xUnit — `backend/tests/ZooSanMarino.Application.Tests/RevocacionSesionCalculosTests.cs`
**Obligatorios (gate de CI).** La lógica se diseña *para* ser testeable sin EF: `Evaluar` recibe
primitivos, no entidades.

| # | Caso | Esperado |
|---|---|---|
| 1 | `jti = null` (token legado) | `EstadoSesion.Legado` ⇒ `EsSesionValida == true` |
| 2 | `jti` presente, sin fila | `NoRegistrada` ⇒ `false` (**fail-closed**) |
| 3 | Fila viva, `revoked_at = null`, `expires_at` futuro | `Valida` ⇒ `true` |
| 4 | Fila con `revoked_at` en el pasado | `Revocada` ⇒ `false` |
| 5 | Fila con `expires_at <= ahora` | `Vencida` ⇒ `false` |
| 6 | `expires_at == ahora` exacto (borde) | `Vencida` (`<=`, coherente con `ClockSkew = Zero`) |
| 7 | Revocada **y** vencida a la vez | `Revocada` gana (precedencia estable para el mensaje) |
| 8 | `MotivoParaCliente` por estado | `Revocada`/`NoRegistrada` ⇒ `"sesion-revocada"`; `Vencida` ⇒ `"token-expirado"`; nunca `null` en los inválidos |
| 9 | `DebeActualizarUltimaVista(lastSeen = null)` | `true` (primera vez siempre marca) |
| 10 | `DebeActualizarUltimaVista(lastSeen = hace 1 min, umbral 5 min)` | `false` (no escribir en el camino caliente) |
| 11 | `DebeActualizarUltimaVista(lastSeen = hace 6 min, umbral 5 min)` | `true` |
| 12 | `[Theory]` `PuedeRevocarSesionDeOtro`: super admin ⇒ `true`; permiso `usuarios.revocar_sesion` ⇒ `true`; ninguno ⇒ `false`; lista de permisos vacía/null ⇒ `false` | fail-closed |
| 13 | `PuedeRevocarSesionPropia(userIdSesion == userIdActual)` | `true`; distinto ⇒ `false`; `null` ⇒ `false` |
| 14 | `TtlCache(estado)` | `Valida` ⇒ 60 s; estados muertos ⇒ hasta `exp`; nunca negativo si `exp` ya pasó |

**Equivalencia con el comportamiento previo (exigencia del repo):** un caso explícito que fija que
con `jti = null` **nada cambia** respecto de hoy — es la prueba de que la ventana de gracia hace lo
que dice.

### 5.2 Tests del front (Karma) — `.spec.ts` junto a cada función
- `debe-cerrar-sesion-por-401.funcion.spec.ts` (**existente**, se le agregan casos): `sesion-revocada`
  ⇒ `true`; `platform-secret` ⇒ `false` (**regresión**: este es el caso que no se puede romper);
  401 sin token ⇒ `false`; cuerpo string vs objeto vs ausente.
- `politica-sesion.funcion.spec.ts` (**existente**): los mensajes viejos siguen **byte a byte**;
  `'revocada'` tiene el suyo.
- `device-id.funcion.spec.ts` (nuevo): §2.4.

### 5.3 Smoke manual (con el backend local **apagado al terminar**, §CLAUDE.md)
> ⚠️ Hay otra sesión trabajando el repo: si hay un backend ajeno vivo en `:5002`, **no matarlo** —
> compilar con `dotnet build --artifacts-path <dir>` y correr con `PORT=5501` + `--contentRoot`.

1. **Login normal** ⇒ el JWT trae `jti` (decodificar en jwt.io) y aparece **una** fila en
   `sesiones_activas` con su `device_id`.
2. **Heartbeat** ⇒ 200 y `last_seen_at` se mueve; al segundo heartbeat dentro de los 5 min
   **no** se vuelve a escribir (throttle).
3. **Revocar desde otro navegador** (super admin) ⇒ el primero recibe 401 con
   `errorCode: "sesion-revocada"` **en menos de 60 s**, ve el toast nuevo y cae en `/login`.
4. **Revocar la propia** desde «Mis dispositivos» ⇒ la otra pestaña del mismo usuario cae; la actual
   sigue (o cae, si se revocó la actual: ambos caminos se prueban).
5. **Cambio de contraseña** ⇒ **todas** las sesiones del usuario mueren.
6. **Usuario desactivado** (`IsActive = false`) ⇒ sus sesiones mueren sin esperar al `exp`.
7. **Token legado**: mintear un JWT con la llave y **sin** `jti` ⇒ **pasa** (ventana de gracia) y
   queda el log `Information`.
8. **PAT (`sk_…`) intacto**: `GET /api/tickets/...` con service token ⇒ **200**, sin tocar
   `sesiones_activas` (el esquema `ServiceToken` no pasa por `OnTokenValidated`).
9. **Offline con capturas pendientes** (DevTools → Offline): capturar 2 seguimientos, revocar la
   sesión en el servidor, volver a poner red ⇒ 401, cierre con motivo `'revocada'`, y en
   `/diagnostico` **las 2 capturas siguen ahí** (no se perdieron).
10. **Jornada larga**: con el token de 16 h, navegar a `/daily-log/seguimiento` pasados 60+ min sin
    red ⇒ **ya no expulsa** (verifica que §0.6.2 quedó cerrado).
11. **Rate limit de sync**: con `X-Device-Id` puesto, dos «tablets» (dos device-id) detrás de la
    misma IP drenan cola **sin bloquearse entre sí** (`ClavesAVerificar` con alcance `Sync`).

### 5.4 Validación de build
- `cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test`.
- `cd frontend && yarn build` (0 errores; único warning aceptado: el de *bundle budget*) +
  `yarn test` para los `.spec` tocados.
- `node frontend/scripts/verificar-lista-cacheable.js` **debe seguir en 0 sin decisión** (los
  endpoints nuevos cuelgan de `session`, ya excluido).

---

## 6. Riesgos y qué NO hace este plan

### 6.1 Lo que queda deliberadamente afuera
- **Refresh token rotativo.** El título de B1 lo nombra; este plan lo descarta con argumento (§1.3) y
  deja el terreno preparado: `sesiones_activas` **es** el registro que un refresh necesitaría. El
  campo `refreshToken` del front sigue muerto y se deja como está (borrarlo es ruido en un cambio ya
  sensible).
- **Sesiones multi-slot** (varios usuarios por tablet). Es otro pendiente del tracker; depende del
  storage (`auth_session` es clave única), no de la revocación. `sesiones_activas` no lo bloquea ni lo
  resuelve.
- **Cifrar el storage local** (B9/D3) y **rotar las llaves** (B8). B1 **no** hace que el JWT de la
  tablet sea ilegible: sigue en JSON plano.
- **Cerrar la ventana de gracia** de tokens sin `jti`: commit posterior, con su casilla propia.
- **Endurecer `authGuard`.** No se toca: el problema de los 60 min lo cierra la vigencia, no el guard.

### 6.2 El límite honesto frente a la tablet que nunca se reconecta

> **Un dispositivo que no vuelve a ver la red no se puede revocar. Ni con este plan ni con ninguno.**

Lo que sí se puede decir con precisión:

| Pregunta | Respuesta honesta |
|---|---|
| ¿Puede seguir **leyendo** lo que ya tiene cacheado? | **Sí.** El dato ya está en su IndexedDB. Solo lo borra el TTL de 16 h de la caché, y ese TTL lo evalúa código que corre **en el dispositivo del atacante**. |
| ¿Puede **escribir** algo en el sistema? | **No.** Escribir exige llegar al servidor, y en cuanto llega, el 401 lo frena. La captura offline **encola**, no escribe. |
| ¿Puede **traer datos nuevos**? | **No.** Misma razón. |
| ¿Qué acota la ventana offline hoy? | Tres cosas, **las tres del lado del cliente**: el `exp` del JWT (pasa a 16 h), `jornadaOfflineMs = 16 h` de `politica-sesion.funcion.ts`, y el `authGuard`. Cualquiera con DevTools puede alterar las tres — el `auth_session` es JSON plano (§0.6.1). |
| Entonces, ¿qué compra B1 exactamente? | Que el **sistema** deje de estar expuesto. El dato que ya se fue con el aparato está comprometido desde el minuto cero del robo; lo que B1 garantiza es que ese aparato **no vuelve a entrar** — ni a leer más, ni a escribir, ni a sincronizar — apenas toque una red. |
| ¿Qué falta para el dato **en reposo**? | MDM / borrado remoto, PIN de dispositivo, y la decisión D3 de minimizar lo que se guarda. **Nada de eso es software de esta app** y ninguno está en alcance. Decirlo en voz alta evita la falsa sensación de cierre. |

Corolario para el tracker: la frase *«una tablet perdida no se puede revocar»* pasa a ser *«una tablet
perdida queda fuera del sistema en cuanto ve la red; lo que ya se llevó, se lo llevó»*. Es menos
redondo y es lo cierto.

### 6.3 Riesgos técnicos y su mitigación

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **`OnTokenValidated` está en el camino de TODO request** | Un bug ahí es un outage total | Lógica pura en `Calculos` con 14 tests; el hook queda de ~15 líneas; caché de 60 s; **fail-open ante caída de BD** (§1.6) |
| **La BD se cae y todos quedan afuera** | Catástrofe operativa | Excepción deliberada: error de BD ⇒ se acepta el token + log `Error`. Documentado en el código |
| **Revocación no instantánea** (≤60 s + heartbeat) | Expectativa mal puesta | Se escribe en la UI del modal. No prometer «inmediato» |
| **`Jwt.DurationInMinutes` 60 → 960 sin la revocación desplegada** | Tokens de 16 h **irrevocables** — peor que hoy | **B1 entra completo o no entra.** Ver §6.4 |
| **Migración que falla al arrancar en ECS** | SIGSEGV + rollback silencioso | DDL puro, `IF NOT EXISTS`, sin FK, sin DML. Probar `dotnet ef database update` local antes de mergear |
| **`X-Device-Id` cambia la identidad del rate limit de sync** | Cambio de comportamiento real | Solo afecta a `/api/sync/*` (`AlcanceDeRuta`); es la intención declarada del código; caso 11 del smoke lo verifica |
| **Colisión con la sesión paralela** que edita `HttpCurrentUser`, `ActiveCompanyMiddleware`, `CompanyResolver`, `EmpresaActivaCalculos` | Conflicto de merge / build roto | Este plan **no toca esos 4 archivos**. `Program.cs` sí: coordinar antes de editar. Nunca `git commit --amend` |
| **Crecimiento de la tabla** | Miles de filas por mes | Índice parcial sobre vivas + limpieza perezosa de >30 días vencidas |
| **Bundle del front** | Techo de error 2,05 MB | El modal va `loadComponent` (lazy) como el resto de `features/config` desde V22. El margen hoy es ~1,08 MB |

### 6.4 Regla de despliegue (no negociable)

**El cambio de `DurationInMinutes` y la verificación de revocación viajan en el mismo deploy.** Si por
lo que sea hay que partirlo, el orden es: (1) `jti` + tabla + verificación con la vigencia **en 60
min**; (2) recién con eso verificado en prod, subir a 960. **Nunca al revés.** Subir la vigencia sin
revocación es emitir tokens de 16 horas que nadie puede apagar — exactamente lo que D4 se negó a
aceptar.

Post-deploy, verificación obligatoria de la sección 🚀 de CLAUDE.md (qué TaskDef corre de verdad, qué
imagen tiene) — el CLI miente cuando ECS revierte.

---

## 7. Orden de trabajo sugerido (para el tracker, STEP 2)

1. `RevocacionSesionCalculos` + sus 14 tests xUnit. **Verde antes de tocar nada más.**
2. Entidad + configuración + `DbSet` + migración; `dotnet ef database update` en local.
3. `ISesionActivaService` + `SesionActivaService` (con `IMemoryCache`).
4. `jti` en `AuthService` + registro de la sesión + revocación en cambio de contraseña / baja.
5. `OnTokenValidated` en `Program.cs` + DI. `dotnet build` + `dotnet test`.
6. Endpoints en `SessionController` (heartbeat con `last_seen_at`, mis sesiones, admin).
7. Front: `device-id.funcion` + interceptor + `debe-cerrar-sesion-por-401` + `politica-sesion` +
   `session-timeout`. `yarn build` + `yarn test`.
8. Front: modal de sesiones en `user-management` + «Mis dispositivos» en `profile`.
9. `DurationInMinutes` 60 → 960 (**último**, junto con el smoke completo).
10. Smoke §5.3 (1-11), backend local **apagado** y puerto libre al terminar.
11. Casilla aparte: **cerrar la ventana de gracia** de tokens sin `jti`.
