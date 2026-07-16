# `funciones/` — lógica pura del módulo sincronizacion-panama

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de dependencias).
Cada archivo agrupa una "acción grande" del módulo para que sea fácil de encontrar, testear y
reutilizar. Sigue el patrón canónico de `features/movimientos-pollo-engorde/funciones/`.

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
| `construir-request.funcion.ts` | `construirConexion` / `construirRequest`: arman `PanamaConexion` y `SincronizarPanamaRequest` desde el formulario (campos vacíos se omiten → backend usa su config). |
| `construir-resumen.funcion.ts` | `construirResumen`: `ResultadoSincronizacionDto` → tarjetas-contador del resumen. |
| `estado-lote.funcion.ts` | `badgeEstadoLote` / `badgeEstadoResultado`: estado de texto → `{ etiqueta, tono }`. |
| `exportar-lotes-excel.funcion.ts` | `exportarLotesSincronizacion`: arma y descarga el `.xlsx` de la tabla por lote (helper compartido). |

## Nota multi-país

Este módulo es el puente de **integración de Panamá** (ZooPanamaPollo → Pollo Engorde). Si en el
futuro se agregan puentes de otros países, la base común (mapeos/armado de request/resumen) vive
acá y cada país aporta solo su presentación específica.
