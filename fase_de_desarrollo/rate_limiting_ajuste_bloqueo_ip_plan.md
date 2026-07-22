# Plan — Ajuste rate limiting / bloqueo por IP (prod: "Tu IP ha sido bloqueada temporalmente")

**Fecha:** 2026-07-21 · **Origen:** usuarios legítimos bloqueados en producción al iniciar sesión.

## Diagnóstico (estado actual)

Existen **dos** mecanismos de bloqueo, ninguno con endpoint de desbloqueo por IP:

| Mecanismo | Dónde | Regla actual | Desbloqueo |
|---|---|---|---|
| Bloqueo por IP | `API/Middleware/RateLimitingMiddleware.cs` | Login/register: **5 req/min por IP** · resto: 100 req/min por IP+ruta · al exceder → **IP completa bloqueada 10 min en TODAS las rutas** | ❌ No hay servicio. `IMemoryCache` in-process: solo expira (10 min) o reinicio del backend |
| Bloqueo por cuenta | `Infrastructure/Services/AuthService.cs` (`LoginAsync`) | 5 intentos fallidos → `IsLocked` **15 min** (auto-desbloqueo) | ✅ Update de usuario (`IsLocked=false`), pero **no resetea `FailedAttempts`** → se re-bloquea al primer fallo siguiente |

**Causa raíz del incidente:** oficinas/granjas comparten IP pública (NAT). Varios usuarios iniciando sesión en el mismo minuto superan el límite de 5/min → el middleware bloquea la IP **entera** (incluye usuarios ya logueados) por 10 minutos. El front (`login.component.ts`) muestra el 429 como "🚫 Acceso Bloqueado".

## Enfoque

Reducir la espera y el radio de impacto **sin debilitar la protección real** (el anti fuerza bruta por cuenta sigue siendo el lockout de 5 intentos en `AuthService`; el anti DDoS de borde sigue siendo CloudFront/WAF):

1. **Límite auth: 5 → 15 req/min por IP** (realidad de IP compartida; 15/min sigue siendo inviable para fuerza bruta contra el lockout por cuenta).
2. **Duración del bloqueo: 10 → 3 min** (pedido explícito: menos espera).
3. **Alcance del bloqueo:** exceder el límite de login/register bloquea **solo las rutas de auth** para esa IP (clave `blocked:auth:{ip}`); exceder límites generales mantiene el bloqueo IP completo (`blocked:{ip}`) → postura DoS intacta, pero un pico de logins ya no tumba toda la app de la oficina.
4. **Configurable** vía `appsettings.json` sección `RateLimiting` (override en prod por env `RateLimiting__*` en la TaskDef ECS, sin redeploy de código).
5. **Fix desbloqueo manual de cuenta:** `UserService.UpdateAsync` con `IsLocked=false` resetea también `FailedAttempts`/`LockedAt`.

## Archivos

- `backend/src/ZooSanMarino.Application/Calculos/RateLimitingCalculos.cs` **(nuevo)** — lógica pura: clasificación de ruta, límite aplicable, claves de bloqueo por alcance, segundos restantes, ventana/umbral.
- `backend/src/ZooSanMarino.API/Middleware/RateLimitingMiddleware.cs` — opciones desde `IConfiguration` (defaults nuevos), delega en `RateLimitingCalculos`, bloqueo con alcance, mensajes con `retryAfter`.
- `backend/src/ZooSanMarino.Infrastructure/Services/UserService.cs` — reset de contadores al desbloquear.
- `backend/src/ZooSanMarino.API/appsettings.json` — sección `RateLimiting` explícita.
- `backend/tests/ZooSanMarino.Application.Tests/RateLimitingCalculosTests.cs` **(nuevo)** — xUnit.

**BD:** sin cambios. **Front:** sin cambios (ya muestra `retryAfter`/`message` del backend).

## Casos de prueba (xUnit, lógica pura)

- Ruta `/api/auth/login` y `/api/auth/register` → límite auth; `/swagger/...` → swagger; resto → general.
- Clave de bloqueo: auth → `blocked:auth:{ip}`; general → `blocked:{ip}`.
- Claves a verificar: ruta auth respeta ambos bloqueos; ruta general solo el global.
- `SegundosRestantes`: redondeo hacia arriba, nunca negativo.
- Umbral `>` (contador == límite no bloquea; límite+1 sí) y expiración de ventana de 60 s.

## Tracker de esta tarea

> `tracker_estado.md` está ocupado por otra sesión activa en paralelo (fix fechas -1 día engorde); para no pisar su estado, el checklist vive acá.

- [x] Diagnóstico: middleware IP (5/min login, bloqueo 10 min TODA la IP) + lockout cuenta (5 fallos → 15 min) · sin servicio de desbloqueo de IP
- [x] `RateLimitingCalculos.cs` nuevo en `Application/Calculos/` (lógica pura)
- [x] `RateLimitingMiddleware.cs`: opciones por `IConfiguration` (auth 15/min, bloqueo 3 min), bloqueo acotado a auth, delega en Calculos
- [x] `UserService.UpdateAsync`: desbloquear cuenta resetea `FailedAttempts`/`LockedAt`
- [x] `appsettings.json`: sección `RateLimiting`
- [x] Tests xUnit `RateLimitingCalculosTests.cs` (16 casos)
- [x] Build Release 0 errores/0 warnings + `dotnet test` 517/517 verde (Release; el bin Debug del API está bloqueado por el backend corriendo en la otra sesión)

## Riesgos / reversa

- Valores tuneables por env sin tocar código; rollback = revertir commit (sin migraciones).
- No se agrega endpoint de desbloqueo de IP (superficie de ataque innecesaria con bloqueos de 3 min); si se necesitara, va con auth de admin y ruta sin "admin" (WAF).
