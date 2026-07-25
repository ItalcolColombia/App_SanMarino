# Tracker — `fn_rekey_nucleo` copia `codigo_bodega`/`descripcion_bodega` al mover núcleo

**Plan:** [fase_de_desarrollo/fn_mover_ubicacion_codigo_bodega_plan.md](fase_de_desarrollo/fn_mover_ubicacion_codigo_bodega_plan.md)

## SQL
- [x] `backend/sql/fn_mover_ubicacion.sql`: agregar `codigo_bodega`/`descripcion_bodega` al INSERT/SELECT de `fn_rekey_nucleo` + comentario-advertencia de lista explícita

## Migración
- [x] `20260725210000_FnMoverUbicacionCopiaBodegaNucleo.cs`: Up = columnas defensivas IF NOT EXISTS + CREATE OR REPLACE de las 3 funciones; Down = versión previa de `fn_rekey_nucleo`, sin borrar nada
- [x] Designer clonado de `20260725130000` (ModelSnapshot intacto)

## Validación
- [x] `dotnet build` 0 errores
- [x] `database update` en `sanmarinoapplocal:5433` (dotnet-ef 10, desde Infrastructure) aplica solo la nueva
- [x] BD: 3 funciones creadas; `fn_rekey_nucleo` incluye las 2 columnas
- [x] Smoke BEGIN…ROLLBACK: mover núcleo temporal conserva bodega
- [x] Commit en rama worktree (autor moisesmurillo, sin atribución)
