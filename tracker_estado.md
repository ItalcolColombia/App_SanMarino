# Tracker — Bloquear venta de lotes cerrados / corridas anteriores en "Venta por granja"

Plan: [venta_granja_bloqueo_lotes_cerrados_plan.md](fase_de_desarrollo/venta_granja_bloqueo_lotes_cerrados_plan.md)

**Alcance:** solo front-end (decisión del usuario) + migración EF para sembrar el permiso
`movimientos_pollo_engorde.vender_lotes_cerrados` en `permissions`. Módulo:
`frontend/src/app/features/movimientos-pollo-engorde/`.

---

## Frontend

- [x] `models/venta-granja.model.ts` — agregar `bloqueada?` / `motivoBloqueo?` a `VentaLineaGranja`
- [x] `funciones/detectar-lotes-bloqueados-venta.funcion.ts` — nueva función pura `marcarLotesBloqueadosVenta`
- [x] `funciones/README.md` — agregar la nueva función al índice
- [x] `modal-movimiento-pollo-engorde.component.ts` — inyectar `UserPermissionService`, getter `puedeVenderLotesCerrados`, aplicar marcado en `loadVentaGranjaLineas()`, guard en `onLineaCantidadInput`, guard en `onSubmit`
- [x] `modal-movimiento-pollo-engorde.component.html` — disabled/readonly extendido, badge de motivo, hint explicativo
- [x] `modal-movimiento-pollo-engorde.component.scss` — estilos badge + fila bloqueada
- [x] `yarn build` (frontend) 0 errores (Node portable 22.23.1 — Node del sistema 22.15 no cumple el mínimo de Angular)

## Backend (solo permiso)

- [x] `dotnet ef migrations add` (pura de datos) — seed `movimientos_pollo_engorde.vender_lotes_cerrados` (`20260714112951_AddPermisoVentaLotesCerradosMovimientoPolloEngorde`)
- [x] `dotnet build` 0 errores (Infrastructure + Application.Tests; API no se pudo rebuildear por bin bloqueado del backend en vivo del usuario — no bloqueante)
- [x] `dotnet ef database update` local sin error — permiso id 58 confirmado por psql; idempotencia del `INSERT ... WHERE NOT EXISTS` verificada (rollback de prueba: `INSERT 0 0`)

## Validación

- [x] Prueba manual en navegador — confirmada por el usuario ("esta correcto ya lo probe")
- [x] Reportar al usuario

## Cierre

- [x] Todo implementado y validado. Pendiente: el usuario decide si commitea (no se hizo commit, solo se preguntó/implementó).
