# `funciones/` — lógica pura del módulo traslados-aves

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de
dependencias). Cada archivo agrupa una "acción grande" del módulo —típicamente la lógica detrás de
un botón o de un bloque de UI— para que sea **fácil de encontrar, testear y reutilizar**.

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
| `construir-filas-edades-lote.funcion.ts` | `construirFilasEdadesLote`: arma las filas del bloque "Edades en el lote" (aves propias + cohortes recibidas) desde la respuesta de `GET /traslados/cohortes/{loteId}`. |

## Nota de reutilización

El bloque "Edades en el lote" (`components/edades-lote/`) es **transversal**: se muestra en
seguimiento diario de Levante y de Producción para cualquier empresa/país. Con 0 cohortes queda como
una línea informativa (la del propio lote), así que no hace falta gatearlo por flag de empresa.
Las **edades siempre las calcula el backend** — esta capa solo formatea y ordena para la tabla.
