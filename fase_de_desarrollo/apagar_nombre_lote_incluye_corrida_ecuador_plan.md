# Apagar `nombre_lote_incluye_corrida` en ItalcolEcuador (1-sep-2026)

## Por que

Ticket de operacion (CAROLINA / GALPON 6): *«al crear lote nos sale una secuencia, ejemplo 2604-02,
a que hace referencia el 02?»*.

Medido contra la copia de produccion (`sanmarinoapplocal`, dump hasta el 30-ago) hay **dos causas
distintas** detras de los sufijos, y solo una es un defecto:

| Sintoma | Causa | Es un defecto? |
|---|---|---|
| `2604 - 2` (CAROLINA G6, id 237) | El lote 236 se creo el 27-ago 08:57, se **elimino** a las 13:54 y se recreo a las 13:55. `CreateAsync` calcula `MAX(corrida)+1` **contando los eliminados** para no reusar un nombre ya usado | **No.** Es el comportamiento querido; el `- 2` es la huella del lote borrado |
| `2604 - 1` (CAROLINA G7, id 241; `2605 - 1` en Kilometro 86, id 229) | `companies.nombre_lote_incluye_corrida = true` en **ItalcolEcuador** | **Si.** Ecuador debe ir en `false` |

La migracion que creo la columna (`20260811220638_AddNombreLoteIncluyeCorrida`) la deja en `false` por
defecto y solo la prende para `ItalcolPanama`; el plan
[`manual_lote_base_engorde_ecuador_plan.md`](manual_lote_base_engorde_ecuador_plan.md) lo dice
explicito. **Ninguna migracion la prende para Ecuador** ⇒ la prendio alguien desde la administracion
de empresas. Se ve en los datos: hasta el 21-ago los lotes de Ecuador nacian `2604`, `2603`… y desde
el 26-ago nacen con ` - 1`.

## Que se hace

Una migracion **data-only** (no toca el modelo) que devuelve el flag a `false` para `ItalcolEcuador`.

- `Up()`: `UPDATE companies SET nombre_lote_incluye_corrida = false WHERE name = 'ItalcolEcuador' AND nombre_lote_incluye_corrida IS DISTINCT FROM false;`
  Idempotente por el `IS DISTINCT FROM` (2a pasada = 0 filas).
- `Down()`: inverso exacto (lo devuelve a `true`), porque el estado previo esta **medido**, no supuesto.
- Sin cambio de schema ⇒ `.Designer.cs` clonado del `ModelSnapshot` actual y **el snapshot no se toca**
  (patron de las migraciones de seed; evita colisionar con sesiones paralelas).

## Que NO se hace, y por que

- **No se renombran los lotes ya creados** (`241` = `2604 - 1`, `229` = `2605 - 1`). Renombrar es un
  backfill sobre datos vivos y `lote_nombre` lo leen reportes que agrupan por nombre. Si operacion lo
  pide, va como trabajo aparte y con su gate.
- **No se toca `2604 - 2`** (CAROLINA G6 id 237, y sus 3 gemelos). Es informacion real: hubo un lote
  eliminado antes. Renombrarlo a `2604` ademas chocaria con la unicidad si el borrado se revierte.
- **No se cambia `CreateAsync`.** Que el contador no reuse los numeros de los lotes eliminados es
  deliberado y esta cubierto por tests.

## Efecto esperado

Solo sobre lotes **nuevos** de Ecuador: la primera apertura de un base en un galpon vuelve a llamarse
`2605`; la segunda sigue siendo `2605 - 2` (lo unico que evita dos lotes con el mismo nombre en el
mismo galpon). Panama (`ItalcolPanama`, flag `true`) queda **intacto**.

## Casos de prueba

1. `Up()` corrido dos veces en la misma transaccion: la 2a actualiza **0 filas** (idempotencia probada,
   no declarada).
2. `Down()` en la misma transaccion deja `ItalcolEcuador` en `true` otra vez.
3. `ItalcolPanama` sigue en `true` despues del `Up()`; ninguna otra empresa cambia (`Sanmarino`, `Demo`,
   `Santa Reyes` ya estaban en `false`).
4. `ConstruirNombreLote` ya tiene sus xUnit para los dos valores del flag
   ([`GestionLotesEngordeCalculosTests.cs`](../backend/tests/ZooSanMarino.Application.Tests/GestionLotesEngordeCalculosTests.cs)):
   este cambio es de datos, no de logica, y esos tests son el contrato que no debe moverse.
