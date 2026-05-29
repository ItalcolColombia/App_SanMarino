# Plan de Desarrollo — Fix SQL: saldo de apertura no debe heredar inventario de lote anterior

**ID:** 12
**Feature:** Corrección de la función SQL `fn_seguimiento_diario_engorde` Y del método `RecalcularSaldoAlimentoPorLoteAsync` (backend) para que el saldo de apertura del galpón **NO** herede el inventario residual del lote anterior que ocupó el mismo galpón.
**Estado:** Pendiente (asignado a otro agente, paralelo al fix #11)
**Fecha:** 2026-05-28
**Plan padre:** [10_fix_fn_seguimiento_diario_engorde_saldos.md](./10_fix_fn_seguimiento_diario_engorde_saldos.md)

---

## Hallazgos del usuario

Validado contra Excel real (`Seguimiento_engorde_2602_20260528 (1).xlsx`, lote 2602, galpón con historial previo):

### Problema 1 — Saldo de apertura hereda lote anterior

**Primer día del seguimiento del lote 2602: 2026-05-01**
- Ingreso del día: **5,600 kg**
- Consumo del día: 320 kg
- Saldo reportado: **137,557 kg** ❌

**Cálculo manual del saldo correcto del primer día (regla del negocio):**
```
saldo_día_1 = ingreso_día_1 − consumo_día_1
           = 5600 − 320
           = 5280 kg ✅
```

**El saldo actual incluye 132,277 kg** que provienen de movimientos del galpón *anteriores* al encaset del lote 2602 — son inventario residual del **lote previo** que ocupó el mismo galpón. La regla del negocio dice que al iniciar un nuevo lote, el inventario debería arrancar en 0 (el galpón se limpia / el alimento residual se traslada o descarta).

### Problema 2 — Movimientos en días sin seguimiento se pierden en cálculo dinámico SQL

Detectado durante la validación del fix #11 con el lote 2 (companyId=3):

| Métrica | Saldo persistido (backend) | Saldo `fn_seguimiento_diario_engorde` | Δ |
|---|---|---|---|
| Seguimiento creado 2026-05-28 | **71,430 kg** ✅ matemático | **1,500 kg** ❌ | 69,930 |

Causa raíz: la función SQL hace `LEFT JOIN seguimiento_diario_aves_engorde s LEFT JOIN hist_alimento ha ON ha.fecha = DATE(s.fecha)`. Cuando un `INV_INGRESO` ocurre en un día sin seguimiento (ej. lote 2 tuvo 9 ingresos entre 2026-04-21 y 2026-05-27 sin seguimientos esos días), esos kg **no aparecen en `seg_enriquecido.ingreso_alimento_kg`** y por tanto no se suman al `SUM() OVER w_ord` que calcula el saldo dinámico. El cálculo persistido del backend (`RecalcularSaldoAlimentoPorLoteAsync`) sí los incluye correctamente porque procesa todos los eventos del histórico, no solo los días de seguimiento.

---

## Diseño propuesto

### Fix 1 — Saldo de apertura debe arrancar en 0 (o ≥ fecha_encaset del lote)

**Regla del negocio:** el saldo de apertura del galpón al inicio del lote actual = **suma de movimientos del galpón ENTRE `fecha_encaset` y `primer_seguimiento − 1 día`**, **excluyendo** todo lo anterior a `fecha_encaset`.

#### En SQL (`apertura_alimento` CTE)

Cambiar la condición:
```sql
-- ANTES:
AND DATE(h.fecha_operacion) < rs.fecha_min
```

a:
```sql
-- DESPUÉS:
AND DATE(h.fecha_operacion) < rs.fecha_min
AND (li.fecha_encaset IS NULL OR DATE(h.fecha_operacion) >= li.fecha_encaset::DATE)
```

Esto restringe la apertura a movimientos que ocurrieron **desde la fecha de encaset del lote actual**, dejando fuera el residual del lote anterior.

#### En backend (`ComputeSaldoAperturaGalponAntesPrimerSeguimiento`)

Agregar parámetro `DateTime? fechaEncaset` y filtrar:
```csharp
foreach (var h in hist)
{
    var ymd = YmdHistoricoEfectivo(h);
    if (ymd is null || string.Compare(ymd, firstYmd, StringComparison.Ordinal) >= 0) continue;
    if (fechaEncaset.HasValue && string.Compare(ymd, FormatYmd(fechaEncaset.Value.Date), StringComparison.Ordinal) < 0) continue;
    // ... resto igual
}
```

Y actualizar los call sites para pasar `lote.FechaEncaset`. Esto aplica a ambos servicios:
- `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeService.cs:418-446`
- `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeEcuadorService.cs` (helper portado)

### Fix 2 — Saldo dinámico SQL debe incluir movimientos de días sin seguimiento

Refactorizar la query final de `fn_seguimiento_diario_engorde`:

**Opción A (recomendada): segundo cálculo del saldo con CTE de movimientos por fecha**

Crear un CTE `mov_kg_por_fecha` que tenga TODAS las fechas con movimientos en el rango (no solo las que tienen seguimiento):

```sql
mov_kg_por_fecha AS (
    SELECT fecha, ingreso_kg + traslado_entrada_kg - traslado_salida_kg AS mov_kg
    FROM hist_alimento  -- ya existe; tiene una fila por fecha con movimientos
)
```

Luego en `seg_enriquecido` agregar:

```sql
-- Para cada fila de seguimiento, sumar todos los movimientos del galpón ANTES o EN su fecha,
-- restar todo el consumo del seguimiento ANTES o EN su fecha. Da el saldo correcto incluso
-- si hubo ingresos en días sin seguimiento.
COALESCE((SELECT SUM(mov_kg) FROM mov_kg_por_fecha m WHERE m.fecha <= DATE(s.fecha)), 0) AS mov_acum_kg
```

Y en el SELECT final:

```sql
GREATEST(0,
    (SELECT apertura_kg FROM apertura_alimento)
    + se.mov_acum_kg                                           -- todos los movimientos hasta esta fecha
    - SUM(se.consumo_dia_kg) OVER w_ord                        -- consumo acumulado del seguimiento
)::FLOAT8 AS saldo_alimento_kg
```

**Opción B**: cambiar el orden — partir de `generate_series(fecha_min, fecha_max)` y JOIN-ear seguimiento.

Opción A es más simple y menos invasiva.

---

## Plan de validación

| Verificación | Esperado |
|---|---|
| Lote 2602 — primer día seguimiento (2026-05-01) | `saldo_alimento_kg = 5280` (no 137,557) |
| Lote 2602 — segundo día (2026-05-02) | `saldo = 5280 − 360 = 4920` |
| Lote 2 — seguimiento 2026-05-28 | `fn().saldo = 71,430` (igual al persistido) |
| Lote 5 — saldo final coincide con persistido | sí (regresión) |
| Lote 32 — saldo final llega a 0 (cerrado) | sí (regresión) |

## Archivos a modificar

| Archivo | Cambio |
|---|---|
| `backend/sql/fn_seguimiento_diario_engorde.sql` | (1) Filtro `>= li.fecha_encaset` en `apertura_alimento` CTE. (2) Nuevo CTE `mov_kg_por_fecha`. (3) Cálculo del saldo en SELECT final usando `mov_acum_kg` en lugar de `SUM OVER w_ord`. |
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeService.cs` | Modificar `ComputeSaldoAperturaGalponAntesPrimerSeguimiento` para aceptar `fechaEncaset` y filtrar. Actualizar call site en `RecalcularSaldoAlimentoPorLoteAsync:507`. |
| `backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoAvesEngordeEcuadorService.cs` | Mismo cambio en el helper portado. Actualizar call site en `RecalcularSaldoAlimentoPorLoteAsync`. |

---

## Coordinación con fix #11 (parallelo)

Mientras este agente trabaja en este fix #12, el fix #11 ya está completo y validado:
- Endpoints `POST/PUT/DELETE /api/SeguimientoAvesEngordeEcuador` ahora descuentan inventario, anulan `INV_CONSUMO` y recalculan saldo.
- Validación contra BD: ✅ POST 201, ✅ DELETE 204, ✅ POST lote cerrado → 400.

No hay conflicto de archivos: este fix toca `fn_seguimiento_diario_engorde.sql` + métodos helper de saldo, sin tocar Create/Update/Delete del fix #11.

---

## Evidencia del hallazgo (lote 2602 — Excel)

| Fecha | Ingreso (kg) | Consumo (kg) | Saldo reportado | Saldo correcto esperado |
|---|---|---|---|---|
| 01/05/2026 | 5,600 | 320 | 137,557 ❌ | **5,280** ✅ |
| 02/05/2026 | — | 360 | 137,197 ❌ | 4,920 ✅ |
| 03/05/2026 | — | 440 | 136,757 ❌ | 4,480 ✅ |
| 04/05/2026 | — | 600 | 136,157 ❌ | 3,880 ✅ |
| 05/05/2026 | — | 680 | 135,477 ❌ | 3,200 ✅ |
| 06/05/2026 | — | 760 | 134,717 ❌ | 2,440 ✅ |
| 07/05/2026 | — | 880 | 133,837 ❌ | 1,560 ✅ |
| 08/05/2026 | 9,315 | 960 | 142,192 ❌ | 9,915 ✅ |
