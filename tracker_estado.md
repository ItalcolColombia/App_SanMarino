# Tracker — Fase 3 (paso 2): switch consumo Colombia modelo A → modelo B

> Plan (fuente de verdad): [`fase_de_desarrollo/fase3_paso2_plan.md`](fase_de_desarrollo/fase3_paso2_plan.md)
> Base: `refactor/optimizacion-multipais` (HEAD `d317b44`). Un commit por slice.
> Baseline: `dotnet build` 0/0, `dotnet test` verde antes de empezar.

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
- [x] `InventarioConsumoGate.ResolverModelo`: Colombia (1) → ModeloBNivelGranja (enum nuevo, distinto de ModeloB de EC/PA para no exigir galpón)
- [x] Levante Create/Update/Delete: rama Colombia usa `IColombiaInventarioConsumoService` (item B + nivel granja), misma tx única + validación previa
- [x] ProduccionService Crear/Actualizar/Eliminar: idem
- [x] Path modelo A de Colombia queda sin uso (FarmInventoryConsumoService + campo `_farmInventoryConsumo` se dejan por diseño; ya no se llaman en CO)
- [x] EC/PA (modelo B con galpón) sin cambios; `DebeDescontarModeloB` intacto (reproductora/engorde siguen sin descontar CO)
- [x] `dotnet build` 0/0
- [ ] Commit

## S4 — Tests ✅
- [x] `InventarioConsumoGateTests`: Colombia → ModeloBNivelGranja (actualizado) + test "ya no usa modelo A" + coherencia
- [x] Contable no-afectación: agregado test tipos modelo B ('Consumo'/'Ingreso') fuera de los buckets del contable modelo A
- [x] id-mapping / consumo nivel granja verificado por BD (BEGIN/ROLLBACK; no es puro → verificación SQL)
- [x] Bloqueo si no hay stock B (validación previa) verificado por BD
- [x] Contable/indicadores sin cambios (golden vigente + verificación BD)
- [x] `dotnet build` 0/0 + `dotnet test` verde (Domain 1/1, Application 60/60)
- [ ] Commit

## Evidencia
### S1 (backfill)
- PRE: A=61 catálogo, 17 stock (suma 667421); B Colombia (company1/pais1)=0/0.
- Ejecución `fase3_migracion_stock_co_a_b.sql` (COMMIT): INSERT 61 catálogo + INSERT 17 stock. Verificación in-script: B catálogo 61, B stock 17 filas, B suma 667421 == A suma 667421.
- Idempotencia: re-run → INSERT 0 0 / INSERT 0 0 (sin duplicar).
- F (alineación catálogo B): 61/61 tipo_item minúscula ('alimento'), 61/61 referencia=codigo, 61/61 descripcion=nombre.
- Stock B a nivel granja: 17/17 con nucleo_id/galpon_id NULL.

### S2/S3/S4 (código + BD)
- **Build/test**: `dotnet build` 0 warnings/0 errors; `dotnet test` 61/61 (Domain 1 + Application 60; +3 tests nuevos).
- **id-mapping A→B** (verificado): catalogItemId 61 (codigo 040475) → item B 259; batch por código, company 1/pais 1.
- **Colombia descuenta B nivel granja** (BEGIN/ROLLBACK, farm 3/item 259): stock 455343 → consumo −1000 → **454343** (movimiento 'Consumo', nucleo/galpon NULL) → devolución +300 → **454643** → post-rollback 455343 (sin datos alterados).
- **Bloqueo**: `ValidarStockConsumoAsync` (replica SQL) → pedir 999999999 kg (disp. 455343) = THROW insuficiente ANTES de persistir → no se guarda el seguimiento.
- **Ecuador intacto**: stock B pais 2 = 132 filas TODAS con núcleo/galpón (con_ubicacion=132, nivel_granja=0); Colombia (pais 1) = 17 filas nivel granja, aisladas. Sin mezcla.
- **Contable NO afectado**: model A buckets Colombia idénticos (Entry 31/730679, Exit 153/63257.8, TransferIn 1/323, TransferOut 1/323); 0 filas ConsumoSeguimiento/DevolucionSeguimiento en model A → estado pre-Fase-2. Los tipos 'Consumo'/'Ingreso' del modelo B NO figuran en ningún bucket del contable (test + el contable no lee inventario_gestion).
- **Indicadores NO afectados**: 0 referencias a tablas de inventario en `fn_indicadores_levante/produccion_postura`.
- **Sin migración EF**: cambios app-level (enum ModeloInventarioConsumo, no entidad). `InventoryMovementType` sin tocar. Backfill data-only.

## Notas / diseño
- Enum `ModeloInventarioConsumo.ModeloBNivelGranja` (nuevo) = Colombia unifica en modelo B pero por (granja, ítem) sin galpón; `ModeloB` (EC/PA) sigue con núcleo/galpón. Así el switch NO rompe Ecuador.
- `FarmInventoryConsumoService` (modelo A) + campos `_farmInventoryConsumo` quedan por diseño (path modelo A ya no lo llama Colombia).
- Pendiente futuro (fuera de alcance): PROD requiere OK+backup para el backfill; retiro del modelo A / ruta `/inventario`; migración de movimientos/kardex históricos A→B.

## Fase 3 paso 2 — QA: ESTABLE ✅ (merge FF, 88 commits)
- Colombia consume del modelo B a NIVEL GRANJA (id-mapping catalogItemId→codigo→item_inventario_ecuador; `ColombiaInventarioConsumoService` + `RegistrarConsumo/IngresoNivelGranjaAsync` aditivos). Gate: Colombia→`ModeloBNivelGranja`.
- QA (BEGIN/ROLLBACK): stock B baja/sube correcto (455343→454343→454743); bloqueo previo a persistir; **Ecuador INTACTO** (RegistrarConsumoAsync con galpón sin cambios; CO 1/1 nivel granja 17 filas vs EC 3/2 galpón 132, disjuntos); **contable e indicadores idénticos**. build 0/0, tests 105.
- Backfill ejecutado en local (catálogo 61 + stock 17, suma 667421). PROD: backfill requiere OK+backup.

## Fase 3 paso 3 — Alineación FRONT Colombia con modelo B (EN CURSO)
- Objetivo: que Colombia VEA y OPERE su inventario B (hoy su menú apunta a `/inventario` = modelo A frozen).
- [ ] Menú: company_menus Colombia (1) → `/gestion-inventario` (50) + `/config/item-inventario-ecuador` (49). Idempotente local; prod OK.
- [ ] UI nivel granja para Colombia: ingreso/traslado en gestion-inventario NO exigen núcleo/galpón para alimento cuando es Colombia (consistente con el back); wire ingreso nivel granja para carga inicial de stock.
- [ ] Validar (QA + visual) que Colombia ve el stock migrado y puede cargar/consumir; Ecuador intacto.
