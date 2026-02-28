# Movimientos de huevos: tablas y flujo

## Dónde se guarda cada cosa

| Qué | Tabla | Descripción |
|-----|--------|-------------|
| **Registro de movimientos** (traslados/ventas de huevos) | **`traslado_huevos`** | Cada movimiento se guarda aquí: número, fecha, tipo (Venta/Traslado), lote origen (`lote_id`, `lote_postura_produccion_id`), cantidades por categoría, estado (Pendiente, Completado, Cancelado). |
| **Saldo histórico y disponible** (espejo por LPP) | **`espejo_huevo_produccion`** | Una fila por `lote_postura_produccion_id`. `*_historico` = acumulado desde seguimiento diario. `*_dinamico` = disponible; se resta cuando se procesa un movimiento. |
| **Producción diaria** (clasificación de huevos por día) | **`seguimiento_diario`** | Registros de producción con huevo_limpio, huevo_tratado, etc. El **trigger** actualiza el espejo (suma en historico y dinamico). |

## Flujo cuando hay un movimiento de huevos

1. **Crear traslado**  
   Se inserta un registro en **`traslado_huevos`** con estado `Pendiente`. No se modifica el espejo.

2. **Procesar traslado** (marcar como Completado)  
   - El registro en **`traslado_huevos`** pasa a estado `Completado`.  
   - Si el traslado tiene **`lote_postura_produccion_id`** (flujo LPP), el backend llama a **`AplicarDescuentoEnEspejoAsync`** y se actualiza **`espejo_huevo_produccion`**: se restan las cantidades del movimiento solo en las columnas **`*_dinamico`** (no se toca `*_historico` ni `historico_semanal`).

3. **Cancelar traslado**  
   Si estaba Completado y se cancela, se llama a **`RevertirDescuentoEnEspejoAsync`** y se **suman** de nuevo las cantidades en `*_dinamico`.

## Resumen

- **Tabla de movimientos**: `traslado_huevos` (registro de todos los traslados/ventas).
- **Tabla espejo**: `espejo_huevo_produccion` (histórico + disponible por LPP).
- Al **procesar** un movimiento (Completado), la aplicación **actualiza el espejo** restando en `*_dinamico`.  
- Para consultar movimientos y cruzar con el espejo puedes usar el script **`sql/consulta_movimientos_huevos_y_espejo.sql`**.
