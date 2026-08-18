# `funciones/` — lógica pura del módulo gestion-inventario

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de
dependencias). Cada archivo agrupa una "acción grande" del módulo —típicamente la lógica detrás de
un botón— para que sea **fácil de encontrar, testear y reutilizar**.

## Convención

- **Un archivo por concern**, nombrado `<accion>.funcion.ts`.
- Reciben datos por parámetro y devuelven un resultado. **No** tocan `service`, `toast`, ni estado
  del componente (la descarga del `.xlsx` es el único efecto esperado, y va delegada al helper
  compartido `shared/utils/excel/exportar-tabla-excel.funcion.ts`).
- Los componentes (`pages/`) quedan como **orquestadores delgados**: consultan el API, arman los
  parámetros, llaman la función y manejan estado/UI.
- Los tipos de dominio se importan desde [`../services/gestion-inventario.service.ts`](../services);
  si algún día un tipo vive inline en un componente, se mueve antes a `../models/` (evita imports
  circulares).

## Índice

| Archivo | Qué hace |
|---|---|
| `exportar-stock-excel.funcion.ts` | `construirFilasStockExcel` / `cabecerasStockExcel` / `exportarStockExcel`: arma y descarga el `.xlsx` del stock de **todas las granjas asignadas** (alimento con su galpón, otros conceptos a nivel granja). |
| `ventana-fecha-movimiento.funcion.ts` | `ventanaFechaMovimiento` / `esFechaMovimientoPermitida` / `mensajeFechaFueraDeVentana`: ventana de fechas de los movimientos cargados **a mano** (día 1 del mes en curso → hoy). Espejo de `VentanaFechaMovimientoInventarioCalculos` del backend, que es el que manda; acá acota el datepicker y avisa antes del request. |
| `ventana-fecha-movimiento.funcion.ts` (D4) | `extremosFechaIngreso` / `esFechaIngresoOfrecible` / `mensajeFechaIngresoFueraDeVentana` / `hintFechaIngreso`: la ventana de las dos puertas de **ingreso**, que además admiten el alimento llegado antes de un encasetamiento del galpón. **No replican la regla**: el encaset que manda depende de la fecha elegida, así que un espejo en TS rechazaría fechas que el backend acepta. Sólo aplican los extremos que informa `GET /inventario-gestion/ventana-fecha-ingreso` y dejan el rechazo fino al controller. |

## Referencia

El patrón canónico del repo es
[`movimientos-pollo-engorde/funciones/`](../../movimientos-pollo-engorde/funciones/README.md).
