# Plan — extender `registros.fecha_retroactiva` a Seguimiento Diario Levante y Producción

## Pedido
El usuario ya tiene el permiso `registros.fecha_retroactiva` (fase_de_desarrollo/permiso_fecha_retroactiva_registros_plan.md,
20-ago-2026) gobernando movimientos de inventario, movimientos de aves, movimientos/ventas de pollo
engorde, traslados de aves/huevos y gastos de inventario. Pide que el MISMO permiso también gobierne
la fecha de **Seguimiento Diario Lote Levante** y **Seguimiento Diario Producción**, que hoy no tienen
ninguna restricción de ventana (se puede fechar cualquier día pasado sin límite, sin pedir el permiso).

## Verificado antes de tocar código
- `SeguimientoLoteLevanteController.Create`/`Update` y `SeguimientoProduccionController.Create`/`Update`
  NO llaman a `ValidarVentanaFechaRegistro` (grep negativo) — es el único punto de entrada manual del
  alcance del permiso que quedó afuera cuando se sembró (20-ago).
- El mecanismo (`VentanaFechaRegistroCalculos` + `VentanaFechaRegistroGuard`, backend; funciones
  espejo en `shared/utils/fecha/ventana-fecha-registro.funcion.ts`, front) ya es genérico — no hace
  falta tocarlo, solo invocarlo desde los 2 controllers y 2 componentes que faltan.
- El patrón de integración en el front es liviano: `min`/`max`/hint en el datepicker (UX), el
  controller es quien de verdad rechaza — replicado tal cual en 8 componentes existentes
  (`movimiento-alimento-form`, `modal-movimiento-pollo-engorde`, etc.), ninguno agrega un Validator
  custom.

## Alcance — 2 backend + 2 frontend, sin BD, sin permiso nuevo
### Backend
- `SeguimientoLoteLevanteController.cs`: `Create`/`Update` → `this.ValidarVentanaFechaRegistro(request?.FechaRegistro)`
  antes de llamar al service (mismo comentario/patrón que `MovimientoAvesController`).
- `SeguimientoProduccionController.cs`: `Create`/`Update` → `this.ValidarVentanaFechaRegistro(dto.Fecha)`.

### Frontend
- `lote-levante/pages/modal-create-edit/modal-create-edit.component.ts`: inyectar `UserPermissionService`,
  calcular `fechaRegistroMin/Max/Hint` en `ngOnInit` (mismo patrón `aplicarVentanaFecha`), bind
  `[attr.min]`/`[max]` en `#f-fecha` + hint bajo el campo.
- `lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.ts`: ídem,
  reemplaza el hint estático "Fecha del seguimiento diario" por el dinámico.

## Fuera de alcance (a propósito)
- Engorde y Reproductora: no los pidió el usuario; si hace falta después es el mismo patrón 2 veces más.
- Ningún cambio en `VentanaFechaRegistroCalculos`/`VentanaFechaRegistroGuard` ni en la función espejo
  del front: son genéricos, ya sirven.
- Sin migración: el permiso ya existe y ya está habilitado por empresa desde el 20-ago.

## Validación
- `dotnet build` + `dotnet test` (backend).
- `yarn build` (frontend).
- Caso de prueba manual: sin permiso, fecha fuera de la ventana (mes en curso ∪ 15 días) → 400 con el
  mismo mensaje que movimientos; con el permiso, cualquier fecha pasada entra; futuro siempre rechazado.
