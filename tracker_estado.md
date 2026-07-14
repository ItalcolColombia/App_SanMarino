# Tracker — Fix: descuento de aves en migración masiva de Seguimiento Levante

Plan: [migracion_seguimiento_levante_aves_fix_plan.md](fase_de_desarrollo/migracion_seguimiento_levante_aves_fix_plan.md)

**Tarea activa:** la carga masiva de Seguimiento Levante recalcula `aves_h_actual/aves_m_actual`
desde cero e ignora traslados/movimientos de aves ya aplicados al lote; además saltea en silencio
filas del Excel cuando la fecha ya tiene una fila "solo traslado". Fix: descuento incremental
(misma semántica que el alta manual) + merge sobre filas traslado-only.

---

## F0 — Diagnóstico
- [x] Comparar flujo manual (`SeguimientoDiarioService`/`SeguimientoLoteLevanteService`) vs. función SQL de migración
- [x] Identificar Bug A: recálculo total ignora traslados (`TrasladoAvesDesdeSegService`) y movimientos (`MovimientoAvesService`)
- [x] Identificar impacto real: `LotePosturaLevanteService.GetResumenCierreAsync`/`CerrarLoteYCrearProduccionAsync` usan ese campo para cerrar Levante→Producción
- [x] Identificar Bug B: filas "solo traslado" bloquean silenciosamente el import de esa fecha
- [x] Plan escrito y tracker reiniciado

## F1 — Fix SQL (`fn_migracion_seguimiento_levante`)
- [x] Reescribir función: merge sobre filas `es_traslado=true` sin datos manuales
- [x] Reescribir función: insert igual que hoy para fechas sin ninguna fila previa
- [x] Reescribir función: descuento incremental sobre `aves_h_actual`/`aves_m_actual` (no recálculo total)
- [x] Verificar idempotencia (reimportar mismo archivo no duplica ni descuenta doble)
- [x] Bug extra encontrado y corregido en el propio fix: `CREATE TEMP TABLE ... ON COMMIT DROP` colisiona si la función se invoca 2 veces en la misma transacción → `DROP TABLE IF EXISTS` defensivo antes de crear las temporales
- [x] Bug extra encontrado y corregido: el matching por `fecha` exacta (`sd.fecha = f.fecha::timestamptz`) no encuentra filas creadas por `TrasladoAvesDesdeSegService` (guardan la fecha con offset horario distinto al cast SQL) → se cambió a comparación por fecha calendario (`sd.fecha::date = f.fecha`) tanto en el merge como en el dedup del insert

## F2 — Migración EF
- [x] `dotnet ef migrations add FixMigracionSeguimientoLevanteAvesIncremental` (`CREATE OR REPLACE FUNCTION` actualizado + `Down` = función original)
- [x] Aplicada a BD local (:5433) — ejecutada directamente vía `psql` (backend local del usuario corriendo en paralelo, PID bloqueando el build de `ZooSanMarino.API`; no se detuvo su proceso). **Pendiente:** correr `dotnet ef database update` cuando esa sesión esté libre, para que `__EFMigrationsHistory` quede marcada (la función ya está aplicada y es idempotente — `CREATE OR REPLACE` — así que no hay riesgo si se reintenta)

## F3 — Validación
- [x] Smoke SQL manual (transacción con `ROLLBACK`, datos reales del lote 114 con traslado de salida de 7617 hembras ya aplicado): migrar mortalidad histórica nueva → `aves_h_actual` descuenta SOLO lo nuevo y conserva el traslado (7575→7565, no se resetea a partir de `aves_h_inicial`)
- [x] Smoke SQL manual: reimport mismo archivo → 0 filas procesadas, `aves_h_actual` sin cambios (idempotente)
- [x] Smoke SQL manual: fecha con fila solo-traslado (id 890) + fila Excel con mortalidad → se fusiona (mortalidad/consumo se completan, `traslado_salida_hembras` intacto), no se saltea, `aves_h_actual` descuenta correctamente
- [x] `dotnet build` (proyecto Infrastructure) 0 errores / 0 warnings
- [x] Sin procesos huérfanos: smoke test corrido dentro de transacción con `ROLLBACK` (BD sin cambios); script de scratch borrado; no se tocó el backend en vivo del usuario (PID 12292)
- [x] **Prueba end-to-end con backend real (pedida por el usuario):** harness console (`scratchpad/SmokeTestMigracion/`, fuera del repo) que arma un `ZooSanMarinoContext` real contra :5433, un `MigracionService` real (stubs solo en los servicios que el flujo Levante no usa, vía `DispatchProxy`) y llama `ImportarAsync(TipoMigracion.SeguimientoLevante, ...)` — el mismo código que ejecuta el `MigracionController` en producción. Subió un .xlsx real con mortalidad (8 hembras) para el lote 114/fecha 2030-03-15: `aves_h_actual` pasó de 7575→7567 (descontó SOLO lo nuevo, conservando el traslado de 7617 intacto), fila insertada y auditoría registrada correctamente. Limpieza automática al final (fila + auditoría borradas, aves restauradas a 7575/1003) — verificado que la BD quedó idéntica al snapshot inicial. No tocó el proceso del backend en vivo del usuario (build separado, sin depender de `ZooSanMarino.API`).

## F4 — Cierre
- [x] Reportar al usuario: fix aplicado + nota sobre `fn_migracion_seguimiento_produccion` (mismo patrón de bug, fuera de alcance de este fix) + pendiente de `dotnet ef database update` local
- [x] Memoria del proyecto actualizada
- [ ] Commit de la mejora (pedido por el usuario)
- [ ] Siguiente chat: mismo análisis/fix para la carga masiva de Producción (`fn_migracion_seguimiento_produccion`)
