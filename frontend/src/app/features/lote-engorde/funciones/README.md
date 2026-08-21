# `funciones/` — lógica pura del módulo lote-engorde

Carpeta de **funciones puras** (sin estado de Angular, sin `this`, sin inyección de dependencias).
Cada archivo agrupa una responsabilidad del módulo para que sea fácil de encontrar, testear y
reutilizar. Convención canónica: [`movimientos-pollo-engorde/funciones/`](../../movimientos-pollo-engorde/funciones/README.md).

## Convención

- **Un archivo por concern**, nombrado `<accion>.funcion.ts`.
- Reciben datos por parámetro y devuelven un resultado. **No** tocan `service`, `toast`, ni estado
  del componente.
- Los componentes quedan como **orquestadores delgados**: arman los parámetros, llaman la función y
  manejan estado/HTTP/UI.

## Índice

| Archivo | Qué hace |
|---|---|
| `aves-encasetadas.funcion.ts` | `avesInicialesDelLote` / `avesSaldoDelLote` / `totalAvesEncasetadas` / `deltaAvesEncasetadas`: separan el **encasetamiento** (la base que edita el formulario) del **saldo vivo** (lo que queda hoy). |

## Nota — encasetamiento vs. saldo

Un lote de engorde guarda las aves **dos veces**: `avesEncasetadas` + el registro `Inicio` del
historial son la base histórica, y `hembrasL`/`machosL`/`mixtas` son el saldo que el seguimiento
diario y las ventas van descontando. El formulario edita **la base**; el backend traduce el cambio a
un delta sobre el saldo (`AjusteEncasetamientoCalculos`) para no borrar las bajas ya aplicadas.

`avesInicialesDelLote` replica el fallback del backend para lotes sin registro `Inicio`. Si uno de
los dos cambia, **el otro tiene que cambiar igual**: si divergen, abrir y guardar el formulario sin
tocar nada generaría un ajuste que nadie pidió.
