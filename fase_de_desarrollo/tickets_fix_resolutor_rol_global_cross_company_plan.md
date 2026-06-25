# Plan: Fix — Resolutor por ROL global/cross-company al crear ticket

## Síntoma (reportado por el usuario)
El usuario **admin** está configurado como **resolutor de todos los países** (vía un ROL, en el módulo
Roles → tab Tickets / "Perfil de Atención", con `pais_id = NULL` = global). Al entrar a **Ecuador**
(empresa distinta de la central) y abrir **Gestión → Mis solicitudes → Crear ticket**:

- El admin **no aparece** como resolutor asignable.
- El tipo de ticket cuyo único resolutor es ese rol global **puede no aparecer** (porque su lista de
  asignables queda vacía).
- Aunque se forzara, la **validación al crear** rechazaría el resolutor.

El usuario quiere: si existe un **rol que atiende tickets de todos los países**, debe poder **crear el
ticket y asignarlo a un usuario con ese rol**, sin importar que el país desplegado no tenga su propio
resolutor, y **sin tener que registrar al admin individualmente en cada país/empresa**.

## Causa raíz
Los resolutores ya son tratados como **globales por (tipo, país)** (ver
`tickets_fix_cross_company_resolutores.md`). Pero la ruta **resolutor-por-rol** todavía ata el join a la
empresa efectiva:

1. `TicketPerfilService.GetAsignablesInternalAsync` (dropdown de asignables) — paso 2 "por rol":
   - `TicketResolutorRoles` se consulta **sin** filtro de company (✅ encuentra el rol global).
   - Pero los portadores del rol se buscan en `UserRoles` con `ur.CompanyId == companyId` (❌). El admin
     tiene el `UserRole` en su empresa central, no en Ecuador → excluido. Si era el único resolutor del
     tipo, `asignables.Count == 0` → el tipo tampoco se ofrece.
   - Archivo/línea: `backend/src/ZooSanMarino.Infrastructure/Services/TicketPerfilService.cs:116`

2. `TicketService.CreateAsync` (validación del resolutor al crear) — fallback "por rol":
   - Mismo filtro `ur.CompanyId == companyId` (❌). Aun si el dropdown mostrara al admin, el create lo
     rechazaría con "El resolutor seleccionado no está disponible para este tipo y país."
   - Archivo/línea: `backend/src/ZooSanMarino.Infrastructure/Services/TicketService.cs:75-78`

## Diseño de la solución
Coherente con el principio ya adoptado ("resolutores globales identificados por (tipo, país)"): la
**pertenencia al rol resolutor no debe filtrarse por empresa**. El filtrado correcto ya lo hace
`TicketResolutorRoles` por `(tipo, pais_id == país OR NULL)`. Por tanto:

- **Fix A** — `GetAsignablesInternalAsync`: quitar `ur.CompanyId == companyId` del join a `UserRoles`.
  Cualquier usuario que porte el rol resolutor (en cualquier empresa) queda asignable. El `Distinct()`
  por `UserId` + el `HashSet addedUserIds` evitan duplicados cross-company.
- **Fix B** — `CreateAsync`: quitar `ur.CompanyId == companyId` de la validación por rol, espejando A.

**No** se toca `ReaplicarPlantillaRolAsync` (seeding explícito per-empresa, comportamiento intencional),
ni la ruta de resolutor directo por usuario (paso 1, que ya no filtra por company).

## Archivos a modificar
| Archivo | Cambio |
|---|---|
| `ZooSanMarino.Infrastructure/Services/TicketPerfilService.cs` | Fix A: quitar filtro de company del join a UserRoles (paso 2) |
| `ZooSanMarino.Infrastructure/Services/TicketService.cs` | Fix B: quitar filtro de company de la validación por rol en CreateAsync |

**Sin migración de BD** — solo lógica de negocio.

## Casos de prueba
1. Admin = resolutor por ROL **global** (`pais_id NULL`), `UserRole` solo en empresa central.
   Solicitante en **Ecuador** abre Crear ticket → el tipo aparece y el **admin figura como asignable**. ✓
2. Solicitante en Ecuador selecciona al admin y crea → la **validación pasa**, ticket creado. ✓
3. El admin ve el ticket en su bandeja **Asignados** (ya funciona: filtra por `AssignedToUserGuid`). ✓
4. Rol resolutor **específico de país** (`pais_id = X`): solo ofrece/valida para tickets de ese país. ✓
5. Usuario sin rol resolutor ni perfil → no aparece como asignable; create rechaza. ✓
6. `cd backend && dotnet build` 0 errores, sin nuevas advertencias.
