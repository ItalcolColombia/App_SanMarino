# Gastos de inventario — elegir el rango de fechas del consumo (tabla + Excel)

**Fecha:** 2026-08-07
**Pedido del usuario (textual):**
> «Al momento de descargar pueda elegir de qué fecha hasta qué fecha necesito el consumo de
> productos, para así no tener que bajar todos los consumos realizados.»

---

## 1. Diagnóstico — dónde está hoy el corte

El backend **ya acepta el rango** en los tres endpoints del módulo; lo que falta es la UI que lo mande.

| Endpoint | Parámetros de fecha | Estado |
|---|---|---|
| `GET /api/inventario-gastos` (tabla) | `fechaDesde`, `fechaHasta` → `fn_inventario_gastos_search(..., {5}::date, {6}::date, ...)` | ✅ soportado, **la UI no los envía** |
| `GET /api/inventario-gastos/export` (hoja *Consumos*) | `Fecha >= FechaDesde.Date` / `Fecha <= FechaHasta.Date` (LINQ, columna `date`) | ✅ soportado, **la UI no los envía** |
| `GET /api/inventario-gastos/existencias` (hoja *Existencias*) | `p_fecha_desde` / `p_fecha_hasta` acotan `consumido_rango` y `gastos_rango` | ✅ soportado, **la UI no los envía** ⇒ hoy la columna «Consumido en el rango» es **histórico completo** |

El servicio Angular (`InventarioGastosService.buildParams`) **ya serializa** `fechaDesde`/`fechaHasta`, y
`FiltrosReporteGastos` (función de export) **ya tiene** los campos y los imprime en el subtítulo
(`describirFiltros` → «Rango: X a Y»). Nadie los llena. ⇒ **Cambio 100 % frontend, sin tocar backend ni BD.**

Dato clave para la correctitud: `inventario_gasto.fecha` es columna **`date`** (no `timestamptz`) ⇒ no
hay corrimiento de zona horaria; el `yyyy-MM-dd` del `<input type="date">` viaja literal y el filtro es
**inclusivo en ambos extremos**.

## 2. Enfoque arquitectónico

- La tarjeta **Filtros** gana dos campos `Desde` / `Hasta` (+ atajos de rango), al lado de `Estado`.
- El rango alimenta **la tabla y el Excel con el mismo valor** — lo que el usuario ve en pantalla es
  exactamente lo que baja (el pedido menciona las dos cosas).
- **Sin default:** rango vacío = «todos», que es el comportamiento de hoy ⇒ cero cambio para quien no
  lo use. Refactor ≠ cambio de comportamiento.
- La lógica de fechas (presets y validación) va a **`funciones/`** como función **pura** (recibe `hoy`
  por parámetro, sin `this` ni DI); la página queda de orquestador delgado, según el README del módulo.

## 3. Archivos

| Archivo | Cambio |
|---|---|
| `frontend/.../gastos-inventario/funciones/rango-fechas-gastos.funcion.ts` | **NUEVO**. Puro: `ymdLocal`, `calcularRangoPreset(preset, hoy)`, `validarRangoFechas(desde, hasta)`, `sufijoArchivoRango(desde, hasta)`. |
| `frontend/.../funciones/exportar-gastos-inventario-excel.funcion.ts` | El nombre del archivo incluye el rango (`gastos-inventario_2026-07-01_a_2026-07-31_YYYYMMDD.xlsx`); sin rango, nombre actual **idéntico**. Subtítulo de *Existencias* aclara a qué rango corresponde lo consumido. |
| `frontend/.../pages/gastos-inventario-page/gastos-inventario-page.component.ts` | Estado `fechaDesde`/`fechaHasta`, `onRangoChange()`, `aplicarPreset()`, `rangoInvalido`; `refresh()`, `exportExcel()` y `limpiarFiltros()` los propagan. |
| `...component.html` | Campos `Desde`/`Hasta` + chips de atajo + aviso de rango inválido + hint actualizado. |
| `...component.scss` | Estilo de los chips de atajo (`.rango-presets`). |

## 4. Reglas de negocio

1. **Rango vacío = todo** (comportamiento actual intacto).
2. **Inclusivo** en ambos extremos (`>=` desde, `<=` hasta) — coherente con backend y fn SQL.
3. **`Desde > Hasta` no consulta:** mensaje en pantalla y ni la tabla ni el Excel se disparan (evita
   viaje inútil y un Excel vacío que parezca «no hay datos»).
4. Solo `Desde` = «desde esa fecha hasta hoy»; solo `Hasta` = «todo hasta esa fecha». Ambos válidos.
5. El rango **también** acota `consumido_rango` / `gastos_rango` de la hoja *Existencias*; `saldo_actual`
   sigue siendo el saldo **a la fecha de descarga** (no es histórico) — el subtítulo lo dice explícito.
6. Los **eliminados siguen fuera del Excel** (el backend los excluye siempre): el rango no cambia eso.
7. Presets calculados en hora **local** del navegador (el usuario piensa en su calendario), sin `Date`
   dentro de la función pura: `hoy` entra por parámetro.

## 5. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| 1 | Sin rango (default) | Tabla y Excel idénticos a hoy; nombre de archivo `gastos-inventario_YYYYMMDD.xlsx` |
| 2 | Desde = Hasta = día con gastos | Solo los de ese día (extremos inclusivos) |
| 3 | Desde > Hasta | Aviso «La fecha Desde no puede ser mayor…»; no hay request ni descarga |
| 4 | Solo Desde | Todo lo registrado desde esa fecha en adelante |
| 5 | Solo Hasta | Todo lo registrado hasta esa fecha |
| 6 | Preset «Este mes» | `desde` = día 1 del mes, `hasta` = hoy |
| 7 | Preset «Mes anterior» | Primer y último día del mes previo (incluye 31/28/29) |
| 8 | Preset «Últimos 30 días» | `hoy − 29` … `hoy` (30 días contando hoy) |
| 9 | Excel con rango | Subtítulo «Rango: … a …» en las 2 hojas; `Consumido en el rango` = suma del rango, no histórico |
| 10 | Limpiar | Rango vacío y tabla completa otra vez |
| 11 | Rango sin gastos | Tabla «No hay registros…», Excel con hojas vacías y toast «0 consumo(s)» |

## 6. Validación

- `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget preexistente).
- Smoke en la pantalla real: aplicar rango → contar filas de la tabla → descargar y cruzar contra la BD
  (`SELECT count(*) FROM inventario_gasto WHERE fecha BETWEEN … AND … AND estado <> 'Eliminado'`).
- Backend y BD **no se tocan** ⇒ no hay migración ni gate multipaís que correr.
