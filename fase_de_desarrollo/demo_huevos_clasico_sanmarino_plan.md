# Plan — Demo vuelve a la clasificación de huevos CLÁSICA (Sanmarino) en seguimiento diario producción

**Fecha:** 2026-07-25 · **Pedido:** la empresa Demo NO debe tener la clasificación de huevo por ítems hecha
para Santa Reyes; debe conservar la parte sencilla de Sanmarino (11 columnas clásicas que suman a Huevo Total).

## Diagnóstico (verificado en BD local)

- `20260726000000_ActivarFeaturesSantaReyesEnDemo` encendió en Demo los 3 flags y copió el catálogo huevo de
  Santa Reyes (21 ítems `item_type='huevo'`). El modal/grilla/indicadores/reporte contable de producción se
  gatean por `companies.clasificacion_huevo_por_items` — con flag OFF el comportamiento es el clásico
  **byte a byte** (garantizado por los tests de `HuevoItemsCalculos`/gating de la Fase 2 SR).
- Demo hoy: `clasificacion_huevo_por_items = true`, 21 ítems huevo, **0** seguimientos de producción y **0**
  referencias `metadata->huevoItems` → reversión limpia.
- El usuario SOLO pide la parte de huevos: `maneja_codigos_erp_avicola` y `permite_traslado_aves_cross_etapa`
  quedan como están en Demo.

## Enfoque

**Solo datos, cero código** (el gating por flag ya existe y está testeado). Migración data-only
`DesactivarClasificacionHuevoItemsEnDemo` (Designer clonado por scaffold, ModelSnapshot intacto), idempotente,
por lookups de nombre, que ordena DESPUÉS de `20260726030933`:

1. `UPDATE companies SET clasificacion_huevo_por_items = false WHERE name='Demo'` (guarda `IS DISTINCT FROM`).
2. `DELETE` de los ítems huevo de Demo **que vinieron de Santa Reyes** (por `codigo` presente en el catálogo
   huevo de SR — espejo del `Down()` de la migración de activación), con guardas: sin referencias en
   `farm_inventory_movements` / `farm_product_inventory` (FKs RESTRICT) ni en
   `seguimiento_diario_produccion.metadata->'huevoItems'[].catalogItemId` de lotes de Demo (fail-safe si el
   cliente ya clasificó en prod: esos ítems no se borran, el flag OFF ya oculta la UI y `huevo_tot` conserva
   los totales legacy).
3. `Down()` best-effort: re-enciende el flag y re-copia los ítems desde SR (mismo INSERT de la activación).

En prod nada está desplegado: la cadena corre completa en el mismo deploy (activa → desactiva) sin ventana.

## Archivos

- `Infrastructure/Migrations/<ts>_DesactivarClasificacionHuevoItemsEnDemo.cs` (+Designer clonado por scaffold).

## Validación

1. Aplicar local (`dotnet ef database update`) → Demo: flag false; 0 ítems huevo; SR intacta (flag true, 21 ítems).
2. Idempotencia: re-ejecutar el SQL → 0 cambios.
3. Smoke UI (Demo): pantalla de seguimiento producción SIN columnas/tab de clasificación por ítems (UI clásica);
   Santa Reyes conserva la nueva.
4. Migración lista para el deploy (sin push, como el resto de la cadena SR).
