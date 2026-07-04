# PAT / Service Access Token — Plan

## Objetivo
Mecanismo de token de servicio (PAT) de larga duración para que un cron headless llame SOLO
`/api/tickets/**` (y `/api/auth/ping`) sin pasar por el login (que en prod exige reCAPTCHA + payload AES).
El PAT mapea a un usuario existente (dev global) y produce el MISMO `ClaimsPrincipal` que su JWT.

## Enfoque arquitectónico
- **Auth scheme "Smart"** (policy scheme): reenvía por prefijo del header Authorization.
  - `Bearer sk_...` → esquema `ServiceToken` (nuevo handler).
  - Cualquier otro `Bearer ...` → `JwtBearer` (config existente TAL CUAL, solo movida dentro de la cadena).
- El `ServiceTokenAuthHandler` valida el hash contra `service_tokens`, verifica scope de ruta,
  carga el usuario dueño y construye un `ClaimsPrincipal` idéntico al del JWT + claim `token_type=service`.
- Lógica pura de token (generar/hashear/verificar) en `Application/Calculos/ServiceTokenHasher.cs`
  (sin EF), reutilizada por el service. Tests unitarios sobre ella.

## Claims replicados (idénticos al JWT de AuthService.GenerateResponseAsync)
Del usuario dueño, cargados desde BD:
- `ClaimTypes.NameIdentifier` = user.Id (Guid) → lo lee `HttpCurrentUser.UserGuid`
- `JwtRegisteredClaimNames.Sub` = user.Id
- `JwtRegisteredClaimNames.UniqueName` = email · `JwtRegisteredClaimNames.Email` = email
- `firstName`, `surName`
- `user_id` = `Math.Abs(user.Id.GetHashCode())` (mismo cálculo que el JWT)
- `ClaimTypes.Role` por cada rol (distinct)
- `company_id` por cada company · `company` (nombre) por cada company
- `permission` por cada permiso agregado de roles
- `token_type` = "service" (marcador extra del PAT)

## Archivos a crear
1. `Domain/Entities/ServiceToken.cs` — entidad pura (Id long, Name, TokenHash, UserId Guid,
   Scopes, ExpiresAt?, RevokedAt?, LastUsedAt?, CreatedAt).
2. `Infrastructure/Persistence/Configurations/ServiceTokenConfiguration.cs` — ToTable("service_tokens"),
   índice único en TokenHash. snake_case por convención (no columnas a mano salvo lo necesario).
3. DbSet en `ZooSanMarinoContext` (`Set<ServiceToken>()`).
4. Migración `AddServiceTokens` — reescrita IDEMPOTENTE (CREATE TABLE IF NOT EXISTS + CREATE UNIQUE INDEX IF NOT EXISTS; Down = DROP TABLE IF EXISTS).
5. `Application/Calculos/ServiceTokenHasher.cs` — static: GenerateToken() (`sk_`+Base64Url(32 bytes CSPRNG)), Hash(plain) SHA-256 hex, Verify(plain, hash).
6. `Application/DTOs/ServiceTokenDto.cs` — record (Id, Name, Scopes, ExpiresAt, RevokedAt, LastUsedAt, CreatedAt). Sin hash ni plano.
7. `Application/Interfaces/IServiceTokenService.cs` — IssueAsync / RevokeAsync / ValidateAsync.
8. `Infrastructure/Services/ServiceTokenService.cs` — implementación. Persiste SOLO hash.
9. `Infrastructure/Auth/ServiceTokenAuthHandler.cs` — AuthenticationHandler<AuthenticationSchemeOptions>.
10. `API/Controllers/ServiceTokensController.cs` — ruta `api/service-tokens` (NO "admin"). [Authorize(Roles="Admin")].

## Archivos a modificar
- `Program.cs`: cadena de auth "Smart" (policy scheme) + DI Scoped de IServiceTokenService + ServiceTokenAuthHandler.
- `ZooSanMarinoContext.cs`: DbSet<ServiceToken>.

## Cambios BD/SQL
- Nueva tabla `service_tokens` (snake_case) vía migración EF idempotente. NO se ejecuta contra ninguna BD
  (se aplica sola al arrancar). Solo `dotnet ef migrations add` + `list`.

## Reglas de negocio
- Emitir: solo rol "Admin" (mismo criterio que ConfigurationController). Owner = usuario actual (ICurrentUser).
- Token plano se devuelve UNA sola vez; en BD solo SHA-256 hex.
- ValidateAsync: hash coincide + no revocado + no expirado → actualiza LastUsedAt.
- Scope de ruta en el handler: si Path NO empieza con `/api/tickets` y no es `/api/auth/ping` → Fail.

## Casos de prueba (ServiceTokenHasherTests)
- (a) Hash determinístico (mismo plano → mismo hash).
- (b) Verify(plano, hash)==true; Verify(plano, otroHash)==false.
- (c) Token generado empieza con `sk_` y tiene longitud esperada (Base64Url de 32 bytes = 43 chars + prefijo).
- (d) Dos tokens generados son distintos (aleatoriedad).

## Restricciones duras
- NO tocar el módulo de tickets. NO ejecutar app/deploys/BD. Solo `dotnet build` + `dotnet ef migrations add/list`.
- No editar .csproj (SDK-style globbing). 0 warnings / 0 errors + tests verdes.
