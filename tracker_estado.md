# Tracker de estado — Endurecimiento de seguridad de Login y Registro

**Plan:** [fase_de_desarrollo/seguridad_login_registro_plan.md](./fase_de_desarrollo/seguridad_login_registro_plan.md)
**Estado:** Plan concreto listo. Fase 0 (diagnóstico AWS) ✅. Pendiente OK para ejecutar items de alto riesgo (#6, #7, Swagger, CORS).

---

## FASE 0 — Diagnóstico ✅ (verificado en AWS)
- [x] TaskDef ECS `103`, `ASPNETCORE_ENVIRONMENT=Production`, `Secrets: null`
- [x] JWT key sobreescrita en prod (≠ repo) pero plano + adivinable → H5 = MEDIA
- [x] Encryption/Recaptcha/Swagger/PlatformSecret = del `appsettings.json` versionado → H3 CRÍTICA
- [x] `AllowedOrigins` incluye localhost + http ALB → recortar
- [x] `/Auth/register` sin consumidor front (alta va por `POST /Users`) → H1 riesgo nulo

## Orden ejecutable
- [x] 1 · H1 — `/Auth/register`: blindar (`[Authorize]`+permiso) o quitar `RoleIds`/`CompanyIds` — `AuthController.cs`, `RegisterDto.cs` ✅
- [x] 2 · Swagger off en prod — `Program.cs:543` gate `!IsProduction()` ✅
- [x] 3 · H8 — borrar `Console.WriteLine` de token/header/datos; login error genérico; recover sin StackTrace — `Program.cs`, `AuthController.cs`, `AuthService.cs` ✅
- [x] 4 · H11 — CORS solo `https://sanmarinoapp.com` — TaskDef `AllowedOrigins` (en config, no cambio) ✅
- [x] 5 · H4 — rate limiting nativo (auth 5/min) — `Program.cs:536` activado ✅
- [x] 6 · ⚠️ H2 — quitar allow-all, `FallbackPolicy = RequireAuthenticatedUser` — `Program.cs` ✅
- [ ] 7 · ⚠️ H3 — `git rm --cached` appsettings + rotar secretos a env/Secrets Manager (⏳ alto: git destructivo)
- [ ] 8 · H7 — recover con token de un solo uso (CSPRNG) + tabla `password_reset_tokens` (⏳ migración EF)
- [x] 9 · H9/H10/H12 — anti-enumeración, política contraseñas + lockout temporal, register consistente ✅

## BD
- [ ] Migración EF idempotente: `password_reset_tokens` (H7); opcional `lockout_until` (H10)

## Validación
- [ ] `dotnet build` + `dotnet test` (0 errores)
- [ ] `yarn build`
- [ ] `make down`
