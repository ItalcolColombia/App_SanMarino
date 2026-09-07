# Llave de plataforma por sesión (derivada del `jti`)

> Motivación: hallazgo de auditoría sept-2026 — `environment.prod.ts` embebe `platformSecret.secretUpFrontend`
> + `platformSecret.encryptionKey` en el bundle JS, así que el header `X-Secret-Up` es un secreto
> estático extraíble por cualquiera. Este cambio lo reemplaza (en el frontend web) por una llave
> **por sesión**, derivada del `jti` del JWT, que ya se valida en cada request contra `sesiones_activas`
> (B1). Ver `respuesta_auditoria_ciberseguridad_2026-09.md` §4.

## Enfoque arquitectónico

- **`X-Secret-Up` sigue siendo un filtro de origen, no autenticación.** No cambia su rol: corre antes
  de `UseAuthentication`, sin consulta a BD, y frena tráfico automatizado. Lo que cambia es de dónde
  sale el valor.
- **Nuevo valor = `HMAC-SHA256(PlatformSecret:DerivationKey, jti)` en Base64.** El `jti` es el
  identificador de sesión que B1 ya emite (`AuthService.GenerateResponseAsync`) y valida contra
  `sesiones_activas` en `JwtBearerEvents.OnTokenValidated`. La llave de plataforma hereda gratis:
  vida de sesión, expiración y revocación.
- **`DerivationKey` es un secreto SOLO del servidor** (variable de entorno del task definition, nunca
  en ningún bundle). Sin `DerivationKey` configurada, el backend no emite `platformKey` y todo el
  mundo sigue por el camino legacy: **fail-safe, cero cambios de comportamiento.**
- **El middleware corre antes de `UseAuthentication`**, así que lee el `jti` parseando el payload del
  JWT **sin validar la firma** (la validación real la hace `OnTokenValidated` unos ms después). Es
  seguro: forjar un `X-Secret-Up` válido para un `jti` arbitrario exige la `DerivationKey`, y un JWT
  con firma falsa igual muere en `OnTokenValidated`.
- **Transición sin ventana de corte.** El middleware acepta **cualquiera** de las dos:
  (a) la firma derivada del `jti` (web nuevo), (b) el secreto estático cifrado legacy (web viejo con
  sesión previa al deploy, **y la app móvil, que no se toca**). El frontend manda la derivada si la
  tiene en la sesión; si no (sesión previa al deploy), cae al secreto estático.
- **Fase B (NO en este plan, va como ítem de tracker sin marcar):** cuando las métricas muestren que
  ~todo el tráfico web usa la firma derivada (≤ 3 días, vida máx. de sesión offline 16 h), quitar
  `secretUpFrontend`/`encryptionKey` de `environment.prod.ts` y la aceptación del secreto web legacy
  del middleware (la móvil se conserva). **Recién ahí cierra del todo el hallazgo.**

## Archivos

### Backend — nuevos
- `backend/src/ZooSanMarino.Application/Calculos/PlatformSecretCalculos.cs` — puro:
  - `DerivarClaveSesion(string? jti, string? derivationKey) : string?` — `HMACSHA256` → Base64; `null` si falta algún insumo.
  - `FirmasCoinciden(string? a, string? b) : bool` — `CryptographicOperations.FixedTimeEquals` (tiempo constante).
  - `LeerJtiDeAuthorizationHeader(string? header) : string?` — `Bearer x.y.z` → base64url-decode del payload → `jti`. Sin validar firma. Fail-safe: cualquier error → `null`.
- `backend/tests/ZooSanMarino.Application.Tests/PlatformSecretCalculosTests.cs` — xUnit.

### Backend — modificados
- `Application/DTOs/AuthResponseDto.cs` — `+ string? PlatformKey`.
- `Infrastructure/Services/AuthService.cs` — `+ IConfiguration` al ctor; en `GenerateResponseAsync`,
  después de tener el `jti`: `response.PlatformKey = PlatformSecretCalculos.DerivarClaveSesion(jti.ToString(), _config["PlatformSecret:DerivationKey"])`.
  Es el único lugar que arma el DTO con token+jti (lo llaman `LoginAsync` y `RegisterAsync`).
- `API/Middleware/PlatformSecretMiddleware.cs` — `+ _derivationKey` del config en el ctor; en
  `InvokeAsync`, antes del `Decrypt` legacy: si hay `Authorization: Bearer` y `DerivationKey`,
  `jti = LeerJtiDeAuthorizationHeader(...)`, `esperada = DerivarClaveSesion(jti, _derivationKey)`,
  `if (FirmasCoinciden(encryptedSecretUp, esperada)) { cliente = ClienteWeb; ... }`. Si no matchea,
  sigue **exactamente** el camino legacy actual (decrypt + comparación con estáticos). Lista de
  exenciones y todo lo demás: **idéntico**.
- `API/appsettings.json` + `appsettings.json.example` + `appsettings.Production.json.example` —
  `PlatformSecret:DerivationKey` con valor de DEV (`"DevOnly#DerivationKey#NOT-FOR-PROD"`), y comentario
  de que prod lo pasa por `PlatformSecret__DerivationKey` en el task definition.

### Frontend — nuevos
- `frontend/src/app/core/auth/funciones/resolver-firma-plataforma.funcion.ts` — puro:
  `(session: { platformKey?: string } | null, secretoEstatico?: string) => { modo: 'derivada' | 'legacy'; valor: string | null }`.
- `.../resolver-firma-plataforma.funcion.spec.ts`.

### Frontend — modificados
- `core/auth/auth.models.ts` — `AuthSession + platformKey?: string`; `LoginResult + platformKey?: string`.
- `core/auth/auth.service.ts` — en el `map(res => ...)`: `const platformKey = rawRes.platformKey || rawRes.PlatformKey;` y `session.platformKey = platformKey || undefined`.
- `core/auth/auth.interceptor.ts` — resolver la firma con la función pura: si `modo === 'derivada'`,
  `of(valor)`; si no, el camino actual `from(encryption.encryptSecretUp(secretUpFrontend))`. El resto
  del interceptor no cambia.

### BD / SQL
**Ninguno.** No hay migración, no hay `.sql`. No se toca `sesiones_activas` (se reusa el `jti`).

## Reglas de negocio

- **R1** `platformKey` se emite **solo si** `PlatformSecret:DerivationKey` está configurada. Ausente ⇒
  `PlatformKey = null` ⇒ el front no lo encuentra ⇒ manda el estático legacy ⇒ comportamiento vigente.
- **R2** El middleware acepta `X-Secret-Up` si: **(a)** es `FixedTimeEquals` con `HMAC(DerivationKey, jti-del-bearer)`,
  **o (b)** descifrado con `EncryptionKey` es igual a `SecretUpFrontend` **o** `SecretUpMovil` (legacy, intacto).
- **R3** Sin `Authorization: Bearer`, o `jti` ilegible, o `DerivationKey` ausente ⇒ el camino (a) se
  saltea sin error y se evalúa (b). Nunca lanza.
- **R4** Exenciones (`/swagger*`, `*/health`, `*/ping`, `/auth/login|register|recover-password`,
  PAT `sk_`) y OPTIONS: **idénticas**. El header `X-Platform-Client` se sigue escribiendo (`web`/`movil`).
- **R5** Comparaciones de firma en **tiempo constante** (`CryptographicOperations.FixedTimeEquals`).
- **R6** El `jti` se lee del JWT **sin validar la firma**. La validación real (firma, `exp`, B1) sigue
  en `OnTokenValidated`, sin cambios.
- **R7** App móvil: **no se toca**. Sigue mandando `SecretUpMovil` cifrado; el middleware lo acepta por (b).

## Casos de prueba

### `PlatformSecretCalculosTests` (xUnit)
1. `DerivarClaveSesion("jti-1", "k")` es determinista y ≠ `DerivarClaveSesion("jti-2", "k")`.
2. `DerivarClaveSesion(null|"", "k")` y `DerivarClaveSesion("jti", null|"")` ⇒ `null`.
3. `FirmasCoinciden(x, x)` ⇒ true; `("x","y")`, `(null,"x")`, `("x",null)` ⇒ false; largos distintos ⇒ false sin excepción.
4. `LeerJtiDeAuthorizationHeader("Bearer <jwt con jti=abc>")` ⇒ `"abc"`.
5. `LeerJtiDeAuthorizationHeader` con: `null`, `""`, `"Bearer "`, `"Basic x"`, `"Bearer no.es.jwt"`, JWT sin `jti`, base64 roto ⇒ `null` (nunca lanza).
6. Ida y vuelta: `jti` del vector 4 + `DerivarClaveSesion` ⇒ el mismo Base64 que produce un `HMACSHA256` de referencia.

### `resolver-firma-plataforma.funcion.spec.ts` (Jasmine)
7. `session.platformKey` presente ⇒ `{ modo: 'derivada', valor: platformKey }`.
8. `session` null / sin `platformKey` ⇒ `{ modo: 'legacy', valor: secretoEstatico ?? null }`.

### Smoke HTTP (backend local :5002, con `PlatformSecret:DerivationKey` en `appsettings.Development.json`)
9. `POST /api/Auth/login` (cuerpo cifrado) ⇒ la respuesta descifrada trae `platformKey` no vacío.
10. `GET /api/Company` con `X-Secret-Up: <platformKey>` + `Authorization: Bearer <token>` ⇒ **no** 401 `platform-secret` (pasa el filtro; 401 `Bearer` si el token no vale, que es lo correcto).
11. `GET /api/Company` con `X-Secret-Up: <platformKey de OTRO jti>` + token ⇒ 401 `platform-secret`.
12. `GET /api/Company` con el `X-Secret-Up` estático cifrado legacy (sin cambiar nada del front viejo) ⇒ sigue pasando (compat).
13. `GET /api/Company` sin `X-Secret-Up` ⇒ 401 `platform-secret` (igual que hoy).
14. Sin `PlatformSecret:DerivationKey` en config: `login` no trae `platformKey`; el estático legacy sigue siendo la única vía y funciona.

### Gates del repo
15. `cd backend && dotnet build` 0/0 sin advertencias nuevas · `dotnet test` verde.
16. `cd frontend && yarn build` OK · `yarn test --watch=false` verde · `node scripts/verificar-change-detection.js` OK.

## Verificación de integración (manual, antes de mergear)
- **Llavero de sesiones aparcadas** (`LlaveroSesionesService`): al aparcar y restaurar una sesión, el
  `platformKey` tiene que sobrevivir (viaja dentro del blob `AuthSession` sellado). Verificar que
  `aparcar`/`restaurar` serializan el objeto completo.
- **Cola offline (PWA)**: `SyncService` usa `HttpClient` ⇒ pasa por `authInterceptor` **al enviar**,
  así que una petición encolada toma el `platformKey` vigente al drenar la cola, no uno viejo. Si la
  sesión venció estando offline, el 401 al drenar es correcto (y el outbox ya lo maneja).

## Fase B — NO ejecutar en este plan (queda como ítem sin marcar)
- Métrica/log: contar en el middleware cuántos requests entran por (a) vs (b).
- Cuando (b)-web ≈ 0 durante 48 h: quitar `secretUpFrontend` + `encryptionKey` de `environment.prod.ts`
  y `environment.ts`; en el middleware, dejar de aceptar el `SecretUpFrontend` estático (conservar
  `SecretUpMovil`); actualizar el interceptor para no tener fallback.
- Setear `PlatformSecret__DerivationKey` en el task definition de ECS (acción AWS, con el resto de secretos).
