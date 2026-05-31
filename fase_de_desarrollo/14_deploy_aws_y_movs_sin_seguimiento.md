# Plan de Desarrollo — Deploy AWS automático + Mostrar movimientos sin seguimiento diario

**ID:** 14
**Feature:** (1) Empaquetar los fixes #10/#12 + la migración masiva #13 como una migración EF Core que se aplica automáticamente al arrancar el back en AWS. (2) Modificar la función SQL para que la tabla diaria también muestre fechas con movimientos de inventario o venta de aves aunque no tengan seguimiento diario.
**Estado:** En implementación
**Fecha:** 2026-05-28
**Plan padre:** [13_migracion_masiva_saldo_alimento.md](./13_migracion_masiva_saldo_alimento.md)

---

## Contexto y problemas

### Problema 1 — Deploy AWS

Hasta hoy los fixes se aplicaron manualmente vía `psql`. Para que el ambiente de producción quede alineado al levantar el back en AWS, todo debe estar empaquetado como migración EF Core que `ctx.Database.MigrateAsync()` ejecutará automáticamente al arranque (configurado en `DatabaseExtensions.cs:39` y disparado por `RunMigrations=true` por defecto en Production según `Program.cs:761`).

### Problema 2 — Movimientos sin seguimiento ocultos

La función `fn_seguimiento_diario_engorde` actualmente solo devuelve filas de fechas con seguimiento diario. Cuando hay un `INV_INGRESO`, `INV_TRASLADO_ENTRADA`, `INV_TRASLADO_SALIDA` o `VENTA_AVES` en una fecha donde el usuario aún no creó un seguimiento, **esa fecha no aparece en la tabla**. Para verla hay que crear un seguimiento (aunque sea con consumo 0) en esa fecha — flujo torpe.

Reportado por el usuario:
> *"si quiero ver un movimiento de alimento que realizo el 05 de mayo tengo que realizar un seguimiento diario en pollo engorde para poder verlo si no queda oculto y no lo veo en la tabla"*

## Solución

### Parte A — Migración EF Core que empaqueta todo

Crear `Migrations/20260528210000_FixFnSeguimientoEngordeAperturaYRecalcularSaldosMasivo.cs` que en `Up()`:

1. **Reemplaza la función** `fn_seguimiento_diario_engorde` con la versión v3 (fix #10 + #12 + parte B de este plan).
2. **Crea tabla de backup** `_migracion_saldo_alimento_2026_05_28` con snapshot del estado actual de `saldo_alimento_kg`.
3. **Ejecuta UPDATE masivo** usando `fn_seguimiento_diario_engorde` como fuente de verdad, solo para lotes activos.
4. **No falla la migración** si hay 0 filas a actualizar (idempotente).

En `Down()`:
- Restaurar `saldo_alimento_kg` desde la tabla de backup si existe.
- Recrear la función SQL en su versión v1 (la que tiene la migración previa `20260520140828_AddFnSeguimientoDiarioEngorde`).

### Parte B — Función SQL muestra movimientos sin seguimiento

Modificar `fn_seguimiento_diario_engorde` para que la tabla devuelva una fila **por cada fecha que tenga seguimiento O movimiento de inventario/ventas**.

#### Cambios en estructura SQL

1. **Nuevo CTE `fechas_universo`**: UNION de todas las fechas con seguimiento + fechas con movimientos en el galpón (INV_INGRESO, INV_TRASLADO_*, VENTA_AVES) durante el ciclo del lote.
2. **`seg_enriquecido`** parte de `fechas_universo` (no de `seguimiento_diario_aves_engorde`).
3. **`seg_id` puede ser NULL** para fechas sin seguimiento.
4. **Campos del seguimiento son NULL** para esas fechas (`mortalidad_*`, `consumo_*`, `peso_prom_*`, etc.), pero los campos calculados de saldo y movimientos se llenan correctamente.
5. **Edad y semana** se calculan siempre desde `fecha_encaset` aunque no haya seg.
6. **`tipo_alimento`** queda NULL pero se podría inferir del tipo del INV_INGRESO si se desea (queda opcional para versión futura).

#### Diseño SQL

```sql
fechas_universo AS (
    -- Fechas con seguimiento
    SELECT DATE(s.fecha) AS fecha, s.id AS seg_id
    FROM seguimiento_diario_aves_engorde s
    WHERE s.lote_ave_engorde_id = p_lote_id

    UNION

    -- Fechas con movimientos (alimento o ventas) en el ciclo
    SELECT DATE(h.fecha_operacion) AS fecha, NULL::BIGINT AS seg_id
    FROM lote_registro_historico_unificado h
    JOIN lote_info li ON TRUE
    JOIN rango_seg rs ON TRUE
    WHERE NOT h.anulado
      AND NOT (h.referencia IS NOT NULL AND (
               h.referencia LIKE '%devolución por eliminación%'
            OR h.referencia LIKE '%devolucion por eliminacion%'))
      AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
      AND (rs.fecha_max IS NULL OR DATE(h.fecha_operacion) <= rs.fecha_max)
      AND (
          -- Mov alimento scope galpón
          (h.tipo_evento IN ('INV_INGRESO','INV_TRASLADO_ENTRADA','INV_TRASLADO_SALIDA')
           AND NOT (h.tipo_evento = 'INV_INGRESO'
                    AND h.referencia IS NOT NULL
                    AND h.referencia LIKE 'Seguimiento aves engorde #%')
           AND h.farm_id = li.granja_id
           AND COALESCE(TRIM(h.nucleo_id), '') = li.nucleo_id
           AND COALESCE(TRIM(h.galpon_id), '') = li.galpon_id)
          OR
          -- Ventas scope lote
          (h.tipo_evento = 'VENTA_AVES' AND h.lote_ave_engorde_id = p_lote_id)
      )
    -- NO incluir fechas pre-encaset
    AND (li.fecha_encaset IS NULL OR DATE(h.fecha_operacion) >= li.fecha_encaset::DATE)
)
```

Luego `seg_enriquecido` cambia su FROM a `fechas_universo fu LEFT JOIN seguimiento_diario_aves_engorde s ON s.id = fu.seg_id`, y todos los campos del seg quedan opcionales con `COALESCE` adecuado.

#### Comportamiento esperado en lote 75 / 2602

Movimientos sin seguimiento durante el ciclo (entre `fecha_encaset 2026-04-29` y `fecha_actual`):
- 2026-04-29 / 2026-04-30 → si no hay movimientos, no aparecen (correcto)
- 2026-05-29 / 2026-05-30 etc → si después del último seguimiento aparece un INV_INGRESO, se mostrará una fila adicional

Para el lote 5 (validado antes), los días con INV_INGRESO entre 2026-05-08 y 2026-05-30 (que estaban ocultos) ahora aparecerán.

### Coherencia con servicios C#

El campo `SeguimientoDiarioTablaFilaDto.SegId` ya es `long` no nullable. Necesita cambiarse a `long?` para reflejar que las nuevas filas no tienen seg_id. El frontend debe manejar:
- Si `segId == null` → fila "informativa" del movimiento (mostrar dato pero sin acciones de edit/delete).
- Si `segId != null` → fila estándar del seguimiento.

---

## Archivos a crear/modificar

| Archivo | Acción |
|---|---|
| `backend/sql/fn_seguimiento_diario_engorde.sql` | **Modificar** — agregar CTE `fechas_universo`, cambiar FROM de `seg_enriquecido`, permitir NULLs |
| `backend/sql/migrate_recalcular_saldo_alimento_engorde.sql` | (sin cambios — ya existe; sirve como referencia para la migración EF Core) |
| `backend/src/ZooSanMarino.Infrastructure/Migrations/20260528210000_FixFnSeguimientoEngordeAperturaYRecalcularSaldosMasivo.cs` | **Crear** — embebe el SQL v3 + ejecuta el UPDATE masivo con snapshot |
| `backend/src/ZooSanMarino.Application/DTOs/SeguimientoDiarioTablaFilaDto.cs` | **Modificar** — `SegId` ahora `long?` |
| `backend/src/ZooSanMarino.Infrastructure/Migrations/20260528210000_FixFnSeguimientoEngordeAperturaYRecalcularSaldosMasivo.Designer.cs` | **Crear** — copia del Designer anterior (no hay cambios al modelo) |

---

## Validación

### Pre-migración (estado actual local — ya migrado manualmente)
- Función ya tiene fix #10 + #12 aplicados manualmente.
- Saldos ya actualizados por la migración manual del fix #13.
- La migración EF Core debe ser **idempotente** — al ejecutarse en local ahora no debe producir cambios extra (max delta < 0.001).

### Pre-migración (estado AWS / producción)
- Función SQL es la v1 (sin fixes).
- Saldos persistidos están con valores antiguos (con herencia).
- Al aplicar la migración EF Core: la función se reemplaza, los saldos se recalculan, los movimientos sin seguimiento empiezan a verse.

### Tests post-migración

1. **`SELECT * FROM fn_seguimiento_diario_engorde(75)` debe devolver 27+ filas** — incluye filas adicionales si hay movs sin seg.
2. **`SELECT * FROM fn_seguimiento_diario_engorde(5)` debe devolver 70+ filas** — antes 70, ahora pueden ser más por los 3 INV_INGRESO que detecté en días sin seg.
3. **Lote 75 día 1**: `saldo = 5,280`, `ingreso = 5,600`, `consumo = 320` (regresión del fix #12).
4. **Build dotnet sin errores**.

---

## Riesgos y mitigación

| Riesgo | Mitigación |
|---|---|
| El UPDATE masivo en producción puede tardar minutos si hay muchos lotes | Snapshot persistente + transacción permite rollback rápido |
| Si el frontend no maneja `SegId == null` puede romperse | Hacer el cambio del DTO opcional con `[JsonPropertyName]` y revisar usos en frontend |
| Migración no se puede re-ejecutar (EF Core) | El SQL es idempotente (DROP IF EXISTS + CREATE OR REPLACE + UPDATE WHERE diff). Si EF Core registra que ya se ejecutó, no se vuelve a aplicar |
| Down() podría no restaurar el orden original de seguimientos | El Down() recrea la función v1 y restaura saldos desde snapshot — restablece estado pre-migración |

---

## Notas para el frontend (no incluido en este fix)

Para que los movimientos sin seguimiento se vean correctamente:
1. El componente que renderiza `SeguimientoDiarioTablaFilaDto[]` debe permitir filas con `segId == null`.
2. Botones "Editar/Eliminar" deben ocultarse o reemplazarse por "Crear seguimiento aquí" cuando `segId == null`.
3. Visualmente, esas filas pueden tener estilo distinto (gris claro, ícono "ingreso/venta").

Esto es trabajo de frontend que queda como ticket separado — el cambio backend permite el flujo.
