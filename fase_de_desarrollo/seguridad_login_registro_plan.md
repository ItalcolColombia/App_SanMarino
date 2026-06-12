# Plan — Endurecimiento de seguridad de Login y Registro

> **Meta:** blindar la superficie pública de autenticación para que un atacante **no pueda mapear el backend ni explotar el login/registro**. Plan concreto, file-by-file, listo para desarrollo.

---

## ✅ Fase 0 — Diagnóstico cerrado (verificado en AWS, 2026-06-11)

TaskDef en uso: `sanmarino-back-task:103` · `ASPNETCORE_ENVIRONMENT=Production` · `Secrets: null` (nada en Secrets Manager; todo es env plano o `appsettings.json`).

| Dato verificado | Resultado | Implica |
|---|---|---|
| `JwtSettings__Key` (env) | Sobreescrita en prod, **distinta** a la del repo, pero **plano en TaskDef** y patrón adivinable (`..._2024_SuperSecure_...`) | H5 baja a MEDIA: no forjable con la key del repo, pero rotar + mover a Secrets Manager |
| `Encryption__*`, `Recaptcha__SecretKey`, `Swagger__Password`, `PlatformSecret__*` | **NO** están en env → prod usa el `appsettings.json` **versionado** | H3 CRÍTICA confirmada: estos secretos viven en git y se usan en prod |
| `AllowedOrigins` (env) | `http://localhost:4200, https://sanmarinoapp.com, http://localhost:8080, http://sanmarino-alb-...amazonaws.com` | No es `*` (bien), pero incluye localhost y orígenes `http://` en prod → recortar |
| Consumidor de `/api/Auth/register` | El front da de alta usuarios por `POST /api/Users` ([user.service.ts:103-117](../frontend/src/app/core/services/user/user.service.ts)). **`/Auth/register` no lo usa nadie** | H1: endpoint anónimo huérfano → blindar/quitar con riesgo casi nulo |
| `Database__RunMigrations` | `true` | Migraciones se autoaplican en deploy (OK para tabla de tokens de reseteo) |

---

## FASE 1 — Críticas (cierre de accesos)

### 🔴 H1 — `/Auth/register` no debe permitir auto-asignar roles/empresas
**Dónde:** [`AuthController.cs:170-206`](../backend/src/ZooSanMarino.API/Controllers/AuthController.cs)
**Qué hacer (recomendado, riesgo nulo — no hay consumidor front):**
- Cambiar `[AllowAnonymous]` → `[Authorize]` y exigir permiso `users.create`.
- En `RegisterDto`, ignorar `RoleIds`/`CompanyIds` salvo que el solicitante tenga alcance sobre ellos (validar contra los claims `company_id`/`permission` del que llama).
- Alternativa mínima si se prefiere no tocar auth aún: dejarlo anónimo pero **borrar** `RoleIds`/`CompanyIds` del flujo (forzar `user.Id` sin rol ni empresa hasta aprobación admin).

**Prueba:** `POST /Auth/register` anónimo con `roleIds:[1]` → 401, o usuario creado **sin** rol/empresa.

### 🔴 H2 — Autorización efectiva (hoy `[Authorize]` no autoriza nada)
**Dónde:** [`Program.cs:394-405`](../backend/src/ZooSanMarino.API/Program.cs) y el `AllowAllPolicyProvider` ([Program.cs:816+](../backend/src/ZooSanMarino.API/Program.cs))
**Qué hacer:**
- Eliminar `DefaultPolicy`/`FallbackPolicy = RequireAssertion(_ => true)` y el `AllowAllPolicyProvider`.
- Poner:
  ```csharp
  opt.FallbackPolicy = new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser().Build();
  ```
- Los públicos (`login`, `register` si queda anónimo, `recover-password`, `ping-simple`, `/health`) quedan con `[AllowAnonymous]` explícito.
- Si hay endpoints que usan `[Authorize(Policy="…")]`, registrar esas políticas reales sobre los claims `permission` que ya emite [`AuthService.GenerateResponseAsync:265-266`](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs).

**Prueba:** endpoint protegido sin token → 401; con token sin el permiso → 403. Login/registro/recover siguen abiertos.
**⚠️ Riesgo:** alto de regresión funcional (hoy “todo pasa”). Hacer barrido de controllers antes de mergear.

### 🔴 H3 — Secretos fuera del repo + rotación
**Dónde:** `appsettings.json` (trackeado pese a `.gitignore`), [`environment.prod.ts:13-32`](../frontend/src/environments/environment.prod.ts)
**Qué hacer:**
1. `git rm --cached backend/src/ZooSanMarino.API/appsettings.json backend/src/ZooSanMarino.API/appsettings.Development.json` (dejar solo los `.example`).
2. **Rotar** y mover a env/Secrets Manager (ECS): `Encryption__RemitenteFrontend`, `Encryption__RemitenteBackend`, `Recaptcha__SecretKey`, `Swagger__Password`, `PlatformSecret__*`, y de paso `JwtSettings__Key` (a clave aleatoria).
3. Las llaves de `environment.prod.ts` son **públicas por diseño** (van en el bundle) → tratarlas como ofuscación, nunca como secreto (ver H6).

**Prueba:** `git ls-files | grep appsettings.json` → vacío. App levanta leyendo secretos de env.
**⚠️ Riesgo:** impacta deploy → requiere OK explícito + coordinar variables en la TaskDef antes de mergear.

---

## FASE 2 — “Que no puedan leer los servicios ni identificar el back”

### 🟠 Swagger fuera de producción
**Dónde:** [`Program.cs:643-647`](../backend/src/ZooSanMarino.API/Program.cs)
**Qué hacer:** envolver `app.UseSwagger()/UseSwaggerUI()` y `/swagger/download` en `if (!app.Environment.IsProduction())`. (`ASPNETCORE_ENVIRONMENT=Production` ya está, así que basta el gate.)
**Prueba:** `GET /swagger` en prod → 404.

### 🟠 H8 — Sin fugas que delaten el stack
**Dónde / qué:**
- [`Program.cs:361-365`](../backend/src/ZooSanMarino.API/Program.cs): **borrar** los `Console.WriteLine` que imprimen `Authorization` header, `ctx.Token` y método/path. Igual los de [`AuthService.cs:338-341`](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs) (CompanyPaises) y [`AuthController.cs:72-74`](../backend/src/ZooSanMarino.API/Controllers/AuthController.cs) (“Datos desencriptados: Email=…”).
- [`AuthController.cs:138-165`](../backend/src/ZooSanMarino.API/Controllers/AuthController.cs): respuesta de login **genérica** (“No fue posible iniciar sesión”), sin distinguir BD/Npgsql/credenciales. El detalle solo a `ILogger` interno.
- [`AuthService.cs:607-625`](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs): dejar de meter `ex.Message`/`StackTrace` en el mensaje de `InvalidOperationException` de recover.

**Prueba:** login con BD caída → mensaje genérico; logs sin token/password/stack.

### 🟠 H11 — CORS solo orígenes reales de prod
**Dónde:** variable `AllowedOrigins` en TaskDef (no en código).
**Qué hacer:** dejar `https://sanmarinoapp.com` (+ el dominio real del portal si difiere). Quitar `localhost:*` y el origen `http://...amazonaws.com` en prod.
**Prueba:** preflight desde origen no listado → bloqueado.

### 🟡 H6 — Reclasificar el “cifrado” como ofuscación (doc, no código)
El cifrado front↔back ([`EncryptionService.cs`](../backend/src/ZooSanMarino.Infrastructure/Services/EncryptionService.cs) / [`encryption.service.ts`](../frontend/src/app/core/auth/encryption.service.ts)) y el `SECRET_UP` usan llave embebida + salt fijo → **no** dan confidencialidad. La confidencialidad la da TLS.
**Qué hacer:** README de seguridad dejando claro que la capa real es HTTPS obligatorio (HSTS ya está en [`SecurityHeadersMiddleware.cs:106-113`](../backend/src/ZooSanMarino.API/Middleware/SecurityHeadersMiddleware.cs)); verificar redirect HTTP→HTTPS en ALB/CloudFront. No invertir más esfuerzo en el cifrado de payload.

---

## FASE 3 — Resistir abuso y robo de credenciales

### 🟠 H4 — Rate limiting real
**Dónde:** [`Program.cs:552`](../backend/src/ZooSanMarino.API/Program.cs) (hoy comentado) + headers falsos en [`SecurityHeadersMiddleware.cs:75-77`](../backend/src/ZooSanMarino.API/Middleware/SecurityHeadersMiddleware.cs)
**Qué hacer:** usar el rate limiter nativo `Microsoft.AspNetCore.RateLimiting`:
- Política `auth` (5–10/min por IP) sobre `/auth/login`, `/auth/register`, `/auth/recover-password`.
- Política global moderada para el resto.
- **Borrar** los `X-RateLimit-*` fijos del SecurityHeaders (son mentira).

**Prueba:** 11 logins/min desde una IP → 429.

### 🟠 H7 — Recuperación de contraseña segura
**Dónde:** [`AuthService.cs:484-627, 677-683`](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs)
**Qué hacer:**
- No regenerar ni enviar la contraseña por email. Crear **token de un solo uso, expiración corta (15–30 min), guardado hasheado**; el email lleva un enlace.
- Reemplazar `new Random()` por `RandomNumberGenerator` (CSPRNG).
- Respuesta **idéntica** exista o no el usuario (anti-enumeración).
- **BD:** migración EF idempotente → tabla `password_reset_tokens` (`id`, `login_id` FK, `token_hash`, `expires_at`, `used_at`, `created_at`).

**Prueba:** recover con email válido → email con enlace (no password); token caduca y no se reutiliza; cuenta legítima no queda bloqueada.

### 🟡 H9 — Anti-enumeración
**Dónde:** [`AuthService.cs:506-513`](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs) (recover) y [`:41-42`](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs) (register).
**Qué hacer:** mensajes neutros que no revelen si el correo existe. (Login ya unifica en “Credenciales inválidas”.)

### 🟡 H10 — Política de contraseñas + lockout temporal
**Dónde:** [`RegisterDto.cs:20`](../backend/src/ZooSanMarino.Application/DTOs/RegisterDto.cs), [`AuthService.cs:143-148, 173`](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs)
**Qué hacer:**
- Mínimo 8–12 + complejidad (validador en DTO y espejo en el form Angular).
- Lockout **temporal** (desbloqueo automático tras N min) en vez de `IsLocked` permanente → evita DoS de cuenta. Reusar `failed_attempts`/`locked_at`; opcional `lockout_until`.

### 🟡 H12 — register consistente
Si `/register` queda anónimo: aplicarle el mismo rate limit y reCAPTCHA que login.

---

## Resumen ejecutable (orden recomendado)

| Orden | Item | Archivo principal | Riesgo |
|---|---|---|---|
| 1 | H1 register | `AuthController.cs` + `RegisterDto.cs` | bajo |
| 2 | Swagger off prod | `Program.cs` | bajo |
| 3 | H8 logs/errores | `Program.cs`, `AuthController.cs`, `AuthService.cs` | bajo |
| 4 | H11 CORS | TaskDef `AllowedOrigins` | bajo |
| 5 | H4 rate limit | `Program.cs`, `SecurityHeadersMiddleware.cs` | medio |
| 6 | H2 autorización | `Program.cs` | **alto (regresión)** |
| 7 | H3 secretos + rotación | `appsettings.json`, TaskDef | **alto (deploy)** |
| 8 | H7 recover + BD | `AuthService.cs` + migración EF | medio |
| 9 | H9/H10/H12 | varios | bajo |

**Validación global:** `cd backend && dotnet build && dotnet test` (0 errores) · `cd frontend && yarn build` · `make down`.

**Requiere OK explícito antes de ejecutar:** #6 (autorización), #7 (rotación + `git rm --cached`), apagar Swagger, recortar CORS — por ser irreversibles o con impacto en deploy.
