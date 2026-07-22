# funciones/ — Implementación

Funciones **puras** del módulo (sin `this`, sin DI, sin toasts ni estado): reciben parámetros y
devuelven resultado. Los componentes/páginas quedan delgados y delegan acá.

| Archivo | Responsabilidad |
|---|---|
| `agrupar-tareas-por-categoria.funcion.ts` | Agrupa el checklist por categoría preservando el orden, con subtotales por grupo. |
| `estado-tarea.funcion.ts` | Presentación de estados (label + colores por token CSS) para tareas, planes, tipos de cronograma y firmas. Referencias constantes → estables para el template. |
| `filtrar-planes.funcion.ts` | Filtro client-side de la lista de cronogramas (búsqueda/tipo/estado) — instantáneo, sin HTTP. |
| `filtrar-tareas.funcion.ts` | Filtro client-side de los ítems del detalle (búsqueda/categoría/estado/solo vencidas, incluye participantes). |
| `resumen-firmas.funcion.ts` | Conteos de firmas por ítem (espejo de `ImplementacionCalculos.CalcularResumenFirmas`) + `mensajeErrorHttp` (timeout/401/403 legibles). |

Reutilización multi-empresa/país: todo el scoping (empresa/país activos) lo resuelve el backend por
headers; estas funciones son agnósticas y sirven para cualquier empresa.
