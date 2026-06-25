# Plan: Fix — El solicitante (aunque sea admin) no gestiona su propio ticket

## Síntoma (reportado por el usuario, con screenshot)
En el detalle de un ticket abierto desde **Mis solicitudes**, el **creador** ve el panel **GESTIÓN**
("Tomar ticket" + "Cambiar estado a…") y puede cambiar el estado de su propia solicitud.
Caso concreto: *Admin Ecuador* creó el ticket TK-2026-000002 y, por tener `tickets.admin`, ve el panel.

Reglas pedidas:
- En **Mis solicitudes** la gestión NO debe estar habilitada (el mismo usuario no puede cambiar el
  estado de su ticket).
- Solo el **módulo de Gestión / Admin de tickets** gestiona, y solo sobre tickets **ajenos**.
- Ocultarlo también para el perfil que solo tiene permiso de **crear**; solo los **resolutores**
  (gestionar) / admin escogen estados.

## Causa raíz
`ticket-detalle` se abre con la misma ruta `:id` desde Mis solicitudes / Gestión / Admin. La
visibilidad del panel depende solo de permisos, con un hueco para el admin:

```ts
puedeGestionarTicket(t) {
  if (this.esAdmin) return true;            // ← el admin gestiona incluso lo que él creó
  return this.esResolutor && !t.soyCreador;
}
```

En backend, el mismo hueco está codificado con la excepción `&& !EsAdmin()`:
`if (EsCreador(ticket) && !EsAdmin()) throw …` → el admin-creador SÍ puede tomar/cambiar estado de su
propio ticket. Esa excepción es justamente lo que el usuario marca como incorrecto.

## Diseño de la solución
Regla única y enforce en ambas capas: **el creador NUNCA gestiona su propio ticket** (ni el admin); el
solicitante solo actúa vía "Confirmar cierre"/"Reabrir" (panel aparte, ya existente). La gestión queda
para resolutor/admin sobre tickets que **no** crearon.

### Frontend — `ticket-detalle.component.ts`
```ts
puedeGestionarTicket(t) {
  if (t.soyCreador) return false;           // solicitante (incl. admin) → sin gestión
  return this.esResolutor || this.esAdmin;  // solo resolutor/admin sobre tickets ajenos
}
```
- Un usuario con solo `tickets.crear` → `esResolutor`/`esAdmin` falsos → sin panel.
- En Mis solicitudes todos los tickets son propios (`soyCreador`) → sin panel.
- El panel "Validá la solución" (confirmar cierre/reabrir) sigue gobernado por `soyCreador && SOLUCIONADO`.

### Backend — `TicketService.cs` (enforcement, fuente de verdad de autorización)
- `TomarAsync`: `if (EsCreador(ticket) && !EsAdmin())` → `if (EsCreador(ticket))`.
- `CambiarEstadoAsync`: `if (EsCreador(ticket) && !EsAdmin())` → `if (EsCreador(ticket))` (el creador
  solo puede REABRIR SOLUCIONADO→EN_ANALISIS; el resto se rechaza). Actualizar comentario.
- `EsAdmin()` queda sin uso → eliminar (dead code). `ConfirmarCierreAsync` no cambia (ya es solo creador).
- Gestión de tickets **ajenos** intacta (ahí `EsCreador` es falso).

**Sin migración de BD** — solo lógica/UI.

## Casos de prueba
1. Admin-creador abre su propio ticket → no aparece panel GESTIÓN; no puede Tomar ni cambiar estado. ✓
2. Mismo admin abre un ticket ajeno → panel GESTIÓN visible y operativo. ✓
3. Usuario solo-crear abre cualquier ticket → sin panel GESTIÓN. ✓
4. Resolutor abre ticket ajeno → panel visible (igual que antes). ✓
5. Solicitante (incl. admin) sobre su ticket SOLUCIONADO → ve "Validá la solución" (confirmar/reabrir). ✓
6. API: admin-creador llama `cambiar-estado` a EN_ANALISIS (no reabrir) → rechazado por backend. ✓
7. `cd backend && dotnet build` y `cd frontend && yarn build` sin errores.
