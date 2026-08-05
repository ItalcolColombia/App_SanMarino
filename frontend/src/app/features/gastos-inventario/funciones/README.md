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
| `exportar-gastos-inventario-excel.funcion.ts` | `exportarGastosInventarioExcel`: arma y descarga el `.xlsx` de 2 hojas (Consumos + Existencias). `construirHojasReporteGastos` / `construirFilas*` / `describirFiltros` son puras y testeables sin descargar. |

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
