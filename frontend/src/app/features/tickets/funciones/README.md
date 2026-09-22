# `features/tickets/funciones/`

Funciones **puras** del módulo de tickets: reciben parámetros y devuelven un resultado. Sin `this`, sin
DI, sin HTTP, sin toasts y sin estado. Una por concern, en su propio archivo, con su `.spec.ts` al lado.

Los componentes quedan como orquestadores delgados: juntan el estado y los inputs, llaman a la función y
se ocupan de la red y de la UI (patrón canónico del repo: `features/movimientos-pollo-engorde/`).

| Archivo | Qué resuelve |
|---|---|
| `estado-resolutores.funcion.ts` | De las filas de resolutor que devuelve el backend al estado del editor (un toggle y un alcance por tipo) y al resumen «qué atiende hoy». La regla que sostiene: una fila **GLOBAL** manda sobre la de empresa del mismo tipo, y un alcance ausente o desconocido cuenta como EMPRESA. |

**Reutilización:** la decisión de fondo (a qué tickets aplica una fila, quién puede marcar GLOBAL) vive en
el backend, en `Application/Calculos/TicketResolutorAlcanceCalculos` y
`TicketPerfilAutorizacionCalculos`, con sus tests xUnit. Lo de acá es la vista: si las dos divergen, manda
el backend.
