# Fase 3 — Paso 2: switch de consumo de inventario de Colombia (modelo A → modelo B)

> Base: `refactor/optimizacion-multipais` (HEAD `d317b44`). Un commit por slice.
> Contexto: `fase_de_desarrollo/fase2_plan.md`, `fase2_negocio_definicion.md`, `fase2_impacto_qa.md`, `backend/sql/fase3_migracion_stock_co_a_b.sql`.
> Objetivo: Colombia (company 1 / pais 1) deja de consumir del **modelo A** (`farm_product_inventory` + `farm_inventory_movements`, tipos `ConsumoSeguimiento`/`DevolucionSeguimiento`) y pasa a consumir del **modelo B** (`inventario_gestion_stock` / `inventario_gestion_movimiento` sobre `item_inventario_ecuador`), unificando con Ecuador/Panamá **sin romperlos**.

## Enfoque arquitectónico
- El stock Colombia en B está a **nivel granja** (nucleo/galpon NULL) tras el backfill (S1). Ecuador/Panamá siguen a nivel núcleo/galpón para alimento — NO se toca ese comportamiento.
- El gate `InventarioConsumoGate.ResolverModelo` cambia Colombia (pais 1) de `ModeloA` → `ModeloB`. Ecuador/Panamá quedan igual.
- La rama Colombia en levante/producción ya NO puede compartir la ruta ModeloB de Ecuador (esa exige núcleo/galpón para alimento). Se introduce un **servicio Colombia dedicado** que:
  1. resuelve `catalogItemId → item_inventario_ecuador_id` (id-mapping, paso B),
  2. descuenta en B a **nivel granja** (nucleo/galpon NULL) vía un método aditivo nuevo `RegistrarConsumoNivelGranjaAsync` en `InventarioGestionService` (paso C), SIN abrir transacción propia (participa de la tx externa, como `FarmInventoryConsumoService`), manteniendo la validación de stock (bloqueo).
- Interfaz idéntica a `IFarmInventoryConsumoService` (Validar/Consumo/Devolucion/Diff) para que las ramas Colombia de levante/producción cambien **solo la dependencia inyectada**, preservando la estructura tx + validación previa (comportamiento del bloqueo intacto).

## Archivos a crear / modificar
- **Crear** `Application/Interfaces/IColombiaInventarioConsumoService.cs` — misma forma que `IFarmInventoryConsumoService`, pero descuenta en modelo B nivel granja.
- **Crear** `Infrastructure/Services/ColombiaInventarioConsumoService.cs` — id-mapping A→B (batch) + delega en `InventarioGestionService.RegistrarConsumoNivelGranjaAsync` / `RegistrarIngresoNivelGranjaAsync`.
- **Modificar** `Application/Interfaces/IInventarioGestionService.cs` + `Infrastructure/Services/InventarioGestionService.cs` — agregar `RegistrarConsumoNivelGranjaAsync` / `RegistrarIngresoNivelGranjaAsync` (aditivos, sin tx propia, contra stock (farm, item, nucleo=NULL, galpon=NULL), validación de stock preservada).
- **Modificar** `Application/Calculos/InventarioConsumoGate.cs` — `ResolverModelo`: Colombia → `ModeloB`.
- **Modificar** `Infrastructure/Services/SeguimientoLoteLevanteService.cs` + `ProduccionService.cs` — la rama Colombia inyecta el nuevo servicio; el path modelo A queda sin uso (dejar `FarmInventoryConsumoService`).
- **Modificar** `API/Program.cs` — registrar el nuevo servicio (Scoped) + inyectarlo en levante/producción.
- **Tests** `tests/ZooSanMarino.Application.Tests/` — actualizar `InventarioConsumoGateTests` (Colombia→ModeloB) + tests nuevos del id-mapping/consumo nivel granja (puro donde aplique).

## Cambios de BD / SQL
- **Backfill ya ejecutado** (S1) en local: `backend/sql/fase3_migracion_stock_co_a_b.sql` (COMMIT). Sin DDL nuevo. Sin migración EF (no cambia esquema).

## Reglas de negocio
- Colombia descuenta en B a nivel granja (nucleo/galpon NULL) para TODOS los ítems (todos alimento tras el backfill).
- Bloqueo atómico preservado: validación previa de stock ANTES de persistir; si falta → throw por ítem → rollback.
- Ecuador/Panamá: modelo B con núcleo/galpón, sin cambios.
- Contable (modelo A) NO recibe consumos nuevos → buckets idénticos (mismo estado que pre-Fase-2). Indicadores no leen inventario → intactos.

## Casos de prueba
- Colombia crea seguimiento → descuenta stock B nivel granja; edición → diff; borrado → devolución.
- Bloqueo: sin stock B suficiente → throw, no se guarda.
- Ecuador intacto (modelo B con galpón).
- Contable/indicadores sin cambios.

## Slices
- **S1** backfill ejecutado (documentado). ✅
- **S2** helper id-mapping A→B + consumo nivel granja en B (sin romper Ecuador).
- **S3** switch del gate + servicios (levante + producción).
- **S4** tests (Colombia descuenta B nivel granja; bloqueo si no hay stock B; Ecuador intacto; contable/indicadores sin cambios).
