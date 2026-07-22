# Plan — Sesión deslizante por inactividad (auto-logout 5 min + desconexión)

## Objetivo
Cerrar la sesión automáticamente y enviar al login cuando:
1. El usuario está **5 minutos sin interactuar** (sesión deslizante: cada interacción reinicia el contador).
2. Se **pierde la conexión con el backend** (con tolerancia a microcortes).

## Decisiones (con el usuario)
1. **Inactividad = interacción del usuario** (mouse/teclado/clic/scroll/touch). No se desloguea a alguien leyendo/llenando un formulario.
2. **Token JWT se queda en 60 min** (NO se reduce a 5). Los 5 min de inactividad se **enforcan en el cliente**. No hay reemisión de token en el servidor.
3. **Desconexión con tolerancia**: se cierra la sesión si el heartbeat falla de forma sostenida (o ante un 401), no ante un solo parpadeo de red.

## Enfoque
- La política de 5 min vive en el **frontend** (`SessionTimeoutService`): timer reiniciado por eventos de interacción.
- Un **heartbeat** liviano detecta la conexión al backend y captura la expiración real del token (401).
- Backend: solo un endpoint de heartbeat autenticado, **excluido del rate limiter** (para no bloquear oficinas NAT con muchos usuarios).
- Refactor ≠ cambio de comportamiento del auth existente: no se toca el login, el JWT, ni el `authGuard`.

## Backend
| Archivo | Cambio |
|---|---|
| `API/Controllers/SessionController.cs` (nuevo) | `[Authorize] GET /api/session/heartbeat` → `200 { ok, serverTimeUtc }`. Devuelve 401 si el token expiró (señal de fin de sesión). |
| `API/Middleware/RateLimitingMiddleware.cs` | Bypass del rate limiter para `/api/session/heartbeat` (endpoint autenticado, no es vector de fuerza bruta; evita bloquear la IP compartida de una oficina). |

## Frontend
| Archivo | Cambio |
|---|---|
| `core/auth/session-timeout.service.ts` (nuevo) | Servicio raíz. Escucha `storage.session$` (start/stop). Listeners de interacción (throttle) → `lastActivity`. Timer idle (cada 15 s): `now-lastActivity ≥ 5 min` → fin de sesión "inactividad". Heartbeat (cada 90 s, solo si el usuario estuvo activo): éxito → resetea fallos; error de red (status 0) → cuenta, ≥2 seguidos → fin "sin conexión"; 401 → fin "sesión expirada". `endSession` idempotente: `auth.logout()` + toast + `router.navigate(['/login'])`. Corre fuera de la zona Angular (evita CD storms). |
| `core/auth/auth.service.ts` | `heartbeat()` → `GET {apiUrl}/session/heartbeat`. |
| `core/auth/auth.interceptor.ts` | En la respuesta, si status **401** y había token (petición autenticada) → notifica a `SessionTimeoutService.onUnauthorized()` (fin de sesión). No toca 401 de login (sin token). |
| `app.component.ts` | `ngOnInit` → `sessionTimeout.init()` (igual patrón que `versionCheckService`). |

## Parámetros
- `IDLE_LIMIT_MS = 5 min`, `HEARTBEAT_MS = 90 s`, `MAX_HEARTBEAT_FAILS = 2`, idle-check cada 15 s.
- Eventos de actividad: `pointerdown`, `keydown`, `mousemove` (throttle ≤1/s), `scroll`, `touchstart`, `wheel`, `visibilitychange`.

## Reglas / bordes
- Solo aplica con sesión presente (no dispara en /login).
- Idempotente: una sola vez por cierre; luego `stop()`.
- Multi-pestaña: `TokenStorageService` ya sincroniza `session$` por `storage` event → al cerrar en una pestaña las demás ven `session=null` y su `authGuard` redirige al navegar. (Sincronía total de idle entre pestañas queda como mejora futura.)
- **Limitación conocida**: con token de 60 min sin reemisión, un usuario activo >60 min continuos es deslogueado al expirar el token (comportamiento actual). Se puede agregar refresh rodante después si se desea.

## Casos de prueba
1. Idle 5 min sin interacción → toast + redirect a /login. (manual)
2. Interacción antes de los 5 min → NO desloguea; el contador se reinicia. (manual)
3. Backend caído mientras activo → tras ~2 heartbeats fallidos → "sin conexión" + login. (manual: apagar back)
4. Token expirado (401 en heartbeat/petición) → "sesión expirada" + login. (manual)
5. Heartbeat NO dispara el rate limiter (varias pestañas/usuarios). (verificación)
6. Login normal sigue funcionando; 401 de credenciales NO redirige raro. (manual)

## Validación
- `dotnet build` 0 errores + `dotnet test`.
- `yarn build` 0 errores.
- Walkthrough con back+front arriba (idle, desconexión).
