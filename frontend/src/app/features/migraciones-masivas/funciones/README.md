# `funciones/` — lógica pura del módulo migraciones-masivas

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de dependencias).
Mismo patrón que `movimientos-pollo-engorde/funciones/` (la referencia canónica del repo): un
archivo por concern, para que sea fácil de encontrar, testear y reutilizar.

## Convención

- **Un archivo por concern**, nombrado `<accion>.funcion.ts`.
- Reciben datos por parámetro y devuelven un resultado. **No** tocan `service`, `toast` ni estado
  del componente.
- Los componentes (`components/`, `pages/`) quedan como **orquestadores delgados**: arman los
  parámetros, llaman la función y manejan estado/HTTP/UI.
- Los tipos del contrato (backend) viven en [`../models/migracion.model.ts`](../models/migracion.model.ts).
  Los tipos de presentación que solo usa esta capa (p. ej. `ResumenItem`, `EstadoBadge`) se
  exportan desde el propio archivo `.funcion.ts` y el componente los importa desde ahí — mismo
  patrón que `ProrateoRow` en `movimientos-pollo-engorde/funciones/prorateo-peso.funcion.ts`.

## Índice

| Archivo | Qué hace |
|---|---|
| `validar-archivo-cliente.funcion.ts` | `validarArchivoCliente`: valida extensión `.xlsx` y tamaño antes de llamar al servidor. |
| `construir-resumen-resultado.funcion.ts` | `construirResumenResultado` (tarjetas totales/procesadas/omitidas/error/advertencias/duración), `construirBadgeEstado` (tono del badge de estado) y `formatearDuracion`. |
| `exportar-errores-excel.funcion.ts` | `exportarErroresExcel`: exporta el detalle de errores/advertencias a `.xlsx` (delega en el helper compartido de Excel). |
| `agrupar-tipo-migracion.funcion.ts` | `esTipoPolloEngorde`: distingue la línea Pollo Engorde de Postura dentro del catálogo de tipos (gating por permiso de los tiles). |

## Nota de reutilización

Postura y Engorde comparten este mismo módulo de Migraciones Masivas — no hay lógica de país en
esta capa. `construirBadgeEstado` se usa tanto en el panel de carga (resultado vigente) como en el
historial (una fila por corrida), por eso vive acá y no dentro de un componente puntual.
