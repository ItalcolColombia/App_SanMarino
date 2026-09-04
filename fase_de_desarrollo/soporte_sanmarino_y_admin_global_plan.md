# Soporte Sanmarino + separación del administrador global

> Pedido del usuario (4-sep-2026): *"tengo una persona de san marino que sera el encargado de
> soporte pero solo para empresa sanmarino [...] en las configuraciones solo debe mostrar opciones
> fuera de lo que ve el administrador global [...] o creo una empresa administrador [...] ya que
> tengo modulo o menus que no van a la causa en sanmarino"*.

## Veredicto de arquitectura: NO se crea una «empresa administrador»

El eje global **ya existe y vive fuera de `companies`**. Son dos, medidos sobre la copia local de
producción:

| Eje | Dónde vive | Qué habilita | Quién lo tiene |
|---|---|---|---|
| **Super admin** | `users.is_super_admin` | Pararse en cualquier empresa activa (`EmpresaActivaCalculos.PuedeUsarEmpresa`), marcar `roles.is_company_admin`, ver todas las granjas, revocar sesiones | 1 usuario |
| **Admin de aplicación** | Nombre de rol **exacto** `admin`/`administrador` (`CatalogoGlobalAutorizacionCalculos`) | Escribir catálogos globales (`permissions`, `menus`) y listar roles sin filtro de empresa (`RoleCompositeService.Roles_GetAllAsync`) | Solo el rol `Admin` (id 1) → el mismo usuario |

Los roles **no cuelgan de una empresa administradora**: cuelgan de `role_companies` y
`user_roles.company_id`. «Mudar el rol Admin a otra empresa» no le quitaría un solo privilegio,
porque el gate global se decide por el **nombre del rol**, no por su empresa. Una empresa fantasma
solo agregaría una fila en `companies` que arrastra `company_permissions` + `company_menus`, aparece
en el selector de empresa activa y en `GET /api/Company`, sin granjas ni lotes — y no resuelve nada
que la marca `is_super_admin` no resuelva ya.

## Los dos bloqueantes encontrados al validar

### B1 · El módulo Empresas no tiene ningún gate en el backend

`CompanyController` no declara **ni un `[Authorize]` ni una policy**: solo lo cubre la
`FallbackPolicy` (= token válido). Cualquier sesión autenticada —el futuro soporte de Sanmarino, o
cualquiera de Panamá— puede hoy por HTTP directo:

```
POST   /api/Company                       → crear empresas
PUT    /api/Company/{id}                  → editar cualquier empresa
DELETE /api/Company/{id}                  → borrar cualquier empresa
PUT    /api/Company/{id}/menus            → reasignarse módulos, o tocar los de otra empresa
PUT    /api/Company/{id}/menus/structure  → idem
PUT    /api/Company/{id}/permissions      → habilitar permisos de cualquier empresa
```

Es el mismo agujero que tenía `PermissionController` antes del 15-ago-2026. Esconder el ítem de menú
sin cerrar esto sería teatro: la policy `AdminAplicacion` existe (`Program.cs:570`) y **no se aplica
acá**.

### B2 · `fn_menu_usuario` no tiene bypass de super admin ⇒ limpiar Sanmarino lo deja sin salida

El menú del super admin se arma con `company_menus` de su empresa activa, igual que el de todos.
`/config/companies` y `/config/db-studio` están habilitados en **una sola empresa: Agroavicola
Sanmarino**. Quitárselos —que es justo lo que pide el punto «módulos que no van a la causa»— deja al
super admin **sin el módulo Empresas en toda la app**, y sin ruta de vuelta desde la UI: para
rehabilitarlo hay que entrar a Configuración → Empresas → Menús, que es el menú que se quitó.
Lockout real, salible solo por SQL.

El fail-open `D2` de la fn no cubre este caso: aplica a la empresa **sin ninguna fila** en
`company_menus`, y las cinco tienen.

---

## Enfoque arquitectónico

Cuatro frentes, en este orden. El orden importa: sin F1 el gate es cosmético, sin F2 la limpieza de
F4 provoca el lockout.

### F1 · Cerrar la escritura del módulo Empresas

- **Cálculo puro nuevo**: `Application/Calculos/AdministracionEmpresasAutorizacionCalculos.cs`
  (`static`, sin EF). Regla: puede administrar empresas quien sea **super admin** (claim
  `is_super_admin`) **o** tenga el rol de admin de aplicación (reusa
  `CatalogoGlobalAutorizacionCalculos.RolesAdminAplicacion`, comparación **exacta**). Fail-closed.
  - *Por qué suma el super admin y no reusa `AdminAplicacion` tal cual*: el eje correcto es el dato
    (`users.is_super_admin`), no el nombre del rol. Hoy coinciden en el mismo usuario, pero atar la
    administración de empresas al string `"Admin"` repite la deuda que la memoria ya señala.
- **Policy `AdminEmpresas`** en `Program.cs`, junto a `AdminAplicacion`, leyendo el claim
  `is_super_admin` (ya lo emite `AuthService`, lo consume `AuthController.cs:263`) + los claims de rol.
- **`[Authorize(Policy = "AdminEmpresas")]`** en las 6 escrituras de `CompanyController`.
- **Las LECTURAS quedan abiertas a propósito**, igual que en los catálogos globales:
  - `GET /api/Company` — lo usa el selector de empresa activa.
  - `GET /api/Company/global` — **lo usa el filtro de tickets** (`ticket-filtros.component.ts:101`).
    Cerrarlo rompe los filtros de tickets para todos.
  - `GET /api/Company/{id}`, `GET /{id}/menus`, `GET /{id}/permissions` — alimentan la pantalla y
    `ActiveCompanyConfigService`.
- **Anti-lockout verificado**: hoy el menú `/config/companies` lo tiene **un solo rol** (`Admin`) y
  **una sola empresa** (Sanmarino). El conjunto de gente que usa la pantalla es exactamente
  `{super admin}`. Nadie pierde un acceso que hoy tenga.

### F2 · Bypass de super admin en el menú

- `fn_menu_usuario`: el gate `company_menus` (CTE `empresa_filtra`) **no aplica al super admin**.
  Se mantienen intactos `role_menus`, `menus.is_active` y `menu_permissions` — el bypass es del gate
  de empresa, nada más. El super admin tiene rol `Admin`, cuyos `role_menus` ya incluyen
  `/config/companies` y `/config/db-studio`.
- Espejo actualizado en `backend/sql/fn_menu_usuario.sql` + **vehículo = migración**
  (`FnMenuUsuarioSuperAdmin`), con su `.Designer.cs` clonado — una migración sin Designer es
  invisible para EF y nunca se aplica.
- Especificación ejecutable en `Application/Calculos/MenuVisibilidadCalculos.cs` + tests xUnit
  nuevos: super admin ⇒ ve lo asignado aunque la empresa no lo habilite; no-super-admin ⇒ byte a
  byte idéntico a hoy.

### F3 · Rol «Soporte Sanmarino»

Migración **data-only idempotente** (lookups por `companies.name` / `menus.route` /
`permissions.key`, nunca por id):

- `roles` → `Soporte Sanmarino`, `is_company_admin = false`.
  ⚠️ **El nombre no puede ser `Admin` ni `Administrador`** (comparación exacta): esos dos strings
  son la llave de los catálogos globales y del listado de roles sin filtro.
- `role_companies` → Agroavicola Sanmarino.
- `role_menus` → Configuración/Usuarios, Configuración/Roles y permisos, Tickets/Mis solicitudes,
  Tickets/Bandeja de gestión, más lo operativo de Sanmarino para poder acompañar al usuario
  (Gestion de Granjas, Lote Postura, Lote Reproductora Postura, Seguimiento Diario Levante y
  Producción, Gestión de Inventario, Movimientos, Reportes).
  ⛔ **Sin** `/config/companies`, `/config/db-studio`, `/config/countries`, `/config/master-lists`
  — esos cuatro administran catálogos globales.
- `role_permissions` → `usuarios.gestionar`, `usuarios.revocar_sesion`, `tickets.crear`,
  `tickets.gestionar`.
  ⛔ **Sin `tickets.admin`**: es «todos los países», justo lo que se quiere evitar.
- El **usuario** no se crea acá (no hay identidad definida): se da de alta desde
  Configuración → Usuarios y se le asigna este rol con empresa Agroavicola Sanmarino.

**Contención que ya funciona sola, sin código nuevo:** menú = `role_menus` ∩ `company_menus(SM)` ∩
`menu_permissions`; permisos = `role_permissions` ∩ `company_permissions(SM)`; roles visibles
filtrados por `role_companies` de la empresa activa. Los tabs Permisos y Menús de
`/config/role-management` seguirán **viéndose** (las lecturas están abiertas a propósito) pero toda
escritura devolverá 403 por `AdminAplicacion`.

### F4 · Limpiar `company_menus` de Sanmarino

Sanmarino tiene **49 de 68** menús, la empresa más abierta. Migración data-only idempotente que
apaga los que el usuario apruebe. **Requiere OK explícito**: quitar un menú se lo saca a todos los
usuarios de Sanmarino, no solo al soporte.

Depende de F2: sin el bypass, apagar `/config/companies` o `/config/db-studio` provoca el lockout.

---

## Reglas de negocio

1. Un permiso/menú global no se decide por empresa ni por país: se decide por el **dato**
   (`is_super_admin`) o por el rol de aplicación. Nunca por `if (empresa == 'X')`.
2. Fail-closed en autorización; fail-open solo donde ya lo está y por una razón escrita
   (`company_menus` de una empresa sin configurar).
3. Refactor ≠ cambio de comportamiento: con super admin `false`, el menú debe salir **byte a byte
   igual** al de hoy.

## Casos de prueba

**Puros (xUnit, obligatorios antes de mergear):**
- `AdministracionEmpresasAutorizacionCalculosTests`: super admin sin rol admin ⇒ `true`; rol `Admin`
  sin super admin ⇒ `true`; `Admin Panama` / `Santa Reyes Administrador` / `ADMINISTRADOR DE GRANJA`
  ⇒ `false` (frontera del substring); `null`, vacío, blancos ⇒ `false`.
- `MenuVisibilidadCalculosTests` (nuevos): super admin ⇒ menú asignado aunque `company_menus` lo
  niegue; no-super-admin ⇒ idéntico al comportamiento previo; empresa sin filas ⇒ fail-open intacto.

**Invariante SQL (correr antes y después de F2/F4):**
`backend/sql/verificar_menu_usuario_paridad.sql` — para todos los pares (usuario, empresa):
*nuevo ⊆ viejo* salvo el super admin, y ningún no-super-admin gana un menú.

**Smoke:** `dotnet build` 0/0 · `dotnet test` verde · `GET /api/Company` 200 para un usuario común y
`PUT /api/Company/{id}/menus` **403** para el mismo · el super admin sigue viendo Empresas con
Sanmarino como empresa activa después de F4.

---

## Hallazgos colaterales (no se tocan en este plan, quedan registrados)

- **8 asignaciones de permiso huérfanas en Sanmarino.** `seguimiento_levante.validar` y
  `seguimiento_produccion.validar` están asignados a los roles `Admin`, `Colombia Administrativa` e
  `Implementador Sanmarino Colombia`, pero **no existen en `company_permissions` de Sanmarino** ⇒
  fail-closed los deja fuera de la sesión. Si en Sanmarino «no se puede validar el seguimiento», la
  causa está acá y no en el rol. También `vacunacion.plantillas.{ver,administrar}` en `Admin`.
- **`GET /api/Company/debug`** devuelve **todos los headers de la request** (incluido
  `Authorization`). Es un endpoint marcado «temporal» en su propio doc-comment. Candidato a borrar.
- **`CanManageMenus` / `CanManageUsers` / `CanManageRoles`** siguen siendo
  `RequireAuthenticatedUser()` con el `TODO(seguridad)` escrito al lado (`Program.cs:560`). No se
  endurecen acá: las usan controllers ajenos a este alcance.
