# B10 — el Super Admin deja de ser un correo en el código y pasa a ser un dato

**Pendiente que ataca:** `tracker_estado.md` → bloque *«PWA — validación de estado y brecha real para
salir a producción»*, §6 (deuda que viaja con el deploy), y la tabla del plan
[`pwa_offline_first_plan.md`](pwa_offline_first_plan.md) línea 178:

> **B10** — Mover el super admin hardcodeado por email (`ActiveCompanyMiddleware.cs:52` y `:116`) a
> datos, reusando el patrón `roles.is_company_admin`. **Atraviesa el aislamiento multiempresa y no se
> puede revocar sin deploy.**

**Fecha:** 2026-08-17 · Bloque propio — no tocar desde otras sesiones.

---

## 1. Lo que realmente hay (medido, no son 2 sitios)

`grep 'moiesbbuga@gmail.com'` sobre `backend/src` (sin `bin/` ni migraciones) da **14 sitios de
autorización** en 13 archivos, cada uno reimplementando la misma regla a mano:

| Archivo | Línea | Qué decide |
|---|---:|---|
| `API/Infrastructure/ActiveCompanyMiddleware.cs` | 52 | usar **cualquier** empresa vía `X-Active-Company-Id` |
| `API/Infrastructure/ActiveCompanyMiddleware.cs` | 116 | idem, vía `X-Active-Company` (por nombre) |
| `API/Controllers/AuthController.cs` | 272 | `isSuperAdmin` en la respuesta del login |
| `Infrastructure/Services/AuthService.cs` | 427 | `IsSuperAdmin` del `AuthResponseDto` |
| `Infrastructure/Services/CompanyService/Funciones/CompanyService.Permisos.cs` | 19 | ver/editar empresas |
| `Infrastructure/Services/DbStudio/DbStudioAuthorization.cs` | 18 | admin de DB Studio |
| `Infrastructure/Services/FarmService.cs` | 532 | ver todas las granjas |
| `Infrastructure/Services/GalponService.cs` | 124 | ver todos los galpones |
| `Infrastructure/Services/NucleoService.cs` | 127 | ver todos los núcleos |
| `Infrastructure/Services/LotePosturaLevanteService.cs` | 111 | alcance de levante |
| `Infrastructure/Services/LotePosturaProduccionService.cs` | 105 | alcance de producción |
| `Infrastructure/Services/RoleCompositeService.cs` | 296 | marcar un rol como Admin de Empresa |
| `Infrastructure/Services/UserFarmScopeService.cs` | 51 | administrar el alcance usuario-granja |
| `Infrastructure/Services/UserPermissionService.cs` | 302 | países, granjas y usuarios visibles |

Y no son 14 copias iguales: conviven **cuatro** formas de comparar
(`?.ToLower() ==`, `?.ToLowerInvariant() ==`, `.Trim().Equals(..., OrdinalIgnoreCase)`,
`string.Equals(..., OrdinalIgnoreCase)`). Es el mismo problema que CLAUDE.md ya nombra para los
números —*«una sola fórmula por número»*— aplicado a la autorización más poderosa del sistema.

**Consecuencias hoy:** conceder o quitar el super admin exige **editar código y desplegar**; y la
regla que atraviesa el aislamiento multiempresa no se puede auditar desde la base.

## 2. Diseño

### 2.1 El dato

Columna **`users.is_super_admin boolean NOT NULL DEFAULT false`** — tipada, nombrada por el
comportamiento, con default neutro. Es el mismo patrón que `roles.is_company_admin`, sobre la tabla
que corresponde: hoy el super admin es **un usuario**, no un rol.

> ⚠️ **Por qué NO va en `roles`.** El usuario de hoy tiene el rol `Admin` (id 1) y **ese rol lo
> tienen 2 usuarios**. Poner la marca en el rol le daría super admin al segundo ⇒ sería **ampliar**
> el acceso, no moverlo. Un refactor de autorización no puede regalar permisos.

### 2.2 Equivalencia día uno

La migración siembra `is_super_admin = true` **exactamente para quien hoy lo tiene por código**,
buscándolo por su correo de login (idempotente, `IS DISTINCT FROM`, la misma convención de los seeds
del repo). Así el comportamiento del primer arranque es **idéntico**, y a partir de ahí se concede o
revoca **desde la base, sin deploy**.

### 2.3 Una sola regla

- `Application/Calculos/SuperAdminCalculos.cs` (**puro**, sin EF): la decisión y su documentación.
  **Fail-closed**: `null` ⇒ `false`. Nunca se infiere el super admin de un correo, un nombre ni un rol.
- Un único lector en Infrastructure que resuelve la marca por `Guid` de usuario. Los 12 sitios que
  hoy consultan el correo pasan a llamarlo: misma cantidad de viajes a la base que antes (hoy cada uno
  ya hacía su propio `SELECT` del email), pero **una sola implementación**.
- Los 2 sitios del login (`AuthService`, `AuthController`), que ya tienen el usuario a mano, leen la
  marca del propio usuario.

### 2.4 Lo que NO cambia

- El claim/campo `isSuperAdmin` que viaja al front **se conserva con el mismo nombre y semántica**.
- Ningún permiso, rol, menú ni alcance cambia. El único que hoy es super admin lo sigue siendo.

## 3. Casos de prueba

**Cálculo puro (xUnit):**
- T1 `true` ⇒ es super admin.
- T2 `false` ⇒ no lo es.
- T3 `null` (usuario inexistente / sin fila) ⇒ **false**, fail-closed.
- T4 la regla NO mira correo, nombre ni rol: dos usuarios con la misma marca dan el mismo resultado.

**Datos (verificación en BD):**
- T5 tras la migración hay **exactamente un** usuario con `is_super_admin = true`, y es el mismo que
  hoy decide el código.
- T6 la migración es idempotente: correrla dos veces no cambia nada.

**Integración / smoke:**
- T7 con el super admin: `X-Active-Company-Id` de una empresa a la que **no** pertenece ⇒ se acepta
  (igual que hoy).
- T8 con un usuario normal: la misma cabecera ⇒ **no** se acepta (igual que hoy).

## 4. Riesgos y cómo se contienen

1. **Quedarse sin super admin** si el seed no encuentra al usuario ⇒ la migración deja `NOTICE` (misma
   convención de los seeds de ItalJira) y se verifica con T5 **en local antes de mergear**.
2. **Ampliar el acceso sin querer** ⇒ por eso el flag es por usuario y se siembra por correo exacto;
   T5 exige *exactamente uno*.
3. **Fail-closed en todos los caminos**: sin fila, sin Guid o sin sesión ⇒ `false`.

## 5. Fuera de alcance

- **No** se toca `TicketService.EsSuperAdmin()` ni `TicketTareaService`: ésos ya son por **permiso**
  (`tickets.admin`), no por correo — no son parte de B10.
- **No** se toca B1 (revocación de sesión): arrastra una decisión de producto (la vigencia de la
  sesión offline) y es otro pendiente.
- **No** se crea pantalla para administrar la marca; se concede/revoca por dato. La UI, si se quiere,
  es otro pedido.

---

## 6. Resultado (17-ago-2026)

**Entregado.** `grep 'moiesbbuga@gmail.com'` sobre `backend/src` (sin `bin/` ni migraciones) da **0**.

- **T1-T4 ✔** cálculo puro con 5 tests xUnit; `dotnet test` **2.809 + 1 en verde** (+7).
- **T5 ✔** tras la migración hay **exactamente 1 usuario marcado de 56**, y es el mismo que decidía el
  código (`moiesbbuga@gmail.com`).
- **T6 ✔** idempotente: la segunda corrida actualiza **0 filas**.
- **T7 ✔** super admin con `X-Active-Company-Id: 3` (empresa a la que **no** pertenece) ⇒ empresa
  efectiva **3**: la pantalla de lotes muestra 0 (Ecuador no tiene lotes de postura) en vez de los de
  Sanmarino.
- **T8 ✔** usuario normal con la **misma** cabecera ⇒ cae a la empresa del token (**1**): ve sus 2
  lotes. El gate se comporta igual que antes, ahora por dato.
- `dotnet build` 0 errores (9 advertencias preexistentes).

> Las pruebas T7/T8 se hicieron con el **nombre** de empresa vacío y sólo el id, para que la única
> decisión en juego fuera la del middleware. Ver el hallazgo de abajo: con el nombre presente, hay
> otro camino que ni siquiera lo consulta.

## 7. 🔴 Hallazgo aparte (no es de esta entrega, queda medido)

El primer intento del smoke dio un resultado raro —el usuario normal veía 0 lotes en vez de sus 2— y
la causa **no era este cambio**:

`LoteService.GetEffectiveCompanyIdAsync()` resuelve la empresa **desde el nombre crudo del header**
`X-Active-Company` (`ICurrentUser.ActiveCompanyName` sale directo de la cabecera en `HttpCurrentUser`,
**sin validar pertenencia**) y sólo cae a `_current.CompanyId` si viene vacío. Es decir: mandando el
nombre de una empresa ajena, un usuario **lee** con el alcance de esa empresa, sin pasar por el
middleware.

En la prueba no se filtró nada porque Ecuador no tiene lotes de postura, **pero el camino existe**.
`GetCompanyIdByNameAsync` se usa en **42 archivos** (`ClienteService`, `CuadreAlimentoEngordeService`,
`InventarioGestionService`, `FarmService`, `GalponService`, …); habría que revisarlos uno por uno.

**No se arregla acá a propósito**: son 42 archivos, toca el alcance multiempresa de módulos de 4
países y necesita su propio plan, su gate y un smoke por empresa. Meterlo dentro de B10 sería cambiar
el alcance a mitad de camino.
