# Eliminar un registro de stock dejaba la tabla diaria alta — TK-2026-000183 (CAROLINA G1 lote 2602)

**Ticket:** `TK-2026-000183` (ItalcolEcuador, `EN_IMPLEMENTACION` desde el 27-ago-2026).
Reporte de operación: *«en granja CAROLINA galpón 1 lote 02 nos sale un saldo de alimento superior a
lo que se ha ingresado»* — el día 1 muestra **saldo 5.600 kg contra un ingreso de 2.880 kg**.

Dos trabajos distintos, los dos en este plan: **el dato del ticket** (una vez) y **el defecto que lo
produjo** (para que no vuelva a pasar).

---

## 1. El mecanismo, medido

En `G0057` (CAROLINA / GALPON 1) hay **dos** `Ingreso` de 2.880 kg del mismo ítem (4, preiniciador):

| mov | fecha declarada | remisión | qué es |
|---|---|---|---|
| 2021 | 02-abr (dentro de la ventana pre-encaset) | **56114** | real → entra como **apertura** |
| **4186** | 07-abr (día 1) | ninguna | **duplicado de la misma remisión** |
| 4187 | 30-abr | — | `EliminacionStock` de 2.880 kg |

`5.600 = 2.880 (apertura) + 2.880 (duplicado) − 160 (consumo del día)`.

🔴 **`EliminarStockAsync` borra la fila de stock y escribe un `EliminacionStock`, pero no baja la
tabla diaria.** `EliminacionStock` se espeja como `INV_OTRO` y `fn_seguimiento_diario_engorde` no lee
ese `tipo_evento`. Resultado: **el stock quedó bien y la tabla quedó alta para siempre**. Es el
espejo exacto del defecto de `EliminarIngresoAsync` (ahí el histórico se anulaba y el stock no se
devolvía); acá el stock se descuenta y el histórico sigue vivo.

**El control que cierra el diagnóstico:** el galpón 2 de la misma granja (lote 62, mismo encaset,
misma remisión) cierra en **0**; el 61 cierra con un residuo de **2.880 kg**, exactamente el
duplicado.

---

## 2. El parche del defecto (código)

### 2.1 `EliminarStockAsync` escribe el espejo de la tabla

Además del `EliminacionStock` (que se conserva: es la auditoría de la baja de stock), la eliminación
escribe un **`AjusteCuadreTablaSalida`** por los mismos kilos, con el **mismo timestamp**, que **no
toca stock** y que la fn v17 **sí** lee (`INV_AJUSTE_CUADRE_SALIDA`). Con eso el stock y la tabla
bajan juntos y el invariante `saldo == stock − movimientos posteriores` se conserva.

Por qué se reusa el tipo del cuadre y no se inventa uno nuevo: los `AjusteCuadreTabla*` **ya están en
producción** y la fn ya los lee en sus 5 CTE. Un tipo nuevo obligaría a una fn v18 y al gate
multipaís completo, para el mismo efecto.

Por qué la fecha es **ahora** y no la del ingreso: los kilos salen del stock hoy, y el par
`EliminacionStock` + ajuste comparte timestamp, así que el histórico los fecha el mismo día
(`fecha_operacion = (created_at AT TIME ZONE 'UTC')::DATE`). El invariante cierra en los dos casos:
si el movimiento cae dentro de la grilla, baja el saldo; si cae después del último seguimiento, es un
«movimiento posterior» y baja el esperado en la misma cantidad.

### 2.2 El espejo C# del `tipo_evento` estaba desalineado

`TipoEventoInventarioCalculos` dice ser *«el espejo en C# de la función SQL
`fn_tipo_evento_inventario`»*, pero no conoce los dos tipos que entraron con la v17 (25-ago): los
manda a `INV_OTRO`, así que `AfectaSaldoAlimentoEngorde` devuelve `false` y
**`RefrescarSaldoAlimentoEngordeAsync` no recalcula la columna persistida**. Su propio doc-comment
prometía que un test lo delataría; el test existía y nadie le agregó el caso.

Se sincroniza el mapeo (los dos tipos nuevos → `INV_AJUSTE_CUADRE_*`, y sí afectan el saldo) y se
agregan los casos al test. Sin esto, el ajuste que escribe §2.1 **no refrescaría** la columna.

### 2.3 Etiquetas

`AjusteCuadreTablaEntrada/Salida` no están en `MapTipoOperacionLabel`, así que en la grilla de
movimientos se ven con el nombre crudo del tipo. Se agregan a la etiqueta y a su inversa.

**Archivos:**
- `Application/Calculos/TipoEventoInventarioCalculos.cs`
- `Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.StockMutacion.cs`
- `Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.Traslado.cs` (etiquetas)
- `tests/ZooSanMarino.Application.Tests/TipoEventoInventarioCalculosTests.cs`

---

## 3. La corrección del dato (migración `20260901140000`)

El lote está **Cerrado** y `fn_seguimiento_diario_engorde` devuelve la **copia congelada** mientras
exista sin anular (la fn arranca con un `UNION ALL` contra `liquidacion_lote_engorde_congelada_fila`).
Por eso el dato vive en **tres** superficies y hay que corregir las tres:

| # | Tabla | Qué cambia |
|---|---|---|
| 1 | `lote_registro_historico_unificado` | el ingreso duplicado (`origen_id 4186`) → `anulado = true` |
| 2 | `seguimiento_diario_aves_engorde` | `saldo_alimento_kg` de los 52 días del lote 61 → **−2.880** |
| 3 | `liquidacion_lote_engorde_congelada_fila` + su header | ídem los 52 días, `ingreso_alimento_kg` del día 1 → **0**, header `saldo_alimento_kg` 2.880 → **0** |

### Por qué corrección quirúrgica y no «reabrir y volver a congelar»

Medido en transacción revertida: anulando el duplicado, el cálculo **vivo** da día 1 = **2.720** con
ingreso **0** y apertura **2.880 (doc 56114)**, y el lote **cierra en 0** — idéntico al gemelo 62.
Comparado fila a fila contra la congelada v13: **52 filas difieren solo en `saldo_alimento_kg`, todas
por exactamente 2.880**, una sola en `ingreso_alimento_kg`, y **cero** diferencias en consumo, aves,
mortalidad, documento, tipo de alimento, despachos y pesos.

Pero la fn de hoy también numera distinto el `edad_dia` (el 07-abr pasa de día 1 a día 2, por el
arreglo de la hora de llegada). **Recongelar traería ese cambio de edades**, que no tiene nada que
ver con este ticket. La corrección quirúrgica reproduce **exactamente** los mismos kilos que el
recálculo vivo, sin importar el cambio ajeno.

### Reglas de la migración

- **Data-only**, `DO` en plpgsql, **idempotente por marca**: escribe
  `metadata->'correccionTk2026000183'` en el header de la congelada y no vuelve a entrar si ya está.
- **Localiza por atributos, no por ids**: galpón `G0057` + ítem + 2.880 kg + `fecha_operacion`
  07-abr + `Ingreso` sin referencia + su `EliminacionStock` pareja. Si no encuentra la firma exacta,
  `RAISE NOTICE` y no toca nada (fail-safe).
- **No toca el stock**: ya está bien — la eliminación descontó esos kilos en su momento.
- **No toca `metadata` de las filas**: el `ingresoAlimentoKg: 3600` que vive ahí es la carga del
  seguimiento, no la columna que pinta la grilla (que dice 2.880). No se toca lo que no se entiende.
- **`checksum` se conserva**: es el md5 de las filas *tal como las devolvió la fn al congelar*, y no
  es reproducible sobre las filas guardadas. La corrección queda registrada en `metadata`, que es
  honesto; recalcularlo con otra fórmula sería fingir integridad.
- `Down()` revierte los cuatro pasos exactamente y borra la marca.

### Lo que NO entra (y por qué)

El detector encuentra **13 pares** con la misma firma (11 movimientos en Ecuador + 1 en Panamá
G0483), todos con el histórico sin anular. **Solo se corrige el del ticket.** En particular:

- **G0058 (lote 62) no se toca**: ahí el mismo par existe, pero es el **único** ingreso del día 1 —
  no hay duplicado — y el lote **cierra en 0**, o sea que esos kilos fueron alimento real consumido.
  Anularlo lo dejaría en **−2.880**.
- **G0036 (80.740 kg en 6 movimientos)**: dos traen la remisión 54159, así que pueden ser carga
  histórica legítima. Sin confirmar las remisiones con operación, tocarlos es adivinar.

---

## 4. Casos de prueba

**xUnit (`TipoEventoInventarioCalculosTests`):**
- `AjusteCuadreTablaEntrada` → `INV_AJUSTE_CUADRE_ENTRADA`; `AjusteCuadreTablaSalida` → `INV_AJUSTE_CUADRE_SALIDA` (y con distinta capitalización, como el `ILIKE` de la fn).
- Los dos **afectan** el saldo de alimento de engorde.
- Regresión: `AjusteStock`, `EliminacionStock` y `Consumo` **siguen** sin afectarlo.

**Migración (transacción revertida sobre la copia de producción):**
- `Up()` deja el día 1 en **2.720 / ingreso 0** y el cierre en **0**, en las tres superficies.
- `Up()` dos veces = la segunda no toca ninguna fila.
- `Down()` devuelve exactamente los valores previos (5.600 / 2.880 / 2.880).
- El gemelo **lote 62 no se mueve** ni un kilo, y el ciclo vigente del galpón (lote 2604) tampoco.
- `verificar_cuadre_alimento_engorde.sql` antes y después: sin galpones nuevos descuadrados.
