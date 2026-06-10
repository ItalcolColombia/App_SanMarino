# `funciones/` — lógica pura del módulo movimientos-pollo-engorde

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de
dependencias). Cada archivo agrupa una "acción grande" del módulo —típicamente la lógica detrás de
un botón— para que sea **fácil de encontrar, testear y reutilizar**.

## Convención

- **Un archivo por concern**, nombrado `<accion>.funcion.ts`.
- Reciben datos por parámetro y devuelven un resultado. **No** tocan `service`, `toast`, ni estado
  del componente.
- Los componentes (`pages/`, `components/`) quedan como **orquestadores delgados**: arman los
  parámetros, llaman la función y manejan estado/HTTP/UI.
- Los tipos compartidos viven en [`../models/`](../models), no aquí (evita imports circulares).

## Índice

| Archivo | Qué hace |
|---|---|
| `formato.funcion.ts` | `formatearNumero`, `fechaCorta`, `ymdToIsoUtcNoon` (helpers compartidos). |
| `agrupar-despachos.funcion.ts` | `construirFilasTabla`: agrupa ventas por despacho para el listado. |
| `exportar-ventas-excel.funcion.ts` | `exportarVentasExcel`: arma y descarga el `.xlsx` de ventas. |
| `mapear-movimiento-dto.funcion.ts` | `buildCreateDto` / `buildUpdateDto` / `buildVentaGranjaDespachoDto`. |
| `prorateo-peso.funcion.ts` | `calcularProrateoPreview` / `calcularProrateoTotales`. |

## Nota multi-país

Esta capa es la **base común** para la organización multi-país. Los modales por país
(p. ej. `modal-...-ecuador`, `modal-...-panama`) deben **reutilizar** estas funciones y los
`models/`, y limitarse a la lógica de presentación específica del país (qué campos se muestran,
validaciones locales, etc.). Si una regla es común a todos los países, va aquí; si es propia de un
país, va en el componente/modal de ese país.
