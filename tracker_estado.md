# Tracker — Seguimiento reproductora engorde: el día del encasetamiento cuenta como DÍA 1

**Plan:** [fase_de_desarrollo/seguimiento_reproductora_engorde_edad_cero_plan.md](fase_de_desarrollo/seguimiento_reproductora_engorde_edad_cero_plan.md)

## Backend
- [x] `ReproductoraEngordeCalculos.EsEdadSeguimientoValida` → edad [0,7] + doc (día 1 = encaset; edad 7 tolerada por lotes con numeración previa)
- [x] `SeguimientoDiarioLoteReproductoraService` Create/Update: condición `edad < 0` + mensajes con "día 1 = día del encasetamiento"
- [x] `MigracionService.SeguimientoReproductora`: condición/mensaje + plantilla (día 1 = encaset, día 7 = encaset+6)
- [x] `backend/sql/fn_cruce_reproductora_a_engorde.sql`: consolida edades 0..7, retiros `[0, d)`, observaciones sin "(día d)"
- [x] Migración `20260724100000_CruceReproductoraEngordeEdadCero` + Designer clonado (Up: fn nueva + recálculo dirigido de lotes con edad 0 confirmada; Down: fn previa + limpieza edad 0 + recálculo)
- [x] Tests xUnit (0/1/6/7 válidas, 8/−1 inválidas + escenario encaset 16/07 vs 15/07)

## Frontend
- [x] Modal: `minFechaYmd` = fecha de encasetamiento + mensajes/hint "día 1 = día del encasetamiento"
- [x] Lista: fecha sugerida del primer registro = día del encaset; columna "Día" = edad + 1 (día 1 = encaset)
- [x] `construir-bloques-reproductora.funcion.ts`: ventana días 1..7 desde edad 0 si hay registro del día del encaset (históricos idénticos)

## Validación
- [x] `dotnet build` Infrastructure: 0 errores, 0 warnings
- [x] `dotnet test`: 643 pasando
- [x] `yarn build` front: OK (solo warning bundle budget preexistente)
- [x] Smoke SQL local (BEGIN…ROLLBACK): cruce genera edad 0 (fecha = encaset, mort 3/2) y preserva edades 1..7; BD intacta
