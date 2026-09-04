# Swagger: puerta de acceso, filtros de empresa y banco de pruebas

> Auditoría + plan de corrección. Alcance: `backend/src/ZooSanMarino.API` (Swagger, middlewares de
> acceso) y `backend/tests/ZooSanMarino.Application.Tests`. **No toca contratos de API ni el front.**

## 0) Diagnóstico medido (4-sep-2026)

Fuentes leídas: `Program.cs` (bloques 12 y 14.1-14.4), `Middleware/SwaggerPasswordMiddleware.cs`,
`Middleware/PlatformSecretMiddleware.cs`, `Infrastructure/ActiveCompanyMiddleware.cs`,
`Infrastructure/HttpCurrentUser.cs`, `Infrastructure/FileUploadOperationFilter.cs`,
`ZooSanMarino.API.csproj`, `appsettings*.json`, los 94 `Controllers/*.cs`, `Calculos/RateLimitingCalculos.cs`.

### A. La puerta con contraseña — existe y bloquea, pero tiene 5 defectos

| # | Hallazgo | Dónde |
|---|---|---|
| A1 | **XSS reflejado**: el mensaje de error se interpola CRUDO en el HTML del login. Un `error=` con etiquetas ejecuta script en el navegador del dev. | `SwaggerPasswordMiddleware.ShowLoginPageAsync` |
| A2 | **La contraseña está commiteada** en `appsettings.json` (base, todos los ambientes) **y hardcodeada como fallback en DOS lugares del código** — borrarla del json no cierra nada. | `appsettings.json:60`, `Program.cs` (`/swagger/login`), `SwaggerPasswordMiddleware` (ctor) |
| A3 | **Comparación de contraseña no constante en tiempo** (`password == expectedPassword`). | `Program.cs` (`/swagger/login`) |
| A4 | **El hash de la cookie está duplicado** (inline en `Program.cs` y en el middleware). Si uno cambia, la sesión deja de validar y nadie entra. | ambos |
| A5 | La cookie `*_LastActivity` **la controla el cliente y no está firmada**: quien ya tiene la cookie de auth la renueva indefinidamente. Menor (exige la contraseña), pero el timeout de 6 min es decorativo. | `SwaggerPasswordMiddleware` |

**Contexto que baja la severidad y hay que decir:** todo el bloque Swagger vive dentro de
`if (!app.Environment.IsProduction())` y el `Dockerfile` fija `ASPNETCORE_ENVIRONMENT=Production`
⇒ **en producción no hay `/swagger` en absoluto.** La puerta protege dev/local, no prod.

### B. 🔴 Lo que rompe el uso real — desde Swagger NO se puede llamar al API

| # | Hallazgo | Efecto |
|---|---|---|
| B1 | `PlatformSecretMiddleware` exige `X-Secret-Up` (AES) en **todo `/api/*`**. Swagger UI no lo manda y **no hay `UseRequestInterceptor`**. | **Todo "Try it out" ⇒ 401** `errorCode: platform-secret` |
| B2 | `POST /api/Auth/login` recibe el cuerpo **cifrado** (`EncryptedRequestDto { encryptedData }`). | **No se puede obtener un JWT desde Swagger** ⇒ el botón *Authorize* no tiene qué pegar |

⇒ Hoy Swagger es un catálogo de lectura, **no un banco de pruebas**. Los dos botones que usaría
un tester (*Authorize* y *Try it out*) no llegan a funcionar.

### C. Filtros de empresa — no están declarados en el contrato

| # | Hallazgo |
|---|---|
| C1 | Los **3 headers que deciden el alcance multiempresa** (`X-Active-Company`, `X-Active-Company-Id`, `X-Active-Pais`) **no aparecen en el swagger.json**: no existe ningún `IOperationFilter` que los agregue (el único registrado es `FileUploadOperationFilter`). Desde Swagger **no se puede cambiar de empresa** ⇒ los escenarios multiempresa, que son el corazón del sistema, no se pueden probar. |
| C2 | `AddSecurityRequirement` se aplica **global**: marca como protegidas también las `[AllowAnonymous]` (login, recover-password). Ruido en la doc. |

La **regla** de empresa activa sí está bien hecha y probada: `EmpresaActivaCalculos` (puro, 12 tests)
y `ActiveCompanyMiddleware` fail-closed. Lo que falta es **declararla en el contrato**.

### D. La documentación escrita no llega a la UI

| # | Hallazgo |
|---|---|
| D1 | **`GenerateDocumentationFile` NO está en el `.csproj`** y `c.IncludeXmlComments(...)` está **comentado** en `Program.cs`. Hay **79 de 94 controllers con `/// <summary>`** —varios explican justamente el alcance por empresa— y **ninguno se ve en Swagger**. Mejora más barata y más grande. |
| D2 | Sólo **26 de 94** controllers tienen `[Tags]`; 73/94 declaran `ProducesResponseType`. Con ~94 controllers la UI queda difícil de navegar. |

### E. Tests

- ✅ Cubierto: `RateLimitingCalculosTests` prueba `EsRutaSwagger` y el límite de 50/min;
  `EmpresaActivaCalculosTests` (12) prueba la regla de empresa activa. 2371 `[Fact]`/`[Theory]` en total.
- ❌ **Sin un solo test**: la decisión de acceso a Swagger (contraseña, expiración, rutas exentas).
  Hoy es código imperativo dentro del middleware ⇒ no testeable sin extraerlo a
  `Application/Calculos`, que es justo el patrón que manda el CLAUDE.md.

## 1) Enfoque arquitectónico

1. **Extraer la decisión pura** del gate a `Application/Calculos/SwaggerAccesoCalculos.cs` (`static`,
   sin `HttpContext`): ruta protegida, ruta exenta, comparación en tiempo fijo, huella de sesión,
   vigencia por inactividad. El middleware queda de orquestador. Tests xUnit obligatorios.
2. **Una sola fuente para la huella de la cookie** — `Program.cs` y el middleware llaman a la MISMA
   función pura (mata A4).
3. **Declarar el contrato multiempresa en Swagger** con un `IOperationFilter` nuevo, sin tocar
   controllers.
4. **Habilitar los XML comments** ya escritos.
5. **Hacer ejecutable el "Try it out"** sin debilitar el gate de plataforma: la firma `X-Secret-Up`
   la inyecta el propio servidor en la UI (dev-only, detrás de la contraseña) y se agrega un
   endpoint de token para pruebas **fuera de `/api`**, también dev-only.

⛔ **No se toca**: `PlatformSecretMiddleware` (sigue exigiendo la firma a todo `/api/*`),
`ActiveCompanyMiddleware`, el cifrado del login del front, ni ningún contrato existente.

## 2) Archivos

**Nuevos**
- `backend/src/ZooSanMarino.Application/Calculos/SwaggerAccesoCalculos.cs`
- `backend/tests/ZooSanMarino.Application.Tests/SwaggerAccesoCalculosTests.cs`
- `backend/src/ZooSanMarino.API/Infrastructure/EmpresaActivaHeadersOperationFilter.cs`

**Modificados**
- `backend/src/ZooSanMarino.API/ZooSanMarino.API.csproj` — `GenerateDocumentationFile` + `NoWarn 1591`
- `backend/src/ZooSanMarino.API/Program.cs` — bloque 12 (XML comments, filtro de headers) y
  bloque 14.1-14.4 (login del gate, request interceptor, endpoint de token de pruebas)
- `backend/src/ZooSanMarino.API/Middleware/SwaggerPasswordMiddleware.cs` — delega en el cálculo puro
  + `HtmlEncode` del error (A1)
- `backend/src/ZooSanMarino.Infrastructure/Services/EncryptionService.cs` — `Encrypt` pasa a `public`
  (simétrico con `Decrypt`, que ya lo es)

**Sin cambios de BD.** No hay migración ni `.sql` nuevo.

## 3) Reglas de negocio

- **R1** El gate protege `/swagger*` y `/swagger-ui*`; exenta sólo `POST /swagger/login`. Fail-closed.
- **R2** La contraseña se compara en **tiempo fijo**. Sin fallback hardcodeado: si falta la config,
  el gate **niega** (hoy acepta la contraseña del código).
- **R3** La huella de sesión se calcula en **un solo lugar**.
- **R4** Inactividad > 6 min ⇒ sesión vencida (se conserva el comportamiento actual).
- **R5** Los 3 headers de empresa se declaran como parámetros **opcionales** en toda operación
  `/api/*`. Opcionales a propósito: sin ellos el backend cae al `CompanyId` del token — que es el
  comportamiento vigente y no se cambia.
- **R6** El interceptor de `X-Secret-Up` y el endpoint de token **sólo existen fuera de Production**
  y **sólo detrás de la contraseña de Swagger**. En prod no se monta ninguno de los dos.

## 4) Casos de prueba

**Unitarios (`SwaggerAccesoCalculosTests`)**
1. `/swagger`, `/swagger/index.html`, `/swagger/v1/swagger.json`, `/swagger-ui/dark.css` ⇒ protegida.
2. `/api/company`, `/health`, `/hc` ⇒ NO protegida (el gate no las toca).
3. `POST /swagger/login` ⇒ exenta; `GET /swagger/login` ⇒ protegida.
4. Contraseña correcta ⇒ true; incorrecta, vacía, `null`, con espacios ⇒ false.
5. Config ausente/vacía ⇒ **niega siempre**, incluso con la contraseña histórica.
6. Huella: misma contraseña + misma IP ⇒ igual; distinta IP ⇒ distinta; IP nula ⇒ no revienta.
7. Vigencia: 5 min 59 s ⇒ viva; 6 min 1 s ⇒ vencida; timestamp ilegible ⇒ vencida (fail-closed).

**Smoke HTTP (backend local :5002)**
8. `GET /swagger` sin cookie ⇒ 200 con el formulario (no el UI).
9. `GET /swagger/v1/swagger.json` sin cookie ⇒ formulario, no el JSON.
10. `POST /swagger/login` con contraseña mala ⇒ vuelve al formulario; con la buena ⇒ 302 + cookie.
11. Con cookie: `swagger.json` **válido**, y contiene los 3 headers de empresa y las descripciones
    de los XML comments.
12. Un `error=` con etiquetas HTML ⇒ el HTML sale **escapado** (A1 cerrado).
13. Try it out de un GET real ⇒ **200** (no 401 platform-secret) — B1 cerrado.
14. Mismo GET con `X-Active-Company` de otra empresa a la que el usuario NO pertenece ⇒ el alcance
    **no cambia** (fail-closed de `ActiveCompanyMiddleware` intacto).

**Gates del repo**
15. `dotnet build` 0 errores / sin advertencias nuevas · `dotnet test` verde.
