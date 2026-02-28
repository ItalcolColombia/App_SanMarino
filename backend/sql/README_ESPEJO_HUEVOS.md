# Espejo Huevo Producción - Setup completo

Para que el flujo de huevos quede alineado al eliminar/editar registros de seguimiento diario, ejecutar en este orden:

## 1. Crear tabla espejo

```bash
psql -f sql/create_espejo_huevo_produccion.sql
```

## 2. Crear trigger en seguimiento_diario

```bash
psql -f sql/trigger_espejo_huevo_produccion_seguimiento_diario.sql
```

El trigger se ejecuta automáticamente en INSERT, UPDATE y DELETE de `seguimiento_diario` (solo tipo `produccion` con `lote_postura_produccion_id`):

| Operación | Efecto en espejo_huevo_produccion |
|-----------|-----------------------------------|
| **INSERT** | Suma huevos a *_historico y *_dinamico, actualiza historico_semanal |
| **UPDATE** | Resta OLD, suma NEW |
| **DELETE** | Resta los huevos del registro eliminado (historico, dinamico y historico_semanal) |

## 3. Backfill y migración (datos existentes + movimientos)

Para alinear el espejo con **todo** el seguimiento diario ya guardado y restar los traslados/ventas ya **Completados** (huevo disponible histórico vs actual):

```bash
psql -f sql/migracion_espejo_huevo_desde_seguimiento_y_movimientos.sql
```

Este script:
- Llena/actualiza `espejo_huevo_produccion` desde `seguimiento_diario` (por `lote_postura_produccion_id`): `*_historico` y `*_dinamico` inicial = suma de huevos por categoría.
- Resta de `*_dinamico` las cantidades de los traslados con estado `Completado` (por LPP; si el traslado tiene solo `lote_id`, se resuelve a LPP por `lote_id` o formato `LPP-<id>`).

Alternativa solo backfill (sin restar movimientos):

```bash
psql -f sql/backfill_espejo_huevo_produccion.sql
```

## Flujo al eliminar un registro

Cuando se elimina un registro de seguimiento diario producción:

1. **Aves (aves_h_actual, aves_m_actual)**: El backend (`SeguimientoDiarioService.DeleteAsync`) suma de nuevo la mortalidad, selección y error de sexaje al lote.
2. **Huevos (espejo_huevo_produccion)**: El trigger en BD resta los huevos del registro eliminado de *_historico, *_dinamico y historico_semanal.

Ambos quedan alineados sin pérdida de datos.
