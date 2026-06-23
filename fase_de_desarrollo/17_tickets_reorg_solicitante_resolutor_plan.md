# 17 — Tickets: reorganizar "quién crea" vs "quién recibe" (solicitante vs resolutor)

**Fecha:** 2026-06-21
**Estado:** 🟡 PLAN aprobado (decisiones tomadas) — en implementación
**Base:** continúa [16](16_tickets_asignacion_perfiles_niveles_plan.md) y el módulo de tickets en producción.
**Migración de BD:** ❌ NINGUNA. Se reutilizan `ticket_resolutor_roles` y `ticket_resolutores` (ya existen).

---

## 1. Problema (diagnóstico)

El módulo mezcla y/o deja muertos dos conceptos independientes:

1. **Editar Usuario → "Perfil de Atención"** mete en una sola tarjeta dos cosas opuestas:
   - **Nivel de solicitante** (Normal/Implementador) = **quién CREA**.
   - **Tipos que atiende como resolutor** = **quién RECIBE**.
2. **Editar Rol → "Tipos de ticket que atiende este rol"** hoy **no tiene efecto**:
   - El enrutamiento/asignación lee **solo** `ticket_resolutores` (por usuario) — ver `TicketPerfilService.GetAsignablesInternalAsync`.
   - La plantilla del rol (`ticket_resolutor_roles`) solo se aplica vía `SeedPerfilDesdeRolAsync`, que **nunca se invoca** (ni al asignar rol, ni desde la UI).
3. No se explica la precedencia (rol = plantilla, usuario = real/override).

## 2. Decisiones (confirmadas 2026-06-21)

- **D1 — Quién RECIBE (resolutor):** **Rol = plantilla auto-aplicada.** Al asignar un rol a un usuario se **siembran** sus resolutores desde la plantilla del rol (idempotente, sin pisar overrides). El usuario puede ajustarlos. Se reutiliza el motor actual (enrutamiento por `ticket_resolutores`).
- **D2 — Quién CREA (nivel):** **Solo por persona** (como hoy). Solo se separa visualmente en su propia tarjeta. (No se agrega nivel por rol.)
- **D3 — Cambios posteriores a la plantilla del rol:** botón **"Aplicar a los usuarios de este rol"** (re-seed idempotente) para que editar la plantilla tenga efecto sobre usuarios existentes.

## 3. Modelo objetivo (claro)

| Eje | Pregunta | Dónde se configura | Tabla |
|---|---|---|---|
| **Solicitante** | ¿Qué puede CREAR? | **Usuario** → tarjeta "① Creación" (Nivel) | `ticket_perfiles_usuario.nivel` |
| **Resolutor** | ¿Qué RECIBE? | **Rol** = plantilla (auto-aplicada) + **Usuario** = real/override | `ticket_resolutor_roles` (plantilla) → `ticket_resolutores` (real) |

## 4. Cambios — Backend (.NET, sin migración)

- **`ITicketPerfilService`**: agregar overload `SeedPerfilDesdeRolAsync(Guid userId, int roleId, int companyId, CancellationToken ct)` (siembra para una empresa concreta). Agregar `ReaplicarPlantillaRolAsync(int roleId, CancellationToken ct)` (siembra a todos los usuarios con ese rol en la empresa activa).
- **`TicketPerfilService`**: implementar overload por empresa; el método existente (empresa activa) delega en él. Implementar `ReaplicarPlantillaRolAsync` (loop usuarios con `UserRole(roleId, companyId)` → seed por usuario, idempotente, no desactiva overrides).
- **`UserService`**: inyectar `ITicketPerfilService`. En `CreateAsync` y `UpdateAsync`, **después del commit**, sembrar por cada par `(companyId, roleId)` recién agregado, dentro de try/catch (un fallo de seed **no** rompe el alta/edición del usuario; patrón del proyecto = encolado de correo).
- **`TicketPerfilesController`**: nuevo `POST /api/ticket-perfiles/rol/{roleId:int}/reaplicar` → `ReaplicarPlantillaRolAsync`.

## 5. Cambios — Frontend (Angular, refactor + copy, sin cambio de contrato)

- **`ticket-perfil-editor` (html)**: separar en tarjetas claramente rotuladas y mode-aware:
  - `modo='usuario'`: **① Creación de solicitudes** (Nivel) + **② Atención de solicitudes — Resolutor** (tipos+país), con nota "pre-cargado desde tu rol; podés ajustarlo".
  - `modo='rol'`: solo **Plantilla de atención del rol (resolutor)** + nota "se aplica a los usuarios de este rol" + botón **"Aplicar a los usuarios de este rol"**.
- **`ticket-perfil-editor` (ts)**: método `reaplicarPlantilla()` (solo modo rol) con confirm + toast.
- **`ticket-perfil.service.ts`**: `reaplicarPlantillaRol(roleId)`.
- **`role-management.component.html`**: alinear el `<h4>` externo con el nuevo copy (evitar doble título).
- **`modal-create-edit.component.html`** (Usuario): intro corta opcional en el tab.

## 6. Preservación de comportamiento

- Contratos HTTP existentes intactos (PUT usuario = nivel+resolutores; PUT rol = resolutores). Solo se **agrega** el endpoint `reaplicar`.
- El auto-seed es **nuevo comportamiento intencional** (D1). Idempotente y no destructivo (no desactiva resolutores existentes del usuario).
- Sin migración EF. Sin DDL en prod.

## 7. Casos de prueba

1. Crear usuario con rol que tiene plantilla `(SOPORTE, país X)` → el usuario queda con resolutor `(SOPORTE, país X)` activo.
2. Asignar un 2º rol a un usuario existente (UpdateAsync) → se siembran sus resolutores sin borrar los previos.
3. Usuario con override manual `(DESARROLLO, global)` que NO está en la plantilla → re-seed del rol **no** lo elimina.
4. Editar plantilla del rol + "Aplicar a los usuarios de este rol" → los usuarios del rol reciben los nuevos tipos faltantes; los que ya tenían no se duplican.
5. Seed que falla (rol sin plantilla) → no rompe el alta/edición del usuario.
6. UI: en Usuario se ven 2 tarjetas separadas (Creación / Atención); en Rol una sola (Plantilla) con botón aplicar.

## 8. Validación

- Backend: `cd backend && dotnet build` (0 errores) + `dotnet test`.
- Frontend: `cd frontend && yarn build`.
- E2E manual (HMR) y `make down` al terminar.
