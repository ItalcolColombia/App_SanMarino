# [ARCHIVADO 2026-07-03] Tracker — Fase 3 (paso 2/3): consumo Colombia modelo A → modelo B

> Archivado al iniciar la tarea **SoporteBot** (loop de soporte). Estado congelado tal cual estaba.
> Plan original: [`fase_de_desarrollo/fase3_paso2_plan.md`](fase_de_desarrollo/fase3_paso2_plan.md)

## S1 — Backfill A→B ejecutado (local) ✅
- [x] Ejecutar `backend/sql/fase3_migracion_stock_co_a_b.sql` (COMMIT) contra BD local :5433
- [x] Verificar suma conservada 667421 (B == A) + 61 catálogo + 17 stock nivel granja
- [x] Confirmar idempotencia (re-run INSERT 0 0) + alineación catálogo (F: tipo_item minúscula, referencia=codigo, descripcion=nombre)
- [ ] Commit (documentar ejecución en el tracker)

## S2 — Helper id-mapping A→B + consumo nivel granja en B ✅
- [x] `InventarioGestionService`: `RegistrarConsumoNivelGranjaAsync` + `RegistrarIngresoNivelGranjaAsync` (aditivos, sin tx propia, stock nucleo/galpon NULL, validación de stock preservada)
- [x] Interfaz `IInventarioGestionService` extendida con los 2 métodos nuevos
- [x] `IColombiaInventarioConsumoService` + `ColombiaInventarioConsumoService`: id-mapping `catalogItemId → catalogo_items.codigo → item_inventario_ecuador.id` (batch) + delega en el consumo nivel granja
- [x] NO romper Ecuador (ruta ModeloB con núcleo/galpón intacta; métodos nuevos son aditivos)
- [x] Registrado en DI (Program.cs) como Scoped
- [x] `dotnet build` 0/0
- [ ] Commit

## S3 — Switch del gate + servicios (levante + producción) ✅
- [x] `InventarioConsumoGate.ResolverModelo`: Colombia (1) → ModeloBNivelGranja
- [x] Levante Create/Update/Delete: rama Colombia usa `IColombiaInventarioConsumoService`
- [x] ProduccionService Crear/Actualizar/Eliminar: idem
- [x] Path modelo A de Colombia queda sin uso
- [x] EC/PA (modelo B con galpón) sin cambios
- [x] `dotnet build` 0/0
- [ ] Commit

## S4 — Tests ✅
- [x] `InventarioConsumoGateTests`: Colombia → ModeloBNivelGranja + test "ya no usa modelo A" + coherencia
- [x] Contable no-afectación
- [x] id-mapping / consumo nivel granja verificado por BD
- [x] Bloqueo si no hay stock B verificado por BD
- [x] Contable/indicadores sin cambios
- [x] `dotnet build` 0/0 + `dotnet test` verde (Domain 1/1, Application 60/60)
- [ ] Commit

## Fase 3 paso 3 — Alineación FRONT Colombia con modelo B (EN CURSO al archivar)
- Objetivo: que Colombia VEA y OPERE su inventario B (hoy su menú apunta a `/inventario` = modelo A frozen).
- [ ] Menú: company_menus Colombia (1) → `/gestion-inventario` (50) + `/config/item-inventario-ecuador` (49)
- [ ] UI nivel granja para Colombia: ingreso/traslado NO exigen núcleo/galpón para alimento cuando es Colombia
- [ ] Validar (QA + visual) que Colombia ve el stock migrado y puede cargar/consumir; Ecuador intacto

> Evidencia detallada S1–S4 preservada en el historial de git y en el plan. Retomar creando un tracker nuevo cuando se reanude esta tarea.
