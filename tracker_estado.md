# Tracker — Fase 2: Colombia descuenta inventario (modelo A) desde seguimientos

> Plan (fuente de verdad): [`fase_de_desarrollo/fase2_plan.md`](fase_de_desarrollo/fase2_plan.md)
> Base: `refactor/optimizacion-multipais` (HEAD `68785a3`). Un commit atómico por slice.
> Baseline verificado: `dotnet build` 0/0, `dotnet test` 38 tests OK.

## S1 — Limpieza datos espurios (local)
- [ ] Script `backend/sql/fase2_limpieza_espurios.sql` idempotente (SELECT antes/después)
- [ ] Borrar mov id=5705 + stock id=352 (company 1/pais 1, item 89, modelo B)
- [ ] Borrar catalogo_items pais 2 (ids 303/304/305)
- [ ] Ejecutar SOLO en local; documentar que prod requiere OK aparte
- [ ] Commit

## S2 — Movement types + kardex
- [ ] Enum `InventoryMovementType`: agregar `ConsumoSeguimiento`, `DevolucionSeguimiento`
- [ ] `fn_kardex_signo`: ConsumoSeguimiento→-1, DevolucionSeguimiento→+1 (SQL + C# switch)
- [ ] Migración idempotente HECHA A MANO (CREATE OR REPLACE, timestamp posterior, ModelSnapshot intacto)
- [ ] Golden: casos nuevos en FarmInventoryKardexCalculosTests + re-verificar vs fn en local
- [ ] Commit

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
