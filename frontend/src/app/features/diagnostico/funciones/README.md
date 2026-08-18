# `features/diagnostico/funciones/` — qué puede ver y tocar quien abre el rescate

Convención de CLAUDE.md: una función **pura** por responsabilidad, sin `this`, sin DI y sin I/O. El
componente es un orquestador delgado que la invoca.

- **`clasificar-capturas-diagnostico.funcion.ts`** — decide, captura por captura, si es de la sesión
  activa. Está acá y no en el componente porque es la única cosa de esta pantalla cuyo error se paga
  caro: `/diagnostico` **no tiene `authGuard`** (es la pantalla de rescate, y con guard sería
  inalcanzable justo cuando se la necesita), así que lo que muestre lo ve cualquiera que levante la
  tablet. Es `fail-closed`: **sin sesión, nada es propio** y todo queda enmascarado.

## Reutilización

No depende del dominio avícola ni de un país: es la regla de «esto lo capturó quien está mirando».
Se apoya en `shared/offline/funciones/clave-particion.funcion.ts`, que es la misma clave que
particiona la caché y la cola.
