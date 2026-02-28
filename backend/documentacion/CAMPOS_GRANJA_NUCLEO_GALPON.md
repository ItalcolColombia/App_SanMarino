# Dónde se guardan Granja, Núcleo y Galpón

En todas las tablas de lotes (Lote, LotePosturaLevante, LotePosturaProduccion) se persisten **granja_id**, **nucleo_id** y **galpon_id** (Farm, Núcleo, Galpón).

## Al crear un Lote (tabla `lotes`)

- **LoteService.CreateAsync** asigna y guarda en `lotes`:
  - `GranjaId` → `granja_id`
  - `NucleoId` → `nucleo_id`
  - `GalponId` → `galpon_id`

- **LotePosturaLevante** (tabla `lote_postura_levante`):
  - Si existe el trigger `trg_lotes_sync_lote_postura_levante` en la BD, el trigger crea un registro en `lote_postura_levante` con los mismos `granja_id`, `nucleo_id`, `galpon_id` del lote recién insertado.
  - Si el trigger no existe, **LoteService.CreateAsync** crea explícitamente el registro en `lote_postura_levante` con los mismos valores (granja, núcleo, galpón) del lote.

## Al actualizar un Lote (tabla `lotes`)

- **LoteService.UpdateAsync** actualiza `GranjaId`, `NucleoId`, `GalponId` en `lotes`.
- Si está instalado el trigger `trg_lotes_sync_lote_postura_levante`, el **UPDATE** en `lotes` hace que el trigger actualice el registro correspondiente en `lote_postura_levante` (mismo `lote_id`) con los nuevos `granja_id`, `nucleo_id`, `galpon_id`.

## LotePosturaProduccion (tabla `lote_postura_produccion`)

- Se crea cuando un LotePosturaLevante llega a **26 semanas** (LotePosturaLevanteService.ProcessarCierresPendientesAsync).
- **CrearLoteProduccion** copia de LotePosturaLevante a LotePosturaProduccion:
  - `GranjaId`, `NucleoId`, `GalponId` (y el resto de campos de ubicación).

## Al trasladar un lote (LoteService.TrasladarLoteAsync)

- Se actualiza siempre la tabla **lotes**: `GranjaId`, `NucleoId`, `GalponId` al destino.
- Según la edad del lote:
  - **&lt; 26 semanas (levante):** se actualizan los registros en **lote_postura_levante** (granja, núcleo, galpón destino).
  - **≥ 26 semanas (producción):** se actualizan los registros en **lote_postura_produccion** (granja, núcleo, galpón destino); no se modifica `lote_postura_levante`.

Resumen: en **creación** y **traslado**, granja, núcleo y galpón quedan guardados en la tabla que corresponde a la fase del lote (Lote siempre; LPL o LPP según edad en traslado).
