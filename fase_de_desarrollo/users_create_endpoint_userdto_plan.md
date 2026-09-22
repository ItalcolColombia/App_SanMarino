# Plan — `POST /api/Users` debe responder `UserDto`, no `AuthResponseDto`

> **Revisión 21-sep-2026 (retomado sobre `main` = `e32c558`).** El plan original (abajo) se escribió el
> 16-sep sobre `982cd81`. Al retomarlo se re-verificó contra el código de hoy y cambian 3 cosas:
>
> 1. **El controller NO pasa a `[FromBody] CreateUserDto`: sigue recibiendo `RegisterDto`.** El plan
>    original no vio que `RegisterDto` trae la validación de entrada del alta (DataAnnotations, que
>    `[ApiController]` aplica antes de entrar al método): contraseña **≥ 8 con letra y número**, email
>    con formato + `MaxLength(255)` + `NoSqlInjection`, y `Required`/`MaxLength`/`NoSqlInjection` en
>    nombre, apellido, cédula, teléfono, ubicación y zona. `CreateUserDto` es un record sin atributos y
>    `CreateAsync` solo exige contraseña ≥ 6. Cambiar el tipo del body habría **debilitado la
>    validación** del alta. Solución: el controller conserva `RegisterDto` (mismo contrato de ENTRADA,
>    mismos 400 de validación) y lo convierte con una función pura
>    `AltaUsuarioCalculos.DesdeRegistro(RegisterDto) → CreateUserDto` (Application, con tests xUnit);
>    solo cambia la SALIDA, que es lo decidido.
> 2. **La siembra de perfiles de tickets por rol ya no existe.** `42dc6ec` (TICKETS-ABRIR-VS-ATENDER,
>    19-sep) sacó `ITicketPerfilService`/`SeedTicketPerfilesAsync` de `UserService`: la plantilla del rol
>    se lee en vivo. `RegisterAsync` tampoco sembraba. La regla de negocio «Siembra de perfiles de
>    resolutor» de abajo queda **sin efecto** (ni el camino viejo ni el nuevo la hacen).
> 3. **Diferencias `RegisterAsync` vs `CreateAsync` re-medidas sobre el código de hoy** (lo que el
>    swap cambia, además de la respuesta):
>    - Exige ≥ 1 empresa y ≥ 1 rol (`RegisterAsync` aceptaba vacíos). Sin efecto visible: el form del
>      modal ya los exige (`requiredArray` en `companyIds`/`roleIds`) y es el **único** llamador de
>      `POST /api/Users` (front, sin uso en la app móvil).
>    - Valida que empresas/roles existan (antes: violación de FK → 400 genérico con el mensaje de EF).
>    - Guarda email y nombres con `Trim()` (antes: tal cual llegaban).
>    - Transacción explícita (`BeginTransactionAsync`): segura, `EnableRetryOnFailure` sigue apagado
>      (`Program.cs:153`).
>
> El correo de bienvenida solo se ENCOLA (`EmailService.SendWelcomeEmailAsync` →
> `email_queue`); en dev `Email:Queue:Enabled=false` ⇒ el smoke no manda correos reales. El parche de
> consumo del front (`120a646`, mapeo `userId`/`username` → `id`/`email`) se conserva como tolerancia
> para la ventana del deploy (front nuevo con back viejo); con el back nuevo el `??` toma `id`/`email`.

## Requerimiento (16-sep-2026)

Al probar end-to-end el modal de alta de usuario (`modal-create-edit.component.ts`) contra el backend
local, `POST /api/Users` responde con la forma de `AuthResponseDto` (`userId`, `username`, `token`,
`platformKey`, `roles`, `permisos`, `menu`...) en vez de `UserDto`/`UserListItem` (`id`, `email`,
`firstName`, `isActive`...) que usan `GET /api/Users`, `GET /api/Users/{id}` y `PUT /api/Users/{id}`.
Decisión del usuario (no un default mío): ir al fix de raíz, no a un parche aditivo sobre
`AuthResponseDto`.

## Diagnóstico

`UsersController.Create` ([UsersController.cs:38](../backend/src/ZooSanMarino.API/Controllers/UsersController.cs))
no llama a `_userService.CreateAsync()` (existe, ya devuelve `UserDto`, cero llamadores en todo el
backend — código muerto). Llama a `_auth.RegisterAsync(dto)`
([AuthService.cs:62](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs)), el mismo
método que usan el login y `/Auth/register`. Por eso el shape es de auth: literalmente es la respuesta
de un login, no la de un alta de recurso.

Efecto colateral de reusar `RegisterAsync` para esto (no es solo un tema de forma del DTO):
`GenerateResponseAsync` ([AuthService.cs:320](../backend/src/ZooSanMarino.Infrastructure/Services/AuthService.cs))
emite un JWT válido para la cuenta recién creada y registra esa sesión en `sesiones_activas` con el
IP/device/user-agent de quien está creando el usuario (el `HttpContext` de esa request es el del
ADMIN, no el del usuario nuevo) — una sesión "activa" fantasma para alguien que nunca inició sesión,
más un correo de bienvenida disparado desde ahí. Nada de esto lo explota el frontend hoy (el guardado
de token solo ocurre dentro de `AuthService.login()`, nunca por un interceptor genérico), pero es
comportamiento de más para lo que conceptualmente es "crear un recurso".

`AuthService` sigue siendo, después de este cambio, la **única fábrica de tokens** del backend
(invariante ya verificada en `tracker_estado.md` A2/V39.13) — este fix no la toca; solo deja de pedirle
un login a algo que no lo necesita.

## Enfoque arquitectónico

`UsersController.Create` pasa a usar `_userService.CreateAsync(CreateUserDto)` (Application), el
método que ya existe y ya hace la misma transacción (Login+User+UserCompany+UserRole+UserFarm) con
validación de FKs. Dos huecos que hay que cerrar para que el swap no sea un cambio de comportamiento
además del que se decidió a propósito:

1. **`UserService.CreateAsync` nunca envía el correo de bienvenida** (no tiene `IEmailService`) y
   **hardcodea `IsEmailLogin = true`** (`UserService.cs:102`) — `RegisterAsync` sí manda el correo
   (salvo `IsPlatformUser`) y sí distingue `IsEmailLogin = !dto.IsPlatformUser`. Sin portar esto, cada
   alta perdería el correo de bienvenida y marcaría mal a los usuarios de plataforma (`@zootecnico.com`).
   `CreateUserDto` (Application) tampoco tiene `IsPlatformUser` — hay que agregarlo.
2. **`UserDto` (record) no tiene `Email`** — ni siquiera `GetByIdAsync`/`UpdateAsync` lo devuelven hoy
   (gap preexistente; `modal-create-edit.component.ts:273` ya lo esquiva leyendo el email de la fila de
   la lista en vez de la respuesta del GET). Si Create devuelve `UserDto` con `Email` pero
   GetById/Update siguen sin él, el shape es "el mismo tipo" pero con datos inconsistentes — no cumple
   lo que se pidió ("mismo shape que GET/PUT"). Se cierra parejo en los tres.
3. El modal ya lee `result.emailQueueId`/`result.emailSent` de la respuesta de create para la UX de
   "correo en cola" (`modal-create-edit.component.ts:475-483`). Por eso `UserDto` suma `EmailSent`/
   `EmailQueueId` (nullable, solo se completan en Create; quedan `null` en GetById/GetAll/Update, igual
   que `LastLoginAt` es null para alguien recién creado).

**Frontend: no requiere cambios.** Ya reviewed — `User`/`UserDto` (interfaces en
`user.service.ts`) ya declaran `id`, `email`, `emailSent?`, `emailQueueId?`; es el backend el que no
cumplía su propio contrato ya modelado en el front. Único llamador de `UserService.create()` es
`modal-create-edit.component.ts:468`, y no depende de ningún campo de `AuthResponseDto` (`token`,
`platformKey`, `menu`, `permisos`) — se comprobó por grep que nada en el front lee esos campos de la
respuesta de `POST /api/Users`.

`RegisterAsync` y `/Auth/register`/`/Auth/login` **no se tocan** — siguen siendo el único camino que
emite JWT/sesión, sin cambios.

## Archivos a crear/modificar

### Backend

- `ZooSanMarino.Application/DTOs/UserDto.cs`
  - `CreateUserDto`: agregar `bool IsPlatformUser = false` (parámetro final, no rompe la firma).
  - `UserDto`: agregar `string? Email = null`, `bool? EmailSent = null`, `int? EmailQueueId = null`
    (parámetros finales, no rompen los 3 call-sites posicionales existentes en `UserService.cs`).
- `ZooSanMarino.Infrastructure/Services/UserService.cs`
  - Constructor: inyectar `IEmailService _emailService` (ya registrado `AddScoped` en `Program.cs`,
    mismo lifetime que `IUserService`).
  - `CreateAsync`: `IsEmailLogin = !dto.IsPlatformUser`; después del commit + seed de perfiles de
    ticket, replicar el bloque best-effort de `RegisterAsync` (`AuthService.cs:112-138`) que manda
    `SendWelcomeEmailAsync` solo si `!dto.IsPlatformUser`, capturando `emailQueueId`/`emailQueued` sin
    hacer fallar el alta si el correo falla; completar `Email`/`EmailSent`/`EmailQueueId` en el
    `UserDto` de retorno.
  - `GetAllAsync`, `GetByIdAsync`: agregar `.Include(u => u.UserLogins).ThenInclude(ul => ul.Login)` y
    proyectar `Email` igual que ya hace `GetUsersAsync` (`UserService.cs:294`).
  - `UpdateAsync`: sumar una query chica de `Email` junto a las de `rolesNames`/`companyIds`/`farms` en
    la "proyección final" (mismo patrón ya usado ahí), sin tocar el resto de la lógica de sync.
- `ZooSanMarino.API/Controllers/UsersController.cs`
  - `Create`: `[FromBody] RegisterDto dto` → `[FromBody] CreateUserDto dto`; body pasa a
    `await _userService.CreateAsync(dto)`; `[ProducesResponseType(typeof(AuthResponseDto), 201)]` →
    `typeof(UserDto)`. `_auth` queda intacto (lo siguen usando `ChangePassword`/`AdminResetPassword`).

### Frontend

Ninguno (contrato TS ya correcto — ver Enfoque arquitectónico).

## Cambios de BD/SQL

Ninguno. Sin migraciones: no cambia ninguna columna/tabla, solo la forma del DTO de respuesta y qué
método de aplicación arma la fila (mismas tablas: `users`, `logins`, `user_logins`, `user_companies`,
`user_roles`, `user_farms`).

## Reglas de negocio

**Se preservan** (mismo resultado que hoy con `RegisterAsync`, verificado línea por línea):
- Emails duplicados rechazados (`InvalidOperationException`, mismo criterio `Logins.AnyAsync`).
- Empresas/roles inexistentes rechazados (ValidateCompaniesAsync/RolesAsync, ya existían en
  `CreateAsync` y son más estrictos que `RegisterAsync`, que no valida FKs — mejora, no regresión).
- Correo de bienvenida: se manda solo si `!IsPlatformUser`, best-effort (no revienta el alta si falla).
- `IsEmailLogin` refleja `!IsPlatformUser`.
- Siembra de perfiles de resolutor de tickets por rol asignado (ya la hacía `CreateAsync`, best-effort).
- Asignación de granjas (`FarmIds`) — `RegisterDto` no la tenía; `CreateUserDto` sí. El front hoy no
  manda `farmIds` en el alta (se asignan después vía `POST /api/Users/{id}/farms`), así que en la
  práctica sigue entrando como `[]`/`null` → sin cambio observable.

**Cambia a propósito** (la razón de este plan):
- La respuesta ya no incluye `token`/`platformKey`/`menu`/`permisos`/`companyPaises`/`isSuperAdmin`.
- Ya no se emite JWT ni se registra sesión en `sesiones_activas` para el usuario recién creado — el
  alta deja de "loguear" a nadie. Efecto: la fila fantasma de sesión (atribuida al dispositivo del
  admin) deja de aparecer.
- La respuesta ahora sí incluye `id`, `email`, `emailSent`, `emailQueueId` (lo que el modal necesita
  para quedarse en modo edición tras crear).

## Casos de prueba

No hay proyecto de integración con EF/WebApplicationFactory en el repo (`backend/tests/` solo tiene
`ZooSanMarino.Application.Tests` —referencia únicamente `Application`, no puede tocar `UserService`
en `Infrastructure`— y `ZooSanMarino.Domain.Tests`). No se monta infraestructura nueva de testing para
este fix puntual (sería un proyecto aparte, fuera de alcance); se sigue el patrón ya usado en el
tracker para trabajo reciente sobre servicios EF: **smoke HTTP real contra backend+BD locales**
(`make up`), matando el backend antes/después (regla dura de CLAUDE.md §Ciclo de vida del backend
local).

- Build: `dotnet build` 0 errores, sin warnings nuevos.
- `dotnet test`: la suite de `Application.Tests` sigue en verde (no debería tocarla este cambio, pero
  confirma que `CreateUserDto`/`UserDto` con los campos nuevos no rompen nada que los use por posición).
- Smoke (backend+BD local, usuario de prueba, limpiado al final):
  1. `POST /api/Users` con payload real (persona + empresa + rol reales de la BD local) → 201, body
     `{ id, email, firstName, surName, isActive: true, roles: [...], companyIds: [...], ... }`, **sin**
     `token`/`platformKey`/`menu`.
  2. `GET /api/Users/{id}` con el `id` devuelto → mismo `email` que en el create (ya no null).
  3. `GET /api/Users` (lista) → el usuario nuevo aparece con el mismo email.
  4. `PUT /api/Users/{id}` (cambiar un campo cualquiera) → respuesta sigue trayendo `email`.
  5. Crear con `isPlatformUser: true` → `IsEmailLogin` queda en `false` en BD, no se encola correo
     (`emailQueueId: null`), el login sintético `@zootecnico.com` funciona igual que antes.
  6. Verificar en `sesiones_activas` que el alta del paso 1 **no** insertó ninguna fila (antes sí lo
     hacía) — evidencia directa de que se cortó el login fantasma.
  7. Repetir el alta con el mismo email → 400 con el mensaje de "correo ya registrado" (sigue igual).
  8. `/Auth/login` y `/Auth/register` sin tocar: smoke rápido de que un login normal sigue devolviendo
     `token` (no se rompió `AuthResponseDto` para sus consumidores reales).

## Riesgos identificados (documentados, sin acción requerida en este cambio)

- `IUserService.CreateAsync` pasa de "código muerto" a código vivo en el camino de escritura más usado
  del módulo de usuarios — cualquier bug latente en su transacción (nunca ejercitada en producción)
  queda expuesto recién ahora. Mitigado por el smoke manual punto por punto de arriba.
- El mensaje de "correo duplicado" cambia de texto exacto (`"...registrado."` con punto final en
  `CreateAsync` vs sin punto en `RegisterAsync`) — cosmético, visible en un toast de error.
- Sin integración automatizada, la cobertura de este cambio depende del smoke manual (mismo nivel de
  garantía que el resto de los cambios recientes sobre servicios EF en este repo, ver
  `tracker_estado.md` V2).
- `RegisterDto.FarmIds` no existía; si en el futuro el front empieza a mandar `farmIds` en el alta,
  ahora sí se van a asignar (antes se ignoraban silenciosamente). No aplica hoy: el front no manda ese
  campo.

## Validación (antes de mergear)

```bash
cd backend
dotnet build
dotnet test
```

Smoke local (matar cualquier backend previo, levantar solo para probar, apagar al final — regla dura
del repo):
```bash
netstat -ano | grep LISTENING | grep ":5002" | awk '{print $NF}' | xargs -r -I{} taskkill //PID {} //F
make up
# ... casos de prueba de arriba con curl/Postman/Browser pane ...
make down
netstat -ano | grep LISTENING | grep ":5002"   # debe salir vacío
```

## Resultado (21-sep-2026)

Implementado segun la revision del principio. Validacion: `dotnet build` 0/0; `dotnet test`
Application.Tests 4.430/4.430 (11 nuevos en `AltaUsuarioCalculosTests`) y Domain.Tests 1/1; smoke HTTP
contra el back nuevo y la BD local 17/17 (detalle en el bloque USERS-CREATE-USERDTO de `tracker_estado.md`).
El caso 8 (login) se probo con el body cifrado como lo cifra el front: el usuario creado por el camino
nuevo inicia sesion y recibe token.
