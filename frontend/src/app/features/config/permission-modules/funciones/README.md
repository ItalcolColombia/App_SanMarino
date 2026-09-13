# permission-modules / funciones

Funciones **puras** de la pantalla «Módulos y permisos» (sin `this`, sin DI, sin HTTP): el componente
junta estado, llama la función y guarda el resultado en un campo (nunca desde un getter del template).

| Archivo | Qué resuelve |
|---|---|
| `matriz-modulos-empresa.funcion.ts` | Matriz módulo × empresa y la lista de módulos a enviar al tocar una celda (`null` si el estado de la empresa no cargó: no se guarda a ciegas). |
| `catalogo-modulos.funcion.ts` | Filtro del catálogo de permisos, «otros módulos» de un permiso compartido, ids del módulo y el texto del resultado. |

La **regla** de qué permisos se prenden o apagan NO vive acá: es del backend
(`Application/Calculos/PermisoModuloCalculos.cs`, con tests). El front sólo muestra y envía.
La agrupación por módulo que usan los modales de Roles y Empresas está en
`core/services/permission-module/agrupar-permisos-por-modulo.funcion.ts`.
