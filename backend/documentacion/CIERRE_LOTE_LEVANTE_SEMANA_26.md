# Cierre automático de lotes Levante en semana 26

## Resumen

Cuando un lote de levante alcanza la **semana 26** (por edad o por `fecha_encaset`), se cierra automáticamente y se crean lotes de producción separados por género (H y M).

## Implementación: Trigger + Backend

Se eligió lógica en el **backend** en lugar de un trigger de base de datos porque:

1. **Lógica compleja**: Crear dos registros con nombres derivados (QLK345-H, QLK345-M) y copiar muchos campos es más claro y mantenible en C#.
2. **Validación**: El backend valida `edad >= 26` antes de cerrar.
3. **Control de usuario**: Se registra `CreatedByUserId` y `UpdatedByUserId` para auditoría.
4. **Casos especiales**: Si `aves_h_actual` y `aves_m_actual` son 0, se crean ambos lotes con 0 para consistencia.

## Flujo

1. **estado_cierre** en `lote_postura_levante`:
   - `Abierto`: antes de semana 26 (edad < 26).
   - `Cerrado`: cuando edad >= 26.

2. **Disparador**: Al llamar `GET /api/LotePosturaLevante`, el servicio ejecuta `ProcessarCierresPendientesAsync` **antes** de devolver los lotes:
   - Busca lotes con `edad >= 26` y `estado_cierre = 'Abierto'`.
   - Para cada uno: pone `estado_cierre = 'Cerrado'` y crea lotes en `lote_postura_produccion`.

3. **Lotes producción creados**:
   - Ejemplo: QLK345 → `QLK345-H` (hembras) y `QLK345-M` (machos).
   - `aves_h_actual` y `aves_m_actual` del levante pasan a `hembras_iniciales_prod`/`machos_iniciales_prod` y a `aves_h_inicial`/`aves_m_inicial` de cada lote producción.

## Archivos modificados

- **Entity**: `LotePosturaLevante.EstadoCierre`
- **DTO**: `LotePosturaLevanteDetailDto.EstadoCierre`
- **Servicio**: `LotePosturaLevanteService.ProcessarCierresPendientesAsync`, `CrearLoteProduccion`
- **SQL**: `add_estado_cierre_lote_postura_levante.sql`, trigger actualizado con `estado_cierre`

## Trigger en base de datos

1. **`trigger_lotes_to_lote_postura_levante.sql`** (actualizado): Calcula `edad` en semanas desde `fecha_encaset` cuando `edad_inicial` es null.
2. **`trigger_lote_postura_levante_cerrar_produccion.sql`** (nuevo): En `lote_postura_levante` AFTER INSERT/UPDATE, si edad >= 26 (por `edad` o por `fecha_encaset`), cierra y crea lotes producción H/M.

Orden de ejecución al crear un lote:
- Lotes INSERT → trigger lotes sync → INSERT en lote_postura_levante (con edad calculada)
- lote_postura_levante INSERT → trigger cerrar_produccion → si edad >= 26, crea H/M y marca Cerrado

## Despliegue

1. Ejecutar migración EF o `add_estado_cierre_lote_postura_levante.sql`.
2. Actualizar trigger: `trigger_lotes_to_lote_postura_levante.sql`.
3. Crear trigger: `trigger_lote_postura_levante_cerrar_produccion.sql`.
