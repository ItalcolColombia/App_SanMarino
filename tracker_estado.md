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

## S2b — Extender parser a itemsGenerales ✅ commit 6bf8715
- [x] `MetadataEngordeCalculos.ParseMetadataItemsToKg`: agregar `itemsGenerales` (aditivo)
- [x] No romper Ecuador (no usa generales; parser preserva fallback); leer SOLO de Metadata
- [x] Test nuevo MetadataEngordeCalculosTests (6 casos, 46 tests OK)
- [x] Commit

## S3 — FarmInventoryConsumoService (modelo A, sin tx propia) ✅
- [x] Nuevo IFarmInventoryConsumoService + FarmInventoryConsumoService: Validar/Consumo/Devolucion/Diff
- [x] NO abre BeginTransactionAsync propia (participa de tx externa; solo SaveChanges de alta stock)
- [x] Registrado en DI (Program.cs) como Scoped
- [x] `InventarioConsumoGate.ResolverModelo(paisId)` → ModeloB (EC/PA) / ModeloA (CO) / Ninguno
- [x] Tests dispatch (53 tests OK); DebeDescontarModeloB conservado (compat EC/PA)
- [x] Commit 7c9ceeb

## S4 — Activar en levante + producción (Colombia) ✅
- [x] Levante Create/Update/Delete: dispatch por modelo; CO=modelo A con tx única +
      validación previa de stock ANTES de persistir; ajuste de aves dentro de la misma tx
- [x] ProduccionService Crear/Actualizar/Eliminar: lógica NUEVA; resuelve GranjaId+modelo;
      descuenta desde DTOs ItemsHembras/Machos (no re-parsea JSON); diff old (del metadata guardado)
      vs new; tx única; devolución total en delete
- [x] EC/PA sin cambios (flujo B tolerante, sin tx nueva)
- [x] Inyección opcional IFarmInventoryConsumoService en ambos servicios (build 0/0, 54 tests OK)
- [x] Commit 04892b9

## S5 — Tests de NO-AFECTACIÓN ✅
- [x] (a) Contable: ReporteContableNoAfectacionTests (tipos Fase 2 fuera de los 3 buckets;
      solo Entry/TransferIn/TransferOut/Exit entran) + verificación SQL buckets ANTES==DESPUES
- [x] (b) Indicadores: fn_indicadores_levante/produccion_postura = 0 refs a inventario
      (código + fn desplegada en BD) → indicadores idénticos
- [x] (c) Kardex golden re-verificado: fn firma ConsumoSeguimiento=-1 / DevolucionSeguimiento=+1
      contra data real (farm 3/item 61): consumo -100, devolución +30, saldos correctos
- [x] Script `backend/sql/fase2_verificacion_no_afectacion.sql` (read-only + BEGIN/ROLLBACK). 57 tests OK
- [x] Commit
