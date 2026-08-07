# `funciones/` — ItalJira

Una función **pura** por archivo: recibe parámetros y devuelve un resultado. Sin `this`, sin
inyección de dependencias, sin HTTP, sin toasts y sin tocar estado del componente.

Las páginas (`pages/`) quedan como orquestadores delgados: juntan el estado, llaman a la función y
manejan HTTP/UI. Así el armado del árbol y el Excel se pueden testear sin levantar Angular, y se
reusan desde el backlog, el tablero y el roadmap sin copiarlos.

| Archivo | Qué resuelve |
|---|---|
| `armar-arbol-backlog.funcion.ts` | Convierte la lista plana de tareas en el árbol tarea → subtareas y calcula los totales de la historia. |
| `exportar-backlog-excel.funcion.ts` | Arma las hojas del Excel del backlog sobre el helper compartido `exportarMultiHojaExcel`. |

**Prohibido** volver a `import * as XLSX` inline: el Excel sale de
`shared/utils/excel/exportar-tabla-excel.funcion.ts`.
