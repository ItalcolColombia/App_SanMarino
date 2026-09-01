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

---

# Anexo: renombrar los dos lotes que ya nacieron con sufijo (1-sep-2026)

Pedido del usuario despues de apagar el flag. Deja de ser "solo lotes nuevos": se corrigen tambien
los que nacieron mientras el flag estuvo prendido.

## Alcance medido

| id | nombre hoy | destino | galpon | granja | estado |
|---|---|---|---|---|---|
| 229 | `2605 - 1` | `2605` | Galpon-1 | Kilometro 86 | vivo ⇒ **se renombra** |
| 241 | `2604 - 1` | `2604` | GALPON 7 | CAROLINA | vivo ⇒ **se renombra** |
| 235 | `2604 - 1` | — | GALPON 5 | CAROLINA | eliminado ⇒ no se toca |
| 236 | `2604 - 1` | — | GALPON 6 | CAROLINA | eliminado ⇒ no se toca |

Los eliminados se dejan: no se ven en ninguna pantalla ni reporte, y tocarlos solo agrega riesgo.

## Por que el rename es seguro aca (verificado, no supuesto)

- **0 colisiones**: ningun otro lote del mismo galpon se llama `2604` / `2605`, ni vivo ni eliminado.
- **No hay indice unico** sobre `lote_ave_engorde.lote_nombre` ⇒ el `UPDATE` no puede chocar.
- Los dos lotes **no tienen** fila en `liquidacion_lote_engorde_congelada` (que si copia `lote_nombre`
  y lo congela), ni seguimientos, ni filas en `lote_registro_historico_unificado`. Lo unico que cuelga
  es su registro de inicio en `historial_lote_pollo_engorde`, que **no guarda el nombre**.
- Las `vw_seguimiento_pollo_engorde`, `vw_indicadores_diarios_engorde` y
  `vw_liquidacion_ecuador_pollo_engorde` leen el nombre de la tabla ⇒ se actualizan solas.
- `numero_corrida` **no se toca**: sigue en 1, que es lo que habrian tenido de nacer con el flag
  apagado. La proxima apertura se calcula por `MAX(numero_corrida)`, no por el nombre, asi que el
  rename no altera ninguna corrida futura.

## Como se acota el UPDATE, y por que hay tabla de respaldo

Por **regla, no por id**: solo `ItalcolEcuador`, solo `numero_corrida = 1`, solo si el nombre es
**exactamente** `<base> - 1` (lo que genera el flag), solo `deleted_at IS NULL`, solo creados desde el
22-ago -05:00 (el ultimo lote sin sufijo es del 21-ago; entre el 22 y el 25 no se creo ninguno, asi
que el piso no tiene borde discutible), y con un `NOT EXISTS` que descarta la fila si el nombre
destino ya lo usa otro lote vivo del mismo galpon.

**Sin tope superior a proposito.** La copia de produccion llega al 30-ago y el flag sigue prendido
alla hasta que se despliegue: cualquier lote que nazca con el mismo defecto entre hoy y el deploy
tiene que entrar en el backfill. Despues del deploy no puede haber mas, porque la migracion que apaga
el flag corre antes en el mismo arranque.

Ese tope superior era, en el primer borrador, lo unico que hacia exacto al `Down()`. Se reemplaza por
algo mejor: el `Up()` guarda el nombre anterior de cada fila en
`_backup_rename_lote_engorde_ecuador_20260901` (mismo patron que los otros `_backup_*` del repo) y el
`Down()` restaura **desde ahi**, fila por fila. Es exacto por construccion en vez de por adivinanza,
y de paso queda en produccion la auditoria de que se renombro.

## Casos de prueba

1. `Up()` toca **exactamente 2 filas** (229 y 241) y las deja en `2605` / `2604`.
2. El respaldo queda con esas 2 filas y su nombre anterior textual.
3. `Up()` 2a pasada: **0 filas** (ya no matchea `<base> - 1`) y el respaldo no se duplica.
4. Los eliminados 235 y 236 **conservan** su `2604 - 1`, y el 237 su `2604 - 2`.
5. Ningun otro lote de Ecuador cambia de nombre: de las 144 filas de la empresa, solo esas 2 difieren.
6. `Down()` deja **0 filas distintas del estado inicial** y borra el respaldo.
