# `funciones/` — lógica pura del módulo indicador-ecuador

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de
dependencias). Cada archivo agrupa una "acción grande" del módulo —típicamente la lógica detrás de
un botón o de un getter— para que sea **fácil de encontrar, testear y reutilizar**.

## Convención

- **Un archivo por concern**, nombrado `<accion>.funcion.ts`.
- Reciben datos por parámetro y devuelven un resultado. **No** tocan `service`, `toast`, `HttpClient`
  ni estado del componente.
- El componente/página queda como **orquestador delgado**: arma los parámetros, llama la función y
  maneja estado/HTTP/UI. Se conserva TODO método/getter público que usa el template (solo su cuerpo
  delega).
- Los tipos compartidos viven en [`../models/`](../models), no aquí (evita imports circulares).
- **Aritmética intacta:** este cálculo espeja el backend `IndicadorEcuadorService` — mismos
  redondeos, orden y manejo de null. Refactor ≠ cambio de comportamiento.

## Índice

| Archivo | Qué hace |
|---|---|
| `formato.funcion.ts` | `formatearNumero`, `formatearPorcentaje`, `formatearFechaLote`, `sanitizarNombreHoja` (formato exacto, NO el central `shared/utils/format.ts` que separa miles). |
| `cascada-filtros.funcion.ts` | `construirCodigoAnioCorrida`, `aplicarFiltroCronologico`, `filtrarCascadaPe`, `filtrarCascadaGeneral`, `filtrarLotesPorFechaEncaset` (cascadas Granja→Núcleo→Galpón→Lote y filtros por fecha/año-corrida). |
| `corridas-panama.funcion.ts` | `corridasDisponiblesPanama`, `filtrarLotesPorCorridaPanama`: en Panamá el `loteNombre` ES el número de corrida (se repite por galpón); catálogo de corridas del alcance y filtro exacto del selector de lotes. |
| `parsear-filter-data-pollo.funcion.ts` | `parsearFilterDataPollo`: normaliza la respuesta camel/Pascal de `LoteReproductoraAveEngorde/filter-data`. |
| `etiquetas.funcion.ts` | `nombreGalponPe`, `etiquetaLoteFiltro`, `etiquetaColumnaLiquidacion`, `etiquetaTabLote` (labels de selector/columna/tab). |
| `liquidacion-totales.funcion.ts` | `ajusteAvesDe`, `porcentajeAjusteAvesDe`, `calcularLiquidacionTotales` (fila TOTAL de la planilla). |
| `exportar-reporte-tecnico-excel.funcion.ts` | `construirHojasReporteTecnico`: arma las hojas AoA del Reporte Técnico (la descarga queda en el componente). |

## Nota multi-país

Esta capa es la **base común** para la organización multi-país (Ecuador / Panamá). Los cálculos y
etiquetas comunes viven aquí; lo específico de un país (p. ej. el reporte Panamá) se mantiene en el
componente/servicio de ese país reutilizando estas funciones y los `models/`.
