# Tracker — Fix seguimiento producción: "no tiene LoteId asociado" (400 en Demo)

**Plan:** [fase_de_desarrollo/fix_seguimiento_produccion_lote_id_plan.md](fase_de_desarrollo/fix_seguimiento_produccion_lote_id_plan.md)

## Diagnóstico
- [x] Reproducir causa en BD local (copia prod): LPP #8/#9 Demo con `lote_id NULL`, levantes 11/16 con `lote_id` válido (119/124)
- [x] Confirmar `seguimiento_diario_produccion.lote_id` NOT NULL → requisito real, no relajable
- [x] Confirmar 0 levantes activos sin `lote_id` y que el cierre de levante YA hereda `LoteId` (fix previo); backfill nunca aplicado a prod

## Implementación (agente backend, Fable 5)
- [x] `Application/Calculos/SeguimientoProduccionLoteIdCalculos.cs` (lógica pura ResolverLoteIdEfectivo)
- [x] `ProduccionService.CrearSeguimientoAsync`: self-heal LoteId vía levante + persistir en LPP (ExecuteUpdate) + error claro si irreparable
- [x] `ProduccionService.ActualizarSeguimientoAsync`: mismo self-heal (helper compartido `ResolverYSanarLoteIdAsync`)
- [x] Migración data-only idempotente `20260726052546_BackfillLoteIdLotePosturaProduccion` (scaffold dotnet-ef 10, snapshot intacto, Down no-op)
- [x] Tests xUnit `SeguimientoProduccionLoteIdCalculosTests` (8 casos)

## Validación
- [x] Build: 0 errores CS (la solución completa solo choca con DLLs bloqueadas por el backend corriendo del usuario; Infrastructure y Tests compilan 0 warnings/0 errores)
- [x] `dotnet test`: 727/727 Application.Tests + 1/1 Domain.Tests (incluye los 8 nuevos)
- [x] `dotnet ef database update` local aplicó `20260726052546`
- [x] BD local: LPP #8→119, #9→124 (y #2/#4 borrados también sanados); 0 filas activas sin lote_id
- [x] Smoke E2E real (backend :5002, JWT+X-Secret-Up minteados): POST payload EXACTO del usuario → 201 id 680 con lote_id=124, stock modelo B descontado 1000→850.003; DELETE 680 → 204, stock restaurado 1000.000, sin residuo
- [x] Revisión de código (agente reviewer Opus): 0 críticos/altos, 2 medios, 4 bajos → veredicto "aprueba con sugerencias"
- [x] Fixes de revisión aplicados: (1) levante solo de la MISMA empresa en el self-heal (fail-closed), (3) heal estampa `updated_at`/`updated_by_user_id` (trazabilidad del histórico), (4) `lev.company_id = p.company_id` también en la migración, (6) guarda de duplicado alineada al índice único real `(lote_id, fecha)` (evita 500 por violación de unicidad), (5) script sql/ legacy marcado SUPERSEDIDO y endurecido
- [x] Hallazgo 2 descartado con razón: el heal es idempotente y máximo-una-vez; reparar la fila aunque la request luego falle es deseable (misma filosofía que la migración)
- [x] Revalidación post-fixes: Infrastructure 0 errores/0 warnings, tests 727/727, SQL endurecido verificado en BD local (UPDATE 0, idempotente)
