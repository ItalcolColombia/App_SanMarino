# Descargar Excel del stock de TODAS las granjas — Gestión de Inventario

**Fecha:** 2026-08-05
**Módulo:** `frontend/src/app/features/gestion-inventario` (pestaña **Stock**)
**Origen:** requerimiento de operación —
> *«SOLICITO SU AYUDA EN PODER DESCARGAR EN EXCEL EL STOCK QUE TENEMOS EN CADA BODEGA PARA PODER REALIZAR UN COMPARATIVO»*

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
| El export **sí respeta** Concepto y Buscar ítem | Son filtros de **ítem**, no de ubicación. Permiten bajar «solo Alimento» para el comparativo sin romper el «todas las granjas». Quedan escritos en la cabecera del archivo. |
| **Consulta propia** (`getStock({})`), no reutiliza `stockList` | `stockList` puede venir filtrado por una sola granja; reutilizarlo entregaría un Excel incompleto sin avisar. |
| **Una sola hoja**, con las **mismas columnas que la tabla en pantalla** (menos Acciones) | El pedido es «descargar en excel **lo que está en la aplicación**». Sin agregados ni hojas extra que nadie pidió. |
| Columnas Núcleo/Galpón **se omiten en Colombia** | Espejo exacto de `stockShowNucleoGalpon`: en Colombia el inventario es a nivel granja y esas columnas irían vacías en el 100 % de las filas. |
| Se usa `exportarTablaExcel` de `shared/utils/excel/` | Primitiva **obligatoria** del sistema de diseño (CLAUDE.md). Prohibido reintroducir `XLSX.utils.book_new` inline. |
| Los avisos siguen por `openAlertModal` del componente | Es el patrón vigente del módulo (lo usa el export CSV del histórico y las 20 validaciones restantes). No es `alert()` nativo, así que no viola la regla; mezclar `ToastService` solo aquí dejaría dos lenguajes de notificación en la misma pantalla. |

## 3. Archivos

### Nuevos

| Archivo | Contenido |
|---|---|
| `frontend/src/app/features/gestion-inventario/funciones/README.md` | Convención de la carpeta (calcada del canónico `movimientos-pollo-engorde/funciones/README.md`). |
| `frontend/src/app/features/gestion-inventario/funciones/exportar-stock-excel.funcion.ts` | `construirFilasStockExcel` (pura, testeable) + `cabecerasStockExcel` + `exportarStockExcel` (arma y descarga). Sin `this`, sin DI, sin HTTP. |
| `frontend/src/app/features/gestion-inventario/funciones/exportar-stock-excel.funcion.spec.ts` | Specs Jasmine de la parte pura. |

### Modificados

| Archivo | Cambio |
|---|---|
| `…/pages/gestion-inventario-page/gestion-inventario-page.component.ts` | `exportandoStock` (flag) + `exportarStockExcel()`: consulta sin `farmId`, delega en la función pura, maneja vacío/error. |
| `…/gestion-inventario-page.component.html` | Cabecera de la tarjeta **Stock** con el botón «Descargar Excel (todas las granjas)» + nota en el `hint` de filtros. |
| `…/gestion-inventario-page.component.scss` | Clases de la cabecera de la tarjeta Stock (mismo look que la del Histórico). |

**BD / SQL / migraciones: ninguno.**

## 4. Contrato de la función pura

```ts
construirFilasStockExcel(
  rows: InventarioGestionStockDto[],
  opts: { incluirUbicacion: boolean }
): ExcelCell[][]
```

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

**Unitarios (`exportar-stock-excel.funcion.spec.ts`):**

| # | Caso | Esperado |
|---|---|---|
| 1 | Fila de alimento con núcleo y galpón | Trae ambos nombres en sus columnas |
| 2 | Fila de otro concepto (núcleo/galpón `null`) | `—` en Núcleo y Galpón; el resto igual |
| 3 | `granjaNombre` nulo | Cae al `farmId` como texto |
| 4 | `nucleoNombre` nulo pero `nucleoId` presente | Muestra el id (mismo fallback que la grilla) |
| 5 | `incluirUbicacion: false` (Colombia) | 7 columnas: sin Núcleo ni Galpón |
| 6 | `fechaIngreso` con offset (`…T19:00:00-05:00`) | Fecha del día intencional, **sin** corrimiento |
| 7 | `fechaIngreso` null | `—` |
| 8 | `quantity` | Sale **number**, no string (sumable en Excel) |
| 9 | Lista vacía | `[]` (el componente avisa, no descarga archivo vacío) |
| 10 | Cabeceras | Con y sin ubicación coinciden en largo con las filas |

**Manuales (smoke UI):**

| # | Escenario | Esperado |
|---|---|---|
| A | Granja = «Todas», sin concepto → Descargar | `.xlsx` con todas las granjas asignadas; alimento con galpón, otros con `—` |
| B | **Granja = una sola** (p. ej. BODEGA PRINCIAL KM 86) → Descargar | El Excel **igual trae todas** las granjas; la cabecera dice «Granjas: todas las asignadas» |
| C | Concepto = Alimento → Descargar | Solo alimento, todas las granjas, con núcleo/galpón; cabecera lo indica |
| D | Búsqueda que no matchea nada | Modal «Sin datos», **no** se descarga archivo |
| E | Doble clic en el botón | Queda deshabilitado mientras genera; una sola descarga |
| F | Empresa Colombia | Sin columnas Núcleo/Galpón |

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
