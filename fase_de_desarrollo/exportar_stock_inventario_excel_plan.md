# Descargar Excel del stock de TODAS las granjas — Gestión de Inventario

**Fecha:** 2026-08-05
**Módulo:** `frontend/src/app/features/gestion-inventario` (pestaña **Stock**)
**Origen:** requerimiento de operación —
> *«SOLICITO SU AYUDA EN PODER DESCARGAR EN EXCEL EL STOCK QUE TENEMOS EN CADA BODEGA PARA PODER REALIZAR UN COMPARATIVO»*

> **Revisión 2 (mismo día).** El usuario pidió que el archivo traiga **todos los conceptos**
> repartidos en **dos hojas** — `Alimento` y `Otros conceptos` — en vez de una sola hoja plana.
> Consecuencia: el export **también deja de aplicar el filtro de concepto** de la pantalla (las dos
> hojas YA son la partición por concepto), y el botón pasa a llamarse *«Descargar Excel (todo el
> stock)»*. Todo lo demás del diseño se mantiene.

---

## 1. Qué se pide (traducción literal del requerimiento)

Un botón en la pestaña **Stock** que descargue un `.xlsx` con el stock disponible de **todas las
granjas/bodegas asignadas al usuario**, respetando el modelo del módulo:

- **Alimento** → la fila trae su **Núcleo + Galpón** correspondiente.
- **Otros conceptos** → la fila es a **nivel granja** (sin núcleo/galpón).

Hoy la pantalla ya muestra exactamente eso, pero el usuario tiene que ir granja por granja
(o dejar el filtro en «Todas») y **no hay forma de bajarlo a Excel**: el único export del módulo es
el CSV del **Histórico de movimientos**, que es otra cosa (movimientos, no saldos).

## 2. Enfoque arquitectónico

**Front-only. Cero cambios de backend, cero migraciones.**

`GET /api/inventario-gestion/stock` **ya acepta `farmId` opcional**
(`InventarioGestionController.GetStock` → `InventarioGestionService.GetStockAsync`, línea 332):
cuando no llega `farmId`, la consulta devuelve el stock de **todas las granjas asignadas al usuario**
dentro de la empresa y el país activos (`allowedFarmIds.Contains(x.FarmId)`, fail-closed si no
resuelve empresa o no hay granjas). Es decir: el dato multi-granja ya existe y ya está scopeado por
seguridad; lo único que falta es **pedirlo sin filtro de granja y volcarlo a `.xlsx`**.

Decisiones:

| Decisión | Por qué |
|---|---|
| El export **ignora** el filtro de Granja/Núcleo/Galpón y siempre consulta **todas las asignadas** | Es literalmente lo pedido («ahí me debe traer todas las granjas al descargar el excel»). El botón lo dice en su etiqueta y el Excel lo deja escrito en su cabecera. |
| El export **también ignora el filtro de Concepto** *(rev. 2)* | El pedido es «que descargue todos los conceptos… una hoja alimento y la otra otros conceptos». Respetar el filtro dejaría una de las dos hojas vacía por accidente. Queda escrito en las cabeceras de ambas hojas. |
| El export **sí respeta Buscar ítem** | Es una búsqueda de texto que el usuario acaba de escribir a propósito (el campo arranca vacío), no un filtro de navegación como la granja. Se anota en la cabecera del archivo. |
| **Consulta propia** (`getStock({})`), no reutiliza `stockList` | `stockList` puede venir filtrado por una sola granja o un solo concepto; reutilizarlo entregaría un Excel incompleto sin avisar. |
| **Dos hojas** (`Alimento` / `Otros conceptos`), con las **mismas columnas que la tabla en pantalla** (menos Acciones) *(rev. 2)* | Es la partición natural del módulo: alimento se maneja por ubicación y el resto a nivel granja. Sin hojas de resumen ni agregados que nadie pidió. |
| La partición se decide por **concepto**, no por «tiene galpón» | Un alimento de una empresa que lo maneja a nivel granja (Colombia, o `manejaAlimentoPorGalpon=false`) sigue siendo alimento aunque venga sin ubicación. Comparación **insensible a mayúsculas**: el catálogo tiene `alimento` y `Alimento`. |
| La hoja `Otros conceptos` **omite** Núcleo/Galpón, salvo que algún registro los traiga | Esos ítems son siempre a nivel granja ⇒ columnas vacías = ruido. El escape defensivo evita ocultar datos si alguna vez llegara una fila con ubicación. |
| Columnas Núcleo/Galpón **se omiten en Colombia** | Espejo exacto de `stockShowNucleoGalpon`: en Colombia el inventario es a nivel granja y esas columnas irían vacías en el 100 % de las filas. |
| Se usa `exportarMultiHojaExcel` de `shared/utils/excel/` | Primitiva **obligatoria** del sistema de diseño (CLAUDE.md). Prohibido reintroducir `XLSX.utils.book_new` inline. |
| Los avisos siguen por `openAlertModal` del componente | Es el patrón vigente del módulo (lo usa el export CSV del histórico y las 20 validaciones restantes). No es `alert()` nativo, así que no viola la regla; mezclar `ToastService` solo aquí dejaría dos lenguajes de notificación en la misma pantalla. |

## 3. Archivos

### Nuevos

| Archivo | Contenido |
|---|---|
| `frontend/src/app/features/gestion-inventario/funciones/README.md` | Convención de la carpeta (calcada del canónico `movimientos-pollo-engorde/funciones/README.md`). |
| `frontend/src/app/features/gestion-inventario/funciones/exportar-stock-excel.funcion.ts` | Partición por concepto + armado de las 2 hojas + descarga. Todo puro salvo la descarga: sin `this`, sin DI, sin HTTP. |
| `frontend/src/app/features/gestion-inventario/funciones/exportar-stock-excel.funcion.spec.ts` | Specs Jasmine de la parte pura. |

### Modificados

| Archivo | Cambio |
|---|---|
| `…/pages/gestion-inventario-page/gestion-inventario-page.component.ts` | `exportandoStock` (flag) + `descargarStockExcel()`: consulta sin `farmId` ni `itemType`, delega en la función pura, maneja vacío/error. |
| `…/gestion-inventario-page.component.html` | Cabecera de la tarjeta **Stock** con el botón «Descargar Excel (todo el stock)» + nota en el `hint` de filtros. |
| `…/gestion-inventario-page.component.scss` | Clases de la cabecera de la tarjeta Stock (mismo look que la del Histórico). |

**BD / SQL / migraciones: ninguno.**

## 4. Contrato de las funciones puras

```ts
esFilaAlimento(row): boolean                                   // concepto, insensible a mayúsculas
particionarStockPorConcepto(rows): { alimento, otros }         // conserva el orden del backend
cabecerasStockExcel(incluirUbicacion): ExcelCell[]
construirFilasStockExcel(rows, { incluirUbicacion }): ExcelCell[][]
construirHojasStockExcel(rows, meta): HojaExcel[]              // las 2 hojas, sin descargar
exportarStockExcel(rows, meta): void                           // arma + descarga
```

**Hojas del archivo:**

| Hoja | Contenido | Columnas |
|---|---|---|
| `Alimento` | Filas con concepto `alimento`/`Alimento` | Granja · **Núcleo · Galpón** · Código · Producto · Tipo · Fecha · Cantidad · Unidad |
| `Otros conceptos` | Todo el resto (desinfectante, medicamento, vacuna, gas…) | Granja · Código · Producto · Tipo · Fecha · Cantidad · Unidad |

Cada hoja lleva su título, las líneas de contexto comunes del archivo y su propio
`Registros: N · Granjas con existencias: M`. La hoja que quede vacía muestra
`Sin registros para este grupo.` para no romper la estructura del archivo.

Reglas de mapeo, fila por fila:

| Columna | Valor |
|---|---|
| Granja | `granjaNombre ?? String(farmId)` |
| Núcleo *(solo si `incluirUbicacion`)* | `nucleoNombre ?? nucleoId ?? '—'` |
| Galpón *(solo si `incluirUbicacion`)* | `galponNombre ?? galponId ?? '—'` |
| Código | `itemCodigo` |
| Producto | `itemNombre` |
| Tipo | `itemType` (concepto del catálogo) |
| Fecha de ingreso | `fechaIngreso` → `dd/mm/aaaa` sin corrimiento de zona (`fechaCortaSinTz` de `shared/utils/format`), `—` si es null |
| Cantidad | `quantity` **numérico** (no texto: el Excel tiene que poder sumar para el comparativo) |
| Unidad | `unit` |

## 5. Reglas de negocio preservadas

1. **Scope de seguridad intacto:** el listado sale del mismo endpoint que ya filtra por empresa
   efectiva + país + granjas asignadas. El front no puede ampliar el alcance; si el usuario tiene
   una sola granja asignada, el Excel trae esa sola (y la cabecera lo dice).
2. **No se inventa el nivel de manejo:** núcleo/galpón se muestran tal como vienen del backend
   (que ya aplica `AlimentoNivelResolver`: por galpón vs por granja, configurable empresa/granja).
   El front no vuelve a decidir «esto es alimento» por su cuenta.
3. **Paridad con la pantalla:** se exportan las mismas filas que la tabla, incluidas las de
   cantidad 0 (hoy la grilla también las muestra). Nada se oculta en silencio.
4. **Orden:** el que ya devuelve el backend (granja → núcleo → galpón → producto).

## 6. Casos de prueba

**Unitarios (`exportar-stock-excel.funcion.spec.ts`) — 25 specs:**

| Grupo | Casos |
|---|---|
| `esFilaAlimento` | `Alimento` / `alimento` / ` ALIMENTO ` son alimento · `Medicamento`, `Otros insumos`, `''` no lo son · clasifica por concepto y NO por tener galpón |
| `particionarStockPorConcepto` | separa los dos grupos conservando el orden del backend · lista vacía ⇒ dos grupos vacíos |
| `construirHojasStockExcel` | siempre 2 hojas en orden Alimento → Otros · Alimento con Núcleo/Galpón, Otros sin ellos (7 columnas) · cada fila cae en UNA sola hoja (sin duplicar ni perder) · ubicación en «Otros» si algún registro la trae · hoja vacía ⇒ «Sin registros» + `Registros: 0` · resumen por hoja · contexto repetido en ambas · Colombia ⇒ sin ubicación en ninguna |
| `construirFilasStockExcel` | núcleo/galpón en alimento · `—` en no-alimento · fallback granja→farmId y nombre→id · fecha sin corrimiento de zona · fecha nula ⇒ `—` · cantidad **numérica** · lista vacía · orden preservado |
| `cabecerasStockExcel` | largo alineado con las filas, con y sin ubicación |

**Manuales (smoke UI):**

| # | Escenario | Esperado |
|---|---|---|
| A | Sin filtros → Descargar | 2 hojas con todo el stock de todas las granjas |
| B | **Granja = una sola + Concepto = Alimento** → Descargar | El Excel **igual trae todo**; las cabeceras dicen que no aplican los filtros de pantalla |
| C | Búsqueda que solo matchea no-alimento | Hoja `Alimento` con «Sin registros»; hoja `Otros conceptos` con los resultados |
| D | Búsqueda sin resultados | Modal «Sin datos», **no** se descarga archivo |
| E | Doble clic en el botón | Deshabilitado mientras genera; una sola descarga |
| F | Empresa Colombia | Ninguna hoja con columnas Núcleo/Galpón |

## 7. Validación

- `cd frontend && yarn build` → 0 errores (único warning admitido: *bundle budget* preexistente).
- `cd frontend && yarn test` → specs nuevos verdes.
- Smoke UI A–F contra el backend local.
- Sin procesos huérfanos al terminar.

## 8. Fuera de alcance (explícito)

- No se toca el backend ni el contrato del endpoint.
- No se agregan hojas de resumen/consolidado por granja (nadie las pidió; el archivo plano es
  pivoteable en Excel).
- No se migra el export CSV del **Histórico** a `.xlsx` — es otro tablero y otro requerimiento.
