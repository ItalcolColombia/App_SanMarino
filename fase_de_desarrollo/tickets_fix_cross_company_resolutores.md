# Plan: Fix Tickets — Cross-Company Resolutores + Nivel + Bandeja Asignados

## Diagnóstico de los 3 bugs

### Bug 1 — Solo aparece "Soporte" al crear ticket
**Síntoma:** Admin Ecuador (`company_id=3`, permisos `tickets.crear+gestionar`) abre el formulario de creación → solo aparece "Soporte".

**Causa raíz (doble):**
1. `GetTiposPermitidosAsync` lee `nivel` desde `ticket_perfiles_usuario`. Si no hay registro, defaultea a `NORMAL`. `NORMAL` → solo `[SOPORTE, DUDAS]`. El usuario Admin Ecuador no tiene registro → nivel NORMAL → no ve DESARROLLO.
2. Para que "Desarrollo" aparezca en el dropdown, además del nivel, debe existir al menos 1 registro en `ticket_resolutores` con `tipo=DESARROLLO`, `company_id=3` y `(pais_id=2 OR pais_id IS NULL)`. moiesbbuga está registrado solo en company_id=1 → no aparece para Ecuador.

### Bug 2 — El resolutor no ve sus tickets asignados
**Síntoma:** moiesbbuga llama a `/tickets/asignados` → retorna 0 items. Los tickets creados por Admin Ecuador (company 3) deberían aparecer.

**Causa raíz:** `GetAsignadosAsync` llama a `GetEffectiveCompanyIdAsync()` → devuelve 1 (company de moiesbbuga). Luego `BaseQuery(1)` filtra `x.CompanyId == 1`. Los tickets de Ecuador tienen `company_id = 3` → filtrados y excluidos.

### Bug 3 — moiesbbuga no aparece como opción al asignar ticket de Ecuador
**Causa raíz:** `GetAsignablesInternalAsync` filtra `r.CompanyId == companyId` (= 3). moiesbbuga está registrado en `ticket_resolutores` con `company_id = 1` → no aparece para tickets de Ecuador. Mismo filtro en `CreateAsync` para validar el resolutor.

---

## Diseño de la solución

### Principio rector
El equipo de soporte/desarrollo es central (company=1, Sanmarino). Los tickets los crean las subsidiarias (Ecuador=3, etc.). El sistema actual trata a los resolutores como si fueran per-empresa; debe tratarlos como **globales** identificados solo por (tipo, pais).

### Fix 1 — `ICurrentUser` + `HttpCurrentUser`: exponer permisos JWT
Agregar `IReadOnlyList<string> Permissions` al interface. Leer el claim `permission` (array) del JWT. Esto permite que `TicketPerfilService` decida el nivel sin consultar la BD.

### Fix 2 — `TicketPerfilService.GetTiposPermitidosAsync`: nivel desde permisos
Si el usuario tiene `tickets.gestionar` o `tickets.admin` → nivel = `IMPLEMENTADOR` (puede crear todos los tipos: SOPORTE, DUDAS, DESARROLLO, REQUERIMIENTO). La consulta a `ticket_perfiles_usuario` sigue siendo el override explícito si existe.

### Fix 3 — `TicketPerfilService.GetAsignablesInternalAsync`: sin filtro de company
Eliminar `r.CompanyId == companyId` de la query de resolutores. Los resolutores son globales; el filtrado ya se hace por tipo y país. Un resolutor registrado en company=1 debe aparecer para tickets de company=3.

### Fix 4 — `TicketService.CreateAsync`: validación de resolutor sin company
Mismo cambio: quitar `r.CompanyId == companyId` de la validación del resolutor al crear el ticket.

### Fix 5 — `TicketService.GetAsignadosAsync`: sin filtro de company
Quitar `BaseQuery(companyId)`. Filtrar solo por `AssignedToUserGuid == userGuid` y `DeletedAt == null`. El resolutor ve TODOS sus tickets independientemente de qué empresa los creó.

---

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `ZooSanMarino.Application/Interfaces/ICurrentUser.cs` | + `IReadOnlyList<string> Permissions` |
| `ZooSanMarino.API/Infrastructure/HttpCurrentUser.cs` | Leer claim `permission` del JWT |
| `ZooSanMarino.Infrastructure/Services/TicketPerfilService.cs` | Fix 2 + Fix 3 |
| `ZooSanMarino.Infrastructure/Services/TicketService.cs` | Fix 4 + Fix 5 |

**NO se requiere migración de BD** — todos los cambios son de lógica de negocio en código.

---

## Casos de prueba

1. Admin Ecuador (company=3, pais=2) crea ticket → dropdown tipos debe mostrar: SOPORTE ✓ y DESARROLLO ✓
2. Admin Ecuador selecciona moiesbbuga como resolutor → validación debe pasar ✓
3. moiesbbuga llama a `/tickets/asignados` → ve los tickets de Ecuador asignados a su GUID ✓
4. Un usuario sin permiso `tickets.gestionar` → solo ve SOPORTE y DUDAS (nivel NORMAL) ✓
