# funciones/ — Implementación

Funciones **puras** del módulo (sin `this`, sin DI, sin toasts ni estado): reciben parámetros y
devuelven resultado. Los componentes/páginas quedan delgados y delegan acá.

| Archivo | Responsabilidad |
|---|---|
| `agrupar-tareas-por-categoria.funcion.ts` | Agrupa el checklist por categoría preservando el orden, con subtotales por grupo. |
| `estado-tarea.funcion.ts` | Presentación de estados (label + colores por token CSS) para tareas y planes, incluida la marca de vencida. |

Reutilización multi-empresa/país: todo el scoping (empresa/país activos) lo resuelve el backend por
headers; estas funciones son agnósticas y sirven para cualquier empresa.
