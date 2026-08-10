# Auditoría F0.A — estado REAL de A1-A10 contra el código de hoy

**Fecha:** 2026-08-09
**Motivo:** el inventario de `pwa_offline_first_plan.md` §4.A es del **26-jul**. Antes de seguir
ejecutándolo hay que cruzarlo con el código y la BD de hoy — regla de CLAUDE.md: *ante desalineación,
gana el código actual, no el historial ni los planes viejos.*

**Método:** lectura de las funciones y triggers **vivos** en la BD local (refresh del dump de prod),
más grep sobre `backend/src`. No se asumió nada del plan.

---

## Resultado

| # | Qué decía el plan (26-jul) | Estado REAL (09-ago) | Evidencia |
|---|---|---|---|
| **A1** | Índice de clave natural no único + buscar-o-insertar | ✅ **HECHO** | commit `44b2400`, índice `ux_inventario_gestion_stock_clave_natural` |
| **A2** | Descuento read-modify-write | ✅ **HECHO** | commit `44b2400`, `UPDATE … WHERE quantity >= @q` |
| **A3** | El trigger de lotes pisa `aves_*_actual` en la rama UPDATE | ✅ **HECHO por otra sesión** | migración `20260806074742_ArreglarTriggerSyncLotePosturaLevanteNoPisarAvesVivas`; la función viva ya corre el saldo **por delta** |
| **A4** | Sacar el `SaveChangesAsync` de `ObtenerInformacionLoteAsync` | ⚠️ **EL PLAN ESTÁ MAL** — ver abajo | `ProduccionService.Consultas.cs:180` |
| **A5** | Soft delete + `sync_tombstones` | ❌ **PENDIENTE** | 0 tablas `%tombstone%`/`%sync%` en la BD |
| **A6** | Índice único de producción por `(lote_postura_produccion_id, fecha)` | ⚠️ **REQUIERE DECISIÓN** | hoy hay **dos** únicos, ambos por `lote_id` |
| **A7** | Dos services escriben levante con semántica distinta | ❌ **PENDIENTE** | `SeguimientoDiarioService` y `SeguimientoLoteLevanteService` siguen registrados |
| **A8** | `FechaOperacion` en el request de consumo | ✅ **HECHO** | `InventarioGestionConsumoRequest.FechaMovimiento` existe y está documentado |
| **A9** | `fn_lote_ave_engorde_id_desde_ubicacion` imputa al lote más reciente | ❌ **PENDIENTE — Y ES ZONA MINADA** | ver abajo |
| **A10** | Reemplazar el trigger acumulativo del espejo de huevo | ✅ **HECHO** | 0 triggers en `seguimiento_diario_produccion`; no existe ninguna función `%espejo%huevo%` |

**5 de 10 hechos.** Tres de ellos (A3, A8, A10) ya estaban resueltos y el plan no lo reflejaba.

---

## A4 — el plan pide algo que hoy ROMPERÍA el número

El plan dice: *"Sacar el `SaveChangesAsync` escondido en `ObtenerInformacionLoteAsync`: es una LECTURA
que ESCRIBE"*. El diagnóstico del síntoma es correcto —un `GET` escribe `aves_h_actual` y bumpea
`updated_at`, lo que inviabiliza cualquier cursor de sincronización—, pero **la corrección propuesta
es la equivocada**, y ejecutarla tal cual dejaría números mal.

Medido: `AvesHActual` del LPP tiene **al menos 6 escritores incrementales** (`+=` / `-=` con
`Math.Max(0, …)`): traslados desde seguimiento, migración de movimientos, movimientos de aves
(postura), descuento de levante. El bloque de `Consultas.cs:180` **no es un escritor más**: recalcula
el saldo desde `fn_seguimiento_diario_produccion` —la fórmula canónica— y corrige la columna cuando
difiere. O sea que hoy **ese "self-heal" es lo que mantiene la columna en su valor correcto**;
sacarlo dejaría a todos los consumidores leyendo la deriva acumulada de los escritores incrementales.

**La corrección correcta es la del invariante "una sola fórmula por número" de CLAUDE.md**, y ya
existe el precedente exacto: `SaldoAlimentoEngordeAplicador`, que escribe el saldo **desde la fn** y
dejó a los services delegando. A4 es ese mismo refactor para el saldo de aves de postura:

1. Un aplicador que escribe `aves_*_actual` **desde `fn_seguimiento_diario_produccion`**.
2. Los 6 escritores incrementales dejan de tocar la columna y pasan a invocarlo.
3. Recién ahí el `GET` puede dejar de escribir, porque el valor ya lo mantiene alguien más.

No es un cambio de una línea y **no se hace sin el gate de paridad**: es aritmética de saldos de aves
en producción.

---

## A6 — hay que decidir, no ejecutar

Índices únicos vivos en `seguimiento_diario_produccion`:

```
ix_seguimiento_diario_produccion_lote_id_fecha_registro  (lote_id, fecha_registro)
ux_seguimiento_diario_produccion_lote_dia_utc            (lote_id, (fecha_registro AT TIME ZONE 'UTC')::date)
```

El plan pide moverlos a `(lote_postura_produccion_id, fecha)` argumentando que *"dos galpones del
mismo lote base colisionan al sincronizar"*. **Eso hay que verificarlo con datos antes de tocar un
índice único**: si el modelo hoy garantiza un LPP por galpón y `lote_id` es el lote base, la colisión
es real; si `lote_id` ya identifica el galpón, cambiarlo permitiría duplicados que hoy están
correctamente prohibidos. Cambiar un índice único en base a un plan sin medir es exactamente lo que
la regla de schema de CLAUDE.md prohíbe.

Además hay **dos** únicos redundantes sobre lo mismo (uno por timestamp y otro por día UTC); esa
redundancia merece su propia revisión.

---

## A9 — pendiente, y es la zona que ya explotó cuatro veces

Verificado: la función sigue siendo

```sql
ORDER BY l.lote_ave_engorde_id DESC
LIMIT 1
```

sin ningún filtro por rango de vida del lote. El defecto es real: **el consumo del lote saliente se
imputa al lote entrante** en un galpón que encadena ciclos.

**Pero no se toca a ciegas.** Es el mismo terreno donde:

- la ventana de alimento previo al encaset se midió contra Panamá, se desplegó, y rompió Ecuador
  (26 lotes con apertura negativa, 330 filas en rojo, detectado a las 24 h);
- la marca «para el próximo ciclo» se intentó **cuatro veces** y se revirtió, y terminó con el
  checkbox deshabilitado en producción (`8424557`, 09-ago).

Requisitos **no negociables** antes de tocarla, según CLAUDE.md:

1. `backend/sql/verificar_paridad_saldo_engorde.sql` **antes** (congela) y **después** (compara).
2. Toda empresa que no sea el objetivo tiene que salir con **0 en todas las columnas**.
3. El cuadre (`fn_cuadre_alimento_engorde`) no puede moverse de **61 filas / 1 descuadrado**.

---

## Recomendación de orden

1. **A5 (tombstones)** — es el único bloqueante *duro* de F2/F3 que no depende de tocar aritmética de
   saldos, y es aditivo (columnas nuevas + tabla nueva + trigger `AFTER DELETE`). Riesgo bajo.
2. **A7** — consolidar los dos escritores de levante. Prerrequisito de cualquier regla portada a TS.
3. **A6** — medir primero si la colisión existe; recién después decidir el índice.
4. **A4** — el refactor del aplicador, con gate de paridad.
5. **A9** — al final, con el gate de paridad multipaís y en horario de baja operación.

> **Nota para quien retome esto:** el inventario del plan madre quedó desactualizado en 3 de 10 ítems
> en dos semanas. Antes de ejecutar cualquiera de los que quedan, **volvé a verificar contra la BD y
> el código**, como se hizo acá. Cuesta veinte minutos y evita "arreglar" algo que ya está bien.

---

## Apéndice — A6 MEDIDO (2026-08-09): la colisión que el plan describe NO existe en estos datos

Se midió antes de tocar nada, como exige la regla de schema de CLAUDE.md.

### 1. La premisa del plan no se reproduce

El plan justifica mover el índice único a `(lote_postura_produccion_id, fecha)` porque *"dos galpones
del mismo lote base colisionan"*. Medido:

```sql
SELECT lote_id, count(*) FROM lote_postura_produccion
WHERE deleted_at IS NULL GROUP BY lote_id HAVING count(*) > 1;
-- 0 filas
```

**Ningún `lote_id` tiene más de un LPP.** La relación es 1:1, así que el único por `lote_id` no puede
producir la colisión descrita. **Conclusión: no se cambia el índice.** Hacerlo por lo que dice un plan,
contra una medición que lo contradice, permitiría duplicados que hoy están correctamente prohibidos.

### 2. Hallazgo lateral: la entidad `SeguimientoDiario` NO mapea a una tabla unificada

`SeguimientoDiarioConfiguration` hace `ToTable("seguimiento_diario_levante")`. O sea que
`SeguimientoDiarioService` —el service "unificado"— escribe en la tabla de **levante**, y
`seguimiento_diario_produccion` es una tabla distinta con sus propios índices. Es la deuda de
"tablas duplicadas vivas" y conviene tenerla presente: **razonar sobre índices de
`seguimiento_diario_produccion` no dice nada sobre lo que escribe ese service.**

Medido en `seguimiento_diario_levante`: **588 filas, todas `tipo_seguimiento = 'levante'`, 0 con
`lote_postura_produccion_id`.**

### 3. Dos índices únicos que sobran

- `uq_sdlr_prod_lote_fecha` es **parcial sobre `lote_id_int`** con `WHERE lote_id_int IS NOT NULL`.
  Esa columna es NULL en el 100 % de prod (ver la memoria `lote-id-int-legado-mata-lectores-levante`),
  y además la tabla tiene **0 filas de producción**: el índice **no puede dispararse nunca**.
- En `seguimiento_diario_produccion`, `ix_..._lote_id_fecha_registro` (por timestamp) es **redundante**
  con `ux_..._lote_dia_utc` (por día UTC): si dos filas comparten el timestamp exacto, comparten el
  día, así que el índice por día ya las rechaza. El estricto implica al laxo.

Ninguna de las dos limpiezas es urgente y **ninguna se hace en este item**: quitar un índice único es
irreversible en la práctica (recrearlo exige que los datos sigan cumpliendo), y el beneficio es
cosmético. Queda anotado con la medición para que la decisión se tome con datos.

**Estado de A6: CERRADO como "no se cambia", con la medición como fundamento.**
