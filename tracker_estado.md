# Tracker — Sesión deslizante por inactividad (auto-logout 5 min + desconexión)

Plan: [sesion_deslizante_inactividad_plan.md](fase_de_desarrollo/sesion_deslizante_inactividad_plan.md)

Auto-logout tras 5 min sin interacción (sesión deslizante client-side) + cierre por pérdida de conexión
(heartbeat con tolerancia) + 401 → login. Token JWT se queda en 60 min (sin reemisión server-side).

## Backend
- [x] `SessionController`: `GET /api/session/heartbeat` `[Authorize]` → 200/{ok,serverTimeUtc}
- [x] `RateLimitingMiddleware`: bypass del heartbeat (no bloquear IP NAT)
- [x] `dotnet build` 0 errores + `dotnet test` 542 passed

## Frontend
- [x] `SessionTimeoutService` (idle 5 min por interacción + heartbeat 90 s + endSession idempotente)
- [x] `AuthService.heartbeat()`
- [x] `auth.interceptor`: 401 con token → onUnauthorized → endSession
- [x] `AppComponent.ngOnInit` → `sessionTimeout.init()`
- [x] `yarn build` 0 errores (solo warning bundle budget preexistente)

## Validación
- [x] Backend arranca limpio (migraciones al día); heartbeat exige token → 401 sin credenciales
- [ ] (pendiente) Walkthrough en vivo: idle 5 min, interacción resetea, backend caído, 401, login normal (requiere login + esperar 5 min)
