# funciones/ — Vacunación

Convención del repo (ver CLAUDE.md → CLEAN CODE): **una función grande / de botón por archivo**,
PURA — recibe parámetros y devuelve resultado; sin `this`, sin DI, sin toasts ni estado.
Los componentes/páginas quedan delgados: juntan estado, llaman la función y manejan HTTP/UI.

| Archivo | Responsabilidad |
|---|---|
| `calcular-estado-visual.funcion.ts` | Estado de aplicación → etiqueta + clases de badge (solo presentación; la lógica viene del backend). |
| `construir-filas-cronograma.funcion.ts` | Ítems → filas `{item, estado}` con el estado visual **precalculado una vez por carga** (evita funciones en template / alocaciones por ciclo de CD — patrón NG0103). |
| `calcular-kpis-cronograma.funcion.ts` | KPIs del lote seleccionado (totales por estado + % avance / % a tiempo). |
| `calcular-kpis-cumplimiento.funcion.ts` | KPIs globales del reporte (ponderados sobre los lotes filtrados). |
| `exportar-cronograma-excel.funcion.ts` | Excel del cronograma de un lote. |
| `exportar-historial-excel.funcion.ts` | Excel del historial de aplicaciones de un lote. |
| `exportar-cumplimiento-excel.funcion.ts` | Excel multi-hoja del reporte (Resumen + Cumplimiento por lote + Detalle por vacuna). |

Exportaciones: siempre vía helpers compartidos de `shared/utils/excel/exportar-tabla-excel.funcion.ts`
y formato vía `shared/utils/format.ts` — prohibido `XLSX.utils.*` inline.

Reutilización multi-país: todas las funciones son agnósticas de país/empresa (los datos ya vienen
acotados por el backend vía `X-Active-Company`).
