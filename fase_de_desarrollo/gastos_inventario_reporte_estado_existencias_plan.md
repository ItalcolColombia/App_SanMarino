# Plan — Gastos de inventario: reporte sin eliminados + hoja de existencias completas

**Fecha:** 2026-08-05
**Módulo:** `gastos-inventario` (transversal entre empresas; hoy solo ItalcolEcuador tiene datos, pero company 5 comparte catálogo)
**Origen:** novedad de validación del usuario final (Ecuador) + pedido de Moises de auditar tabla, export, filtro de eliminados y retorno a inventario.

---

## 1. Diagnóstico (verificado contra BD local `sanmarinoapplocal:5433`, dump tipo-prod)

| # | Hallazgo | Evidencia |
|---|---|---|
| 1 | ✅ **El retorno a inventario FUNCIONA** | 38 gastos `Eliminado`: **0 sin devolución**, **0 líneas descuadradas**, **0 cantidades descuadradas**. `DeleteAsync` llama a `RegistrarIngresoAsync` por línea (suma stock + movimiento `Ingreso`/`Entrada granja`), todo dentro de una transacción. |
| 2 | 🔴 **El export trae los eliminados** | `InventarioGastoService.ExportAsync` solo filtra estado si el request lo pide, y el front nunca lo pide ⇒ **46 filas Eliminado** mezcladas con 421 Activo. |
| 3 | 🔴 **El CSV descarta la columna Estado** | `InventarioGastoExportRowDto` YA trae `Estado`, `DeletedAt`, `DeletedByUserId`, pero `buildGastosExportCsv` escribe 12 columnas y ninguna es Estado ⇒ el eliminado es indistinguible en el archivo. |
| 4 | 🟠 **La UI no tiene filtro de estado** | `refresh()` y `exportExcel()` no mandan `estado`; `fn_inventario_gastos_search` con `p_estado NULL` devuelve todo. La tabla los muestra con badge (por eso "en pantalla se ven distintos"), el Excel no. |
| 5 | 🟠 **No es Excel: es CSV a mano** | `buildGastosExportCsv` + `Blob` + `text/csv`, saltándose el helper obligatorio `shared/utils/excel/exportar-tabla-excel.funcion.ts`. |
| 6 | 🟠 **`DeleteAsync` no valida empresa** | Busca el gasto solo por `id` (sin `CompanyId`). Hoy la transacción revierte por la validación interna de `RegistrarIngresoAsync`, pero esa validación se saltea si `_current.CompanyId <= 0`. Módulo transversal ⇒ fail-closed. |
| 7 | 🟠 **El reporte solo muestra lo consumido** | El export parte de `inventario_gasto_detalle`: un ítem sin consumo no existe en el archivo. El cliente necesita control de **todo** el inventario. |

**Dimensiones:** Ecuador 10 granjas × 131 ítems no-alimento activos = 1.310 filas máx. Conceptos con duplicado de capitalización (`Otros insumos` / `Otros Insumos`).

## 2. Decisiones tomadas (usuario, 2026-08-05)

- **D1 — Hoja de existencias:** *saldo actual + consumo del rango*. NO se reconstruye kardex histórico (sin saldo inicial al corte): se lee el stock vivo a nivel granja y se le agrega la columna "Consumido en el rango".
- **D2 — Eliminados:** **el reporte los excluye SIEMPRE**, sin importar el request. El historial de eliminados queda consultable **en pantalla** mediante un filtro de Estado nuevo (Activos por defecto). *Tensión declarada:* el cliente pidió por escrito "tener un historial de los eliminados" en el archivo; se resuelve en la UI, no en el Excel.

## 3. Alcance del cambio

### Backend

**B1 — `ExportAsync` excluye eliminados incondicionalmente**
`InventarioGastoService.ExportAsync`: `q.Where(g => g.Estado != "Eliminado")` fijo, antes de aplicar `req.Estado`. Documentado en el `///` del método y del endpoint. `SearchAsync` (tabla) NO cambia: sigue respetando `req.Estado` para que la UI pueda pedir el historial.

**B2 — `fn_inventario_gastos_existencias` (SQL) + endpoint `GET /api/inventario-gastos/existencias`**
La BD filtra y agrega (regla del repo: el backend orquesta, la BD filtra).

```
fn_inventario_gastos_existencias(
  p_company_id int, p_farm_id int, p_fecha_desde date, p_fecha_hasta date, p_concepto text)
RETURNS TABLE(farm_id int, granja_nombre text, item_inventario_ecuador_id int,
              codigo text, nombre text, tipo_item text, unidad text, concepto text,
              saldo_actual numeric, consumido_rango numeric, gastos_rango int)
```

- **Universo:** catálogo no-alimento **activo** de la empresa × granjas. Granjas = la del filtro, o (si no hay filtro) las granjas de la empresa **con inventario a nivel granja o con gastos** (evita cartesiano con granjas sin inventario).
- `saldo_actual` = `inventario_gestion_stock.quantity` (nucleo/galpón NULL), **`0` si no hay fila** ⇒ el ítem sin consumo aparece igual.
- `consumido_rango` / `gastos_rango` = `inventario_gasto_detalle` ⋈ `inventario_gasto` con **estado Activo** y fecha en rango (coherente con D2).
- Orden por concepto normalizado (agrupa `Otros insumos`/`Otros Insumos`), luego nombre. **El concepto se muestra tal cual está en el catálogo** — la normalización es solo clave de orden.
- ⚠️ Columnas **snake_case** en `RETURNS TABLE` (gotcha `SqlQueryRaw<T>`, ver memoria del módulo).
- Migración EF idempotente (`CREATE OR REPLACE`) + fuente en `backend/sql/`.

**B3 — `DeleteAsync` fail-closed por empresa**
Resolver `GetEffectiveCompanyIdAsync` y filtrar el gasto por `CompanyId`; empresa inválida ⇒ `UnauthorizedAccessException` (mismo patrón que `GetByIdAsync`).

**B4 — Cálculo puro + tests (gate CI)**
`Application/Calculos/InventarioGastoReporteCalculos.cs` (static, sin EF):
- `EsGastoEliminado(string?)` / `EsGastoActivo(string?)` — la regla del filtro, un solo dueño.
- `ClaveOrdenConcepto(string?)` — normaliza para ordenar/agrupar sin alterar lo mostrado.
- `EtiquetaConcepto(string?)` — `'(Sin concepto)'` cuando viene vacío.
Tests xUnit en `tests/ZooSanMarino.Application.Tests/InventarioGastoReporteCalculosTests.cs`.

### Frontend (patrón clean-code obligatorio: `models/` + `funciones/`)

**F1 — `models/inventario-gasto.model.ts`**: tipos del módulo extraídos del servicio (incl. `InventarioGastoExistenciaDto` nuevo), re-exportados desde el servicio para no romper imports.

**F2 — `funciones/exportar-gastos-inventario-excel.funcion.ts`** (PURA, sin `this`/DI): arma las 2 hojas y delega la descarga en `exportarMultiHojaExcel`:
- Hoja **`Consumos`**: Fecha · Granja · Núcleo · Galpón · Lote · Concepto · Código ítem · Nombre ítem · Tipo ítem · Cantidad · Unidad · Stock antes · Stock después · **Estado** · Observaciones · Registrado por · Fecha registro.
- Hoja **`Existencias`**: Granja · Concepto · Código · Ítem · Tipo · Unidad · **Saldo actual** · **Consumido en el rango** · Gastos en el rango.
- Título + subtítulos con los filtros aplicados y la leyenda "no incluye consumos eliminados".

**F3 — Página**: selector **Estado** (`Activos` por defecto / `Eliminados` / `Todos`) que aplica a la tabla; `exportExcel()` deja de armar CSV y llama a la función pura (2 requests en paralelo: `export` + `existencias`).

**F4 — Servicio**: método `existencias(...)`; el CSV se elimina.

## 4. Casos de prueba

**Backend (xUnit):** estados `Activo`/`Eliminado`/`eliminado`/`null`/`""`; orden de conceptos con duplicado de capitalización; etiqueta de concepto vacío.

**SQL (contra BD local):**
1. `fn_..._existencias(3, NULL, NULL, NULL, NULL)` → 10 granjas × 131 ítems, ningún ítem del catálogo ausente.
2. Ítem sin consumo → aparece con `consumido_rango = 0` y su saldo real.
3. Ítem con consumo en gasto **eliminado** → `consumido_rango` NO lo cuenta.
4. `saldo_actual` == `inventario_gestion_stock.quantity` fila a fila (0 diferencias).
5. Export con y sin fix: 467 → **421 filas** (46 eliminadas fuera).

**Smoke HTTP:** `GET /export` sin `estado` no devuelve ninguna fila Eliminado; `GET /existencias` responde el universo completo; `DELETE` desde otra empresa ⇒ 400 sin tocar datos.

**Multiempresa:** verificar que company 5 (catálogo compartido, 0 gastos) devuelve su propio universo sin fugar ítems de Ecuador.

## 5. Riesgos

- **Cartesiano catálogo × granjas** sin filtro de granja → acotado a granjas con inventario/gastos; 1.310 filas máx en Ecuador.
- **Refactor ≠ cambio de comportamiento**: `SearchAsync` y la aritmética del export no se tocan; el único cambio de resultado es la exclusión de eliminados (pedida) y las columnas nuevas.
- **Formato**: el archivo pasa de `.csv` a `.xlsx` real. Es lo que el cliente llama "reporte en Excel"; los consumidores del archivo verán 2 hojas en vez de un CSV plano.
