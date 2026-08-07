# `funciones/` — lógica pura del módulo gastos-inventario

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de dependencias).
Cada archivo agrupa una "acción grande" del módulo —típicamente la lógica detrás de un botón— para
que sea **fácil de encontrar, testear y reutilizar**.

## Convención

- **Un archivo por concern**, nombrado `<accion>.funcion.ts`.
- Reciben datos por parámetro y devuelven un resultado. **No** tocan `service`, `toast`, ni estado
  del componente.
- La página (`pages/`) queda como **orquestador delgado**: arma los parámetros, llama la función y
  maneja estado/HTTP/UI.
- Los tipos compartidos viven en [`../models/`](../models), no aquí (evita imports circulares).

## Índice

| Archivo | Qué hace |
|---|---|
| `exportar-gastos-inventario-excel.funcion.ts` | `exportarGastosInventarioExcel`: arma y descarga el `.xlsx` de 2 hojas (Consumos + Existencias). `construirHojasReporteGastos` / `construirFilas*` / `describirFiltros` / `nombreBaseReporteGastos` son puras y testeables sin descargar. |
| `rango-fechas-gastos.funcion.ts` | Rango de fechas del consumo: `calcularRangoPreset` (atajos Este mes / Mes anterior / Últimos 30 / Este año), `validarRangoFechas` y `sufijoArchivoRango`. El `hoy` entra **por parámetro** (nada de `new Date()` adentro) para poder testearlas. |

## Nota multi-país / multi-empresa

El módulo es **transversal entre empresas** (hoy con datos en Ecuador; otras compañías comparten el
mismo catálogo). Las funciones de esta capa no deben decidir por empresa ni por país: reciben las
filas ya resueltas por el backend, que es quien acota por empresa efectiva.

## Reglas que este reporte NO puede romper

- **Un gasto eliminado NO es consumo.** Al eliminarlo, el stock vuelve al inventario
  (`InventarioGastoService.DeleteAsync` → `RegistrarIngresoAsync`), así que contarlo en el reporte
  duplicaría el gasto. El filtro es del backend (`ExportAsync`), no de acá — no lo reimplementes en
  el front ni lo "des vuelta" con un parámetro.
- **La hoja Existencias muestra el catálogo completo**, no solo lo consumido: un ítem sin movimiento
  aparece con `consumidoRango = 0` y su saldo. Filtrar esas filas rompería el control de inventario
  que motivó la hoja.
- **El rango de fechas es del backend, no del front.** `fechaDesde`/`fechaHasta` viajan a
  `search`, `export` y `existencias` (columna `date`, extremos inclusivos): no recortes las filas ya
  descargadas en el front, porque la hoja Existencias agrega en la BD (`fn_inventario_gastos_existencias`)
  y quedaría descuadrada contra la de Consumos.
- **Rango vacío = todo el histórico**, que es el comportamiento previo del módulo. El nombre del
  archivo solo lleva sufijo de período cuando hay rango, para no cambiar la salida de quien no lo usa.
