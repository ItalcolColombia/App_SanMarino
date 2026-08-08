# Plan — Lote cerrado que absorbe el ciclo siguiente + ventana de mes actual en Gestión de Inventario

**Fecha:** 2026-08-07
**Ticket de operación (Ecuador):** «validando el reporte de granja KM 86 lote 01 galpón 1 y 02 tenemos
ingreso del mes de julio cuando el lote cerró en abril».
**Pedido adicional del usuario:** que en Gestión de Inventario solo se pueda cargar movimientos
manualmente con fecha del **mes actual**, para evitar meter meses anteriores.

---

## 0. Diagnóstico (contra el dump de producción en la BD local `sanmarinoapplocal:5433`)

### 0.1 El lote de la captura está identificado sin ambigüedad

`Kilometro 86` = `farms.id = 40` (company 3, ItalcolEcuador) · `Galpon-1` = `G0039`, `Galpon-2` = `G0040`.

| id | lote | galpón | encaset | estado_operativo | primer seg | último seg |
|---|---|---|---|---|---|---|
| **2** | 2601 | G0039 (Galpon-1) | 2026-02-11 | **Abierto** | 2026-02-13 | **2026-04-20** |
| 72 | 2602 | G0039 | 2026-04-22 | Cerrado | 2026-04-24 | 2026-06-06 |
| 104 | 2603 | G0039 | 2026-06-24 | Abierto | 2026-06-26 | 2026-08-06 |
| **12** | 2601 | G0040 (Galpon-2) | 2026-02-15 | **Abierto** | 2026-02-17 | **2026-04-21** |
| 13 | 2601 | G0041 | 2026-02-22 | Cerrado | — | 2026-04-20 |
| 14 | 2601 | G0042 | 2026-02-22 | Cerrado | — | 2026-04-27 |

La captura reproduce **byte a byte** la salida de `fn_seguimiento_diario_engorde(2)`:
edad 123 = `2026-06-14 − 2026-02-11`, saldo 99.030 el 14/06 … 206.450 el 03/08, «Salida 1000 kg»,
«Entrada 2240 kg». Es el lote 2601 de **Galpon-1**. Los galpones 3 y 4 del mismo lote 2601 **sí** se
cerraron en abril y su grilla corta bien: la diferencia es exactamente el `estado_operativo_lote`.

### 0.2 Causa raíz — el CIERRE de la ventana no conoce el ciclo siguiente

`fn_seguimiento_diario_engorde` acota la grilla con `rango_final.fecha_max`:

```sql
fecha_max = COALESCE(
    saldo_close.close_date,                                  -- primer día ≥ último seg con saldo ≤ 0.5
    CASE WHEN estado = 'cerrado' THEN last_seg ELSE NULL END -- fallback por estado
)
```

Para el lote 2 se dan las dos condiciones a la vez:

1. **nunca se liquidó** ⇒ `estado_operativo_lote = 'Abierto'` ⇒ el fallback no aplica; y
2. **su saldo nunca llega a 0** después del último seguimiento — precisamente porque el galpón siguió
   recibiendo alimento para los ciclos 2602 y 2603 ⇒ `close_date` NULL.

⇒ `fecha_max = NULL` ⇒ **la grilla queda sin tope superior** y `fechas_universo` / `hist_alimento` /
`docs_por_fecha`, que filtran por **ubicación (granja+núcleo+galpón), no por lote**, absorben todos los
movimientos posteriores del galpón. El saldo se infla monótono (1.600 kg el 20/04 → 206.450 kg el 03/08)
porque suma los ingresos de los ciclos siguientes **sin su consumo**: el consumo sí está filtrado por el
predicado de convivencia de v10 (`consumo_galpon_por_fecha`), los ingresos no.

**La asimetría es el bug.** La fn ya sabe distinguir ciclos que no conviven — `lotes_ajenos` (v11) y
`corte_apertura` (v12) usan ese conocimiento para la **apertura** — pero nunca se aplicó al **cierre**.

### 0.3 Alcance medido (todas las empresas, detección directa sobre la fn)

Lotes cuya grilla muestra filas que caen **dentro del ciclo siguiente** del mismo galpón:

| empresa | lote | granja | galpón | estado | último seg | inicio ciclo siguiente | último día de la grilla | filas invasoras | saldo final |
|---|---|---|---|---|---|---|---|---|---|
| ItalcolEcuador | 2 (2601) | 40 | G0039 | abierto | 2026-04-20 | 2026-04-24 | 2026-08-03 | **31** | 206.450 |
| ItalcolEcuador | 86 (2603) | 43 | G0055 | abierto | 2026-07-18 | 2026-08-04 | 2026-08-04 | 1 | 6.700 |

**ItalcolPanama: 0 lotes afectados** (nunca encadena ciclos en el mismo galpón). El resto de los 140
lotes de engorde cierra bien. El lote 12 (Galpon-2) no aparece porque su saldo cruzó 0 y `saldo_close`
lo cortó el 2026-04-22 — pero lo hace **con saldo −9.020**, así que el síntoma que ve la operación en
Galpon-2 es distinto (apertura negativa arrastrada), no invasión de meses.

### 0.4 Por qué el ticket dice «ingreso del mes de julio»

Los ingresos de julio **existen y son correctos**: son del lote 2603, encasetado el 24/06 en ese mismo
galpón. Nadie cargó mal la fecha. Lo que está mal es a **qué lote** se los muestra la grilla.

⇒ La segunda parte del pedido (ventana de mes actual) **no arregla este caso** y no lo pretende: es una
medida de higiene independiente para que no se metan movimientos de meses cerrados. Ambas van, pero por
caminos separados.

---

## 1. Decisiones tomadas (confirmadas por el usuario)

| # | Decisión | Elegido |
|---|---|---|
| D1 | Alcance de la regla de mes | **Todo movimiento manual de inventario** (cualquier concepto, incluido el ajuste de stock) |
| D2 | Empresas | **Todas** — regla global de higiene, sin flag en `companies` |
| D3 | Tope superior | **Del día 1 del mes en curso hasta HOY** (bloquea también fechas futuras, hoy aceptadas en silencio) |
| D4 | Cierre de los lotes 2601 «Abierto» | **NO por migración.** Liquidar es una transacción de 5 pasos con reglas de negocio; se avisa a la operación para que los cierre por pantalla. El fix de la fn deja el reporte correcto igual |

---

## 2. Parte A — `fn_seguimiento_diario_engorde` v13: corte por ciclo siguiente

### Enfoque

CTE nuevo, complemento exacto de `corte_apertura` (v12):

```sql
corte_ciclo_siguiente AS (
    SELECT MIN(prim.fecha) - 1 AS hasta
    FROM (
        SELECT MIN(DATE(s2.fecha)) AS fecha
        FROM seguimiento_diario_aves_engorde s2
        JOIN lote_ave_engorde l2 ON l2.lote_ave_engorde_id = s2.lote_ave_engorde_id
                                AND l2.deleted_at IS NULL
        JOIN lote_info li ON TRUE
        JOIN rango_seg rs ON rs.last_seg IS NOT NULL
        WHERE l2.granja_id = li.granja_id
          AND COALESCE(TRIM(l2.nucleo_id), '') = li.nucleo_id
          AND COALESCE(TRIM(l2.galpon_id), '') = li.galpon_id
          AND l2.lote_ave_engorde_id <> p_lote_id
        GROUP BY l2.lote_ave_engorde_id
        HAVING MIN(DATE(s2.fecha)) > (SELECT last_seg FROM rango_seg)
    ) prim
)
```

y en `rango_final`:

```sql
fecha_max = LEAST(
    COALESCE(sc.close_date, CASE WHEN rs.estado = 'cerrado' THEN rs.last_seg END, cc.hasta),
    COALESCE(cc.hasta, 'infinity'::date)
)
```

### Invariantes que se preservan

- **`VENTA_AVES` sigue sin tope** (v7): una venta posterior al cierre por alimento conserva su fila y el
  saldo de aves cierra en 0. El corte solo toca la rama de inventario.
- **Lotes que CONVIVEN no se cortan**: `HAVING MIN(...) > last_seg` es falso cuando los ciclos se solapan
  (caso G0490 DOÑA MARIA de v10) ⇒ el saldo compartido no cambia.
- **Lotes ya cerrados no cambian**: su `fecha_max` (close_date o last_seg) ya es ≤ el inicio del ciclo
  siguiente en el 100 % de los casos medidos ⇒ `LEAST` es identidad.
- El corte NO depende de `estado_operativo_lote`: se deriva de **datos** (hay otro ciclo siguiéndose en
  el galpón), que es la única prueba fechada de que el galpón cambió de dueño.

### Entregables

- `backend/sql/fn_seguimiento_diario_engorde.sql` — v13 (el espejo se actualiza en el mismo commit).
- Migración EF idempotente `20260808xxxxxx_FnSeguimientoEngordeV13CorteCicloSiguiente` con el SQL
  verbatim (`.Fn.cs`), Designer clonado, ModelSnapshot intacto. `Down` = v12 verbatim.
- `Application/Calculos/CorteCicloEngordeCalculos.cs` (puro): `FechaMaxEfectiva(closeDate, estado,
  lastSeg, inicioCicloSiguiente)` — especificación ejecutable de la regla.
- Tests xUnit del cálculo puro (caso del ticket, convivencia, cerrado, sin ciclo siguiente, borde
  `inicio == last_seg + 1`).

### Validación (gate multipaís OBLIGATORIO)

1. `backend/sql/verificar_paridad_saldo_engorde.sql` — congelar línea base ANTES.
2. Aplicar la migración en local.
3. Mismo script DESPUÉS: **ItalcolPanama debe salir 0 en todas las columnas.**
4. Comparación fila a fila lote por lote (140 lotes, 2 empresas): listar exactamente qué lotes cambian
   y verificar que son solo los 2 detectados en §0.3.
5. `fn_cuadre_alimento_engorde` y `fn_cuadre_aves_engorde`: **0 descuadrados antes y después.**
6. `dotnet build` + `dotnet test`.

---

## 3. Parte B — Ventana de mes actual en los movimientos manuales de inventario

### Superficie exacta (las 5 puertas manuales del API)

| Endpoint | Campo | Uso |
|---|---|---|
| `POST /api/inventario-gestion/ingreso` | `FechaMovimiento` | Alta de ingreso |
| `POST /api/inventario-gestion/traslado` | `FechaMovimiento` | Alta de traslado |
| `PUT /api/inventario-gestion/ingresos/{id}/fecha` | `FechaMovimiento` | Edición de fecha |
| `PUT /api/inventario-gestion/traslados/{gid}/fecha` | `FechaMovimiento` | Edición de fecha |
| `PUT /api/inventario-gestion/stock/{id}` | `FechaIngreso` | Ajuste manual de stock |

### ⚠️ El gate va en el CONTROLLER, nunca en el service

`RegistrarIngresoAsync` / `RegistrarTrasladoAsync` / `RegistrarConsumoAsync` son **infraestructura
compartida**: los llaman la carga masiva (`MigracionService.AlimentoEngorde/.AlimentoPostura`), los cuatro
services de seguimiento diario (devoluciones al editar o borrar un registro) y `InventarioGastoService`.
Todos ellos escriben **con fecha histórica a propósito**. Poner la regla en el service rompe la carga
masiva y las devoluciones. El controller es la única frontera «esto lo tipeó una persona en pantalla».

`POST /consumo` **no se toca**: el front nunca lo llama; solo entra desde el seguimiento diario y la
carga masiva.

### Entregables

- `Application/Calculos/VentanaFechaMovimientoInventarioCalculos.cs` (puro):
  `EsFechaPermitida(DateTime? fecha, DateTime hoy)` → `null` permitido (el servidor pone «ahora»);
  permitido si `fecha.Date` ∈ [primer día del mes de `hoy`, `hoy`]. `MensajeFueraDeVentana(hoy)` para
  que el 400 diga siempre lo mismo.
- Controller: guarda en las 5 acciones → `400 { message }`.
- Front (`gestion-inventario-page`): `min`/`max` en los dos `input[type=date]` (alta de ingreso y de
  traslado) + validación previa al submit con el mismo mensaje; ídem en la edición de fecha del
  histórico si expone el control.
- Tests xUnit del cálculo puro (día 1, hoy, ayer, mes anterior, mañana, null, cambio de año).

---

## 4. Casos de prueba

### Parte A
- `fn_seguimiento_diario_engorde(2)` termina el **2026-04-20** con saldo **1.600 kg** (antes: 2026-08-03 con 206.450).
- `fn_seguimiento_diario_engorde(86)` pierde su única fila del 2026-08-04.
- `fn_seguimiento_diario_engorde(72)` y `(104)` (los ciclos siguientes del mismo galpón) **no cambian**.
- Los 30 lotes de ItalcolPanama **no cambian** (gate).
- `fn_reporte_diario_costos_engorde`, que se apoya en esta fn, se re-verifica en las dos empresas.

### Parte B
- Con `hoy = 2026-08-07`: `2026-08-01` OK · `2026-08-07` OK · `2026-07-31` **400** · `2026-08-08` **400** · `null` OK.
- Carga masiva de alimento con fechas de meses anteriores: **sigue funcionando** (no pasa por el controller).
- Editar/eliminar un seguimiento diario viejo (devolución de alimento): **sigue funcionando**.

---

## 5. Fuera de alcance (se avisa, no se toca)

- **Cerrar los lotes 2601 de Galpon-1 (id 2) y Galpon-2 (id 12)**: es una acción de operación por
  pantalla. Mientras sigan «Abierto» aparecerán como lotes activos en los selectores aunque el reporte
  ya salga bien.
- **Saldo de apertura negativo del lote 12 (−9.020 kg)**: síntoma distinto, con su propia causa;
  requiere auditoría de datos aparte antes de tocar nada.
