# Gate de Roles y Menús — cerrar la escalada de privilegios de `CanManageRoles`

> Plan de `fase_de_desarrollo/`. Tracker: bloque propio al final de `tracker_estado.md`.

## 1. El agujero, medido

`Program.cs:550-560` declara `CanManageMenus`, `CanManageUsers` y `CanManageRoles` como
`p => p.RequireAuthenticatedUser()` — token válido y nada más — con un `TODO(seguridad)` al lado.
`RolesController` cuelga de `CanManageRoles` **sus diez endpoints de roles y permisos**, incluidas las
cuatro escrituras que tocan `role_permissions`.

Probado en vivo el 5-sep-2026 (backend local `:5501`, base `sanmarinoapplocal:5433`) con el JWT de un
usuario real sin ningún permiso de administración (ALEX ALTAMIRANO, ItalcolEcuador, 14 permisos
efectivos, ninguno de gestión):

| Request | Resultado | Lectura |
|---|---|---|
| `GET /api/Roles` | **200** | devuelve todos los roles **con sus permisos** = el mapa de privilegios |
| `GET /api/Roles/permissions` | **200** | catálogo completo de keys |
| `POST /api/Roles/999999/permissions/assign` | **404, no 403** | la autorización **pasó**; lo frenó que el rol no existe |

Con un `roleId` real habría escrito. Y como los permisos se hornean como claims `permission` en el
token al login (`AuthService.cs:385`), el atacante se asigna la key, vuelve a loguearse y se salta
**cualquier** gate por permiso del sistema — incluidos los dos que se acaban de agregar:
`CargaMasivaPermisoFilter` (Migraciones Masivas) y `GestionUsuariosEscrituraFilter` (Gestión de
Usuarios). Es decir: los gates nuevos son de papel mientras esta puerta esté abierta.

La única barrera hoy es `RoleCompositeService.EnsurePermisosHabilitadosPorEmpresaAsync`
(`RoleCompositeService.cs:506-535`), que exige que la key esté habilitada en `company_permissions` de
**todas** las empresas del rol destino. Acota la población atacante a roles de una sola empresa
habilitada; **no cierra nada**. `Roles_AddPermissionsAsync` no valida quién llama: ni propiedad del
rol, ni empresa del llamante, ni super admin.

## 2. Auditoría anti-lockout (datos, no supuestos)

Medido sobre `sanmarinoapplocal` (copia de producción), 5-sep-2026. El módulo se localiza por
**route**, nunca por id (los ids difieren local↔prod), replicando
`20260825130000_SeedPermisoUsuariosGestionar`. La route real es **`/config/role-management`**
(menú id 14, label «Roles y permisos»).

**11 roles ven el módulo de Roles** → 15 usuarios: `Admin`(1), `Ecuador Administrador`(10),
`Colombia Administrativa`(12), `Lider Funcional`(15), `Admin Panama`(22), `Admin Demo`(23),
`Santa Reyes Administrador`(30), `Santa Reyes Implementador`(31), `Sistemas sanmarino`(34),
`sistemas panama`(35), `Soporte Sanmarino`(36).

🔴 **El hallazgo que decide el diseño de las lecturas.** `GET /api/Roles` **no lo consume solo la
pantalla de Roles**: `RoleService.getAll()` alimenta el desplegable de roles del modal de crear/editar
usuario (`user-management/components/modal-create-edit`) y la tabla del listado de usuarios
(`pages/tabla-lista-registro`). Y hay **3 roles que ven `/config/users` y NO ven
`/config/role-management`** — `Lider implementación - Regional Ecuador`(14), `Consulta`(20),
`Usuario pruebas`(24), 4 usuarios. Cerrar la lectura sólo con `roles.gestionar` les rompe el modal de
usuarios: dropdown vacío, sin poder asignar rol.

Verificado que la salida existe: **los 3 roles tienen `usuarios.gestionar`** y la key está habilitada
en `company_permissions` de **las 5 empresas**. Por eso la regla de lectura es
`roles.gestionar` **OR** `usuarios.gestionar` ⇒ **0 usuarios quedan afuera**.

`GET /api/Roles/menus/tree` y `GET /api/Menu/tree` (hoy `CanManageMenus`) los consumen dos pantallas:
`role-management` y `company-management` (`/config/companies`, sólo el rol `Admin`, que ya está en los
11). Nadie más lee el árbol global: el sidebar usa `menus/me`.

**Blast radius:**

| | Hoy | Después |
|---|---|---|
| Pueden escribir `role_permissions` (= escalar privilegios) | **58** (toda sesión) | **15** |
| Pueden leer el mapa de privilegios (`GET /api/Roles`) | **58** | **18** |
| Pueden leer el catálogo global de menús | **58** | **15** |
| Usuarios que pierden algo que hoy usan | — | **0** |

## 3. Las keys

Convención `modulo.accion` del repo. Dos keys:

- **`roles.gestionar`** — crear, editar y eliminar roles, y asignar/quitar/reemplazar sus permisos.
  Es la key que cierra la escalada.
- **`menus.gestionar`** — leer el **catálogo global** de menús (`GET /api/Menu/tree`,
  `GET /api/Roles/menus/tree`). Las escrituras del árbol ya están en `AdminAplicacion` desde el
  15-ago-2026; lo que quedaba abierto era la enumeración de todos los módulos de todos los países.

⛔ No se reusan las keys legacy `manage_roles`/`manage_menus` de `PermissionSeed.cs`: no las consulta
nadie, no respetan la convención y no existen en la base (medido: 45 keys, ninguna de las dos).

### Decisión sobre las LECTURAS — se cierran, con la OR que evita el lockout

A diferencia de Gestión de Usuarios (donde el listado quedó abierto a propósito), acá la lectura **es**
el mapa de privilegios del sistema entero: `GET /api/Roles` devuelve cada rol con sus permisos. Es el
insumo de reconocimiento del ataque que este trabajo cierra. Se cierra, con la OR de §2 para no romper
el modal de usuarios.

`GET /api/Permission` **queda abierto**, como está desde el 15-ago-2026 y por la razón escrita en
`CatalogoGlobalAutorizacionCalculos`: un usuario no admin lo necesita para asignar permisos a un rol.
Es el catálogo de *nombres* de key, no quién los tiene — mucho menos sensible que el mapa.

## 4. `CanManageUsers` NO se toca

La usan `RolesController.MenusForUser` (`menus/user/{userId}`) y `MenuController.GetForUser`
(`Menu/user/{userId}`), **ajenos** al módulo de roles. Verificado con grep: **ningún componente del
front los llama** hoy. Quedan como están —fuera del alcance de este trabajo— y se deja anotado el
hallazgo: siguen siendo «token válido y nada más», y devuelven el menú de otro usuario.

## 5. Implementación

**Cálculo puro** → `Application/Calculos/RolesAutorizacionCalculos.cs` (`static class`, sin EF, sin
`HttpContext`). Tres reglas + los mensajes:

| Regla | Verdadero si |
|---|---|
| `PuedeGestionarRoles` | super admin **o** rol de admin de aplicación **o** permiso `roles.gestionar` |
| `PuedeLeerRoles` | `PuedeGestionarRoles` **o** permiso `usuarios.gestionar` |
| `PuedeLeerCatalogoMenus` | super admin **o** rol de admin de aplicación **o** permiso `menus.gestionar` |

Los ejes «super admin» y «rol de admin de aplicación» se reusan de
`AdministracionEmpresasAutorizacionCalculos.PuedeAdministrarEmpresas` — no se reimplementa la
comparación de roles, que es **exacta** a propósito (en la base conviven `Admin Panama`, `Admin Demo`,
`Ecuador Administrador`…, administradores *de su empresa*; un `contains` les daría la llave global).
Es la válvula de seguridad: `PermisosEfectivosAsync` **no** le regala permisos al super admin
(`role_permissions ∩ company_permissions`, nada más), así que sin esta OR una empresa que deshabilite
la key en `company_permissions` dejaría al único super admin sin forma de arreglarlo desde la UI.

**Filtro de CLASE** → `API/Infrastructure/RolesGestionFilter.cs`, patrón de
`GestionUsuariosEscrituraFilterAttribute` + `CargaMasivaPermisoFilterAttribute`: en la clase, un
endpoint nuevo **nace cubierto** y hay que sacarlo explícitamente. Dos marcadores de excepción:

- `[RolesPermisoNoRequerido]` — el endpoint lo gatea otra política. Va en `menus/me` (menú propio),
  `menus/user/{id}` (`CanManageUsers`) y las tres escrituras de menús (`AdminAplicacion`).
- `[CatalogoMenusLectura]` — aplica la regla de menús en vez de la de roles. Va en `menus/tree` de
  `RolesController` y en `Menu/tree` de `MenuController` (ahí el filtro va a nivel de método: es su
  único endpoint afectado).

Default de la clase: `GET` ⇒ `PuedeLeerRoles`; cualquier otro método ⇒ `PuedeGestionarRoles`.
Responde **403** (no 401): la sesión es válida, falta la autorización.

**Program.cs**: las tres policies siguen existiendo (los atributos las nombran) pero el `TODO` se
reemplaza por la explicación de dónde vive el gate real y por qué no puede vivir en la policy —una
policy no distingue lectura de escritura, y la lectura necesita la OR con `usuarios.gestionar`.

**Migración data-only idempotente** `SeedPermisosRolesYMenusGestionar`, Designer clonado del
ModelSnapshot vigente, ModelSnapshot intacto:

1. `INSERT` de las 2 keys (`WHERE NOT EXISTS`).
2. `company_permissions` habilitadas **para cada empresa** — sin esto la key no viaja en el token
   aunque el rol la tenga (`PermisosEfectivosAsync` intersecta las tres tablas), y
   `SembrarCatalogoCompletoSiVaciaAsync` sólo siembra empresas vacías, no rellena keys nuevas.
   Es la trampa medida: `carga_masiva_pollo_engorde` está asignada a 13 usuarios y llega al token de 8.
3. **Anti-lockout** por route: `roles.gestionar` + `menus.gestionar` a todo rol con
   `/config/role-management`; `menus.gestionar` también a todo rol con `/config/companies`.
4. Y ambas al rol `Admin` (id 1), que puede no tener el menú cableado y igual tiene que poder.

`Down` borra sólo lo que crea.

## 6. Casos de prueba

**xUnit** (`tests/ZooSanMarino.Application.Tests/RolesAutorizacionCalculosTests.cs`) — gate de CI:

- Sin permisos, sin marca, sin rol ⇒ las tres reglas `false` (fail-closed).
- `null` / lista vacía / entradas en blanco ⇒ `false`.
- `roles.gestionar` ⇒ gestionar y leer `true`; catálogo de menús `false` (son keys independientes).
- `usuarios.gestionar` **sólo** ⇒ leer `true`, gestionar `false` ← el caso que evita el lockout del
  modal de usuarios; si esta prueba se cae, 4 usuarios pierden el dropdown de roles.
- `menus.gestionar` ⇒ catálogo `true`, roles `false`.
- Super admin sin ninguna key ⇒ las tres `true` (válvula).
- Rol `Admin`/`administrador` (exacto, case-insensitive, con espacios al borde) ⇒ las tres `true`.
- 🔴 `Admin Panama`, `Admin Demo`, `Ecuador Administrador`, `Santa Reyes Administrador`,
  `ADMINISTRADOR DE GRANJA` ⇒ **`false`** (no son admin de aplicación; es el bug que un `contains`
  introduciría).
- Comparación de keys **ordinal**: `Roles.Gestionar` ⇒ `false`.
- `EsLectura`: `GET` sí (y `get`/` GET `); `POST`/`PUT`/`DELETE`/`PATCH`/`null` no.

**Smoke HTTP** con la receta de la sesión (token minteado con los claims `permission` reales del
usuario + `X-Secret-Up` cifrado + fila en `sesiones_activas` por el gate B1), contra el backend local
en un puerto propio:

| Actor | Request | Esperado |
|---|---|---|
| Usuario sin permisos (ALEX ALTAMIRANO) | `GET /api/Roles` | **403** (hoy 200) |
| ídem | `GET /api/Roles/permissions` | **403** (hoy 200) |
| ídem | `POST /api/Roles/{real}/permissions/assign` | **403** (hoy 404 ⇒ pasaba) |
| ídem | `GET /api/Roles/menus/tree` | **403** |
| Usuario con `usuarios.gestionar` y sin `roles.gestionar` | `GET /api/Roles` | **200** ← anti-lockout |
| ídem | `POST …/permissions/assign` | **403** |
| Admin con `roles.gestionar` | `GET /api/Roles` y el `assign` | **200** |

⚠️ Los permisos se congelan en el token al login (60 min): otorgar o revocar no tiene efecto hasta el
re-login. Por eso el smoke mintea el token con las keys ya puestas en vez de esperar propagación.

## 7. Validación

`dotnet build` (artifacts propio, hay sesiones en paralelo) + `dotnet test`. Sin cambios de front ⇒ no
hace falta `yarn build`. Backend local levantado sólo para el smoke y apagado al terminar, con el
puerto libre verificado.
