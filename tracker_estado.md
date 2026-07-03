# Tracker — Fase 3 (paso 2): switch consumo Colombia modelo A → modelo B

> Plan (fuente de verdad): [`fase_de_desarrollo/fase3_paso2_plan.md`](fase_de_desarrollo/fase3_paso2_plan.md)
> Base: `refactor/optimizacion-multipais` (HEAD `d317b44`). Un commit por slice.
> Baseline: `dotnet build` 0/0, `dotnet test` verde antes de empezar.

## S1 — Backfill A→B ejecutado (local) ✅
- [x] Ejecutar `backend/sql/fase3_migracion_stock_co_a_b.sql` (COMMIT) contra BD local :5433
- [x] Verificar suma conservada 667421 (B == A) + 61 catálogo + 17 stock nivel granja
- [x] Confirmar idempotencia (re-run INSERT 0 0) + alineación catálogo (F: tipo_item minúscula, referencia=codigo, descripcion=nombre)
- [ ] Commit (documentar ejecución en el tracker)

## S2 — Helper id-mapping A→B + consumo nivel granja en B
- [ ] `InventarioGestionService`: `RegistrarConsumoNivelGranjaAsync` + `RegistrarIngresoNivelGranjaAsync` (aditivos, sin tx propia, stock nucleo/galpon NULL, validación de stock preservada)
- [ ] Interfaz `IInventarioGestionService` extendida con los 2 métodos nuevos
- [ ] `IColombiaInventarioConsumoService` + `ColombiaInventarioConsumoService`: id-mapping `catalogItemId → catalogo_items.codigo → item_inventario_ecuador.id` (batch) + delega en el consumo nivel granja
- [ ] NO romper Ecuador (ruta ModeloB con núcleo/galpón intacta)
- [ ] Registrado en DI (Program.cs) como Scoped
- [ ] `dotnet build` 0/0
- [ ] Commit

## S3 — Switch del gate + servicios (levante + producción)
- [ ] `InventarioConsumoGate.ResolverModelo`: Colombia (1) → ModeloB
- [ ] Levante Create/Update/Delete: rama Colombia usa el nuevo servicio (item B + nivel granja), misma tx única + validación previa
- [ ] ProduccionService Crear/Actualizar/Eliminar: idem
- [ ] Path modelo A de Colombia queda sin uso (FarmInventoryConsumoService se deja)
- [ ] EC/PA (modelo B con galpón) sin cambios
- [ ] `dotnet build` 0/0
- [ ] Commit

## S4 — Tests
- [ ] `InventarioConsumoGateTests`: Colombia → ModeloB (actualizar)
- [ ] Test id-mapping / consumo nivel granja (donde sea puro)
- [ ] Bloqueo si no hay stock B (validación previa)
- [ ] Contable/indicadores sin cambios (golden vigente + verificación BD)
- [ ] `dotnet build` 0/0 + `dotnet test` verde
- [ ] Commit

## Evidencia
### S1 (backfill)
- PRE: A=61 catálogo, 17 stock (suma 667421); B Colombia (company1/pais1)=0/0.
- Ejecución `fase3_migracion_stock_co_a_b.sql` (COMMIT): INSERT 61 catálogo + INSERT 17 stock. Verificación in-script: B catálogo 61, B stock 17 filas, B suma 667421 == A suma 667421.
- Idempotencia: re-run → INSERT 0 0 / INSERT 0 0 (sin duplicar).
- F (alineación catálogo B): 61/61 tipo_item minúscula ('alimento'), 61/61 referencia=codigo, 61/61 descripcion=nombre.
- Stock B a nivel granja: 17/17 con nucleo_id/galpon_id NULL.
