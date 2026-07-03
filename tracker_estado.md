# Tracker — Fase 2: Colombia descuenta inventario (modelo A) desde seguimientos

> Plan (fuente de verdad): [`fase_de_desarrollo/fase2_plan.md`](fase_de_desarrollo/fase2_plan.md)
> Base: `refactor/optimizacion-multipais` (HEAD `68785a3`). Un commit atómico por slice.
> Baseline verificado: `dotnet build` 0/0, `dotnet test` 38 tests OK.

## S1 — Limpieza datos espurios (local) ✅ commit 6f1b32e
- [x] Script `backend/sql/fase2_limpieza_espurios.sql` idempotente (SELECT antes/después)
- [x] Borrar mov id=5705 + stock id=352 (company 1/pais 1, item 89, modelo B)
- [x] Borrar catalogo_items pais 2 (ids 303/304/305)
- [x] Ejecutar SOLO en local (5 filas borradas, verificado); prod requiere OK aparte (documentado)
- [x] Commit

## S2 — Movement types + kardex ✅
- [x] Enum `InventoryMovementType`: agregar `ConsumoSeguimiento`, `DevolucionSeguimiento`
- [x] `fn_kardex_signo`: ConsumoSeguimiento→-1, DevolucionSeguimiento→+1 (SQL + C# switch)
- [x] Migración idempotente HECHA A MANO (20260703140000; amplía movement_type varchar 20→30 porque
      "DevolucionSeguimiento"=21 chars NO cabía en 20; CREATE OR REPLACE fn; snapshot+Designer alineados)
- [x] Golden: casos nuevos en FarmInventoryKardexCalculosTests (40 tests OK) + re-verificado vs fn local
- [x] Commit

## S2b — Extender parser a itemsGenerales
- [ ] `MetadataEngordeCalculos.ParseMetadataItemsToKg`: agregar `itemsGenerales` (aditivo)
- [ ] No romper test verde Ecuador; leer SOLO de Metadata
- [ ] Test nuevo cubriendo generales
- [ ] Commit

## S3 — FarmInventoryConsumoService (modelo A, sin tx propia)
- [ ] Nuevo servicio/método: valida stock, decrementa Quantity, inserta movimiento (Consumo/Devolucion)
- [ ] NO abrir BeginTransactionAsync propia (participa de tx externa)
- [ ] Registrar en DI (Program.cs) como Scoped
- [ ] Extender `InventarioConsumoGate` para despachar por país (CO→modelo A)
- [ ] Commit

## S4 — Activar en levante + producción (Colombia)
- [ ] Levante (Create/Update/Delete): tx única + validación previa de stock ANTES de persistir
- [ ] ProduccionService (Crear/Actualizar/Eliminar): lógica nueva, resolver GranjaId, descontar desde DTOs
- [ ] EC/PA sin cambios (flujo B tolerante)
- [ ] Commit

## S5 — Tests de NO-AFECTACIÓN
- [ ] (a) Contable Colombia: ConsumoSeguimiento NO entra en buckets → cifras idénticas
- [ ] (b) Indicadores: evidencia/test que fn no leen inventario
- [ ] (c) Kardex golden re-verificado con nuevos tipos
- [ ] Commit
