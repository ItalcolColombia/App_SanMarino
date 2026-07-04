# Plan — `fn_metadata_items_kg` (parseo de metadata en Postgres) + equivalencia

## Objetivo
Evaluar mover el parseo `metadata → (item_id, kg)` del back (C# `MetadataEngordeCalculos.ParseMetadataItemsToKg`) a una función Postgres, para reducir código en el back. **Solo se implementa el swap si la equivalencia es EXACTA** (aritmética idéntica, redondeos incluidos — regla CLAUDE.md).

## Enfoque
1. **POC función** `fn_metadata_items_kg(m jsonb) RETURNS TABLE(item_id int, kg numeric)` que replica EXACTO la lógica C#:
   - Acumula `itemsHembras` + `itemsMachos` + `itemsGenerales` (solo si son arrays).
   - id = `itemInventarioEcuadorId` si presente y `> 0`; si no, `catalogItemId`. Descarta id `<= 0`.
   - kg = `cantidad/1000` si unidad ∈ {g,gramos,gramo}; si no, `cantidad`. `cantidad` faltante → 0.
   - Agrupa por id, suma kg.
2. **Verificación de equivalencia** sobre datos reales + casos borde sintéticos (grams, cantidad faltante, iie<=0 fallback, no-array).
3. **Dato clave ya verificado:** en datos reales solo hay `kg`/`unidades` (0 gramos) → identidad → equivalencia exacta garantizada.

## Riesgo / decisión
- Único riesgo de redondeo = rama gramos (double `/1000.0` en C# vs `numeric/1000` en SQL). **0 filas reales usan gramos** → sin riesgo hoy.
- ⚠️ El parser está en la ruta de **descuento validado**. Cualquier swap debe preservar comportamiento + quedar cubierto por tests.
- **Trade-off a evaluar tras el POC:** el parser ya está centralizado (1 método puro, 25 líneas, con test). Mover a SQL puede NO reducir LOC neto (añade plumbing EF + round-trip) y va contra "Calculos en C#". Se decide con evidencia del POC.

## Casos de prueba
- Real: cada `metadata` de seguimiento_diario_(levante|produccion)_reproductoras + aves_engorde + lote_seguimientos → función vs SUM inline.
- Sintético: gramos (500 g → 0.5), cantidad ausente (→0), iie=0 con catalogItemId (fallback), array vacío / propiedad no-array (ignora).
