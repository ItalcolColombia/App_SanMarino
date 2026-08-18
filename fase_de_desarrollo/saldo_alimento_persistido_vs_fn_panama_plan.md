# El saldo de alimento GUARDADO se separó de la fn en Panamá — y la liquidación lo congela

**Origen:** el pendiente «No verificado (declarado)» del bloque *«Auditoría de cierre — alimento previo
al encaset»* del tracker: *«Descuadre persistido vs fn en Panamá (69 filas, hasta 23.355 kg):
detectado, NO se determinó si necesita la migración `Recalcular…` que sí acompañó a v11 y v12 (este
lote tocó la fn 2 veces sin ella)»*.
**Fecha:** 2026-08-17 · **Respuesta corta: sí la necesita, y hay una razón que no estaba escrita.**

---

## 1. Lo medido (BD local tipo prod, 17ago26)

Comparación fila a fila `seguimiento_diario_aves_engorde.saldo_alimento_kg` (la columna GUARDADA)
contra `fn_seguimiento_diario_engorde(lote).saldo_alimento_kg` (la que pinta la grilla), por
`seg_id`:

| empresa | estado del lote | filas | **filas que difieren** | lotes | peor | Σ absoluta |
|---|---|---|---|---|---|---|
| ItalcolEcuador | Abierto | 645 | **0** | 0 | 0,0 | — |
| ItalcolEcuador | Cerrado | 4.544 | **0** | 0 | 0,0 | — |
| ItalcolPanama | Abierto | 1.021 | **109** | **36** | **23.355,0 kg** | 682.885 kg |

El dato de la auditoría (69 filas) creció a **109**. Ecuador está en cero — o sea que la columna y la
fn coinciden donde alguien ya recalculó, y se separan donde no.

## 2. 🔴 Por qué importa: la liquidación CONGELA esa columna

`LiquidacionCongeladaAplicador` toma el saldo del **último día** directo de la columna guardada
(`OrderByDescending(Fecha).Select(s => s.SaldoAlimentoKg).First()`) y lo escribe en la copia congelada
de la liquidación. Una foto congelada **no se reescribe**: si la columna estaba desalineada ese día, el
número queda mal **para siempre** — y de ahí lo leen Costos, el modal de liquidación y el reporte de
«liquidados con alimento sin trasladar» que se entregó ayer.

**Medido: 6 lotes de Panamá tienen HOY el último día divergente** (el peor por **9.844 kg**). Si se
liquidan antes de recalcular, congelan un saldo que nadie va a poder corregir.

## 3. Qué forma tiene la divergencia (lote 179, el peor)

| fecha | guardado | fn | dif | ingreso del día (fn) |
|---|---|---|---|---|
| 22-jul | 12.332,7 | **−544,0** | 12.876,7 | 0 |
| 23-jul | 11.472,7 | 11.472,7 | 0 | **12.876,7** |
| 27-jul | 29.247,2 | **5.892,2** | 23.355,0 | 0 |
| 28-jul | 27.477,4 | 27.477,4 | 0 | **23.355,0** |
| 11→13-ago | … | … | 953 → 1.951 → 2.949 | — |

Se leen dos efectos distintos:

1. **El ingreso está atribuido a un día distinto.** La diferencia de un día es **exactamente** el
   ingreso que la fn pone al día siguiente, y al día siguiente las dos fuentes vuelven a coincidir. Es
   la firma de una columna escrita con **otra versión de la fórmula** (o antes de que se corrigiera la
   fecha del movimiento): la fn de hoy lee `fecha_operacion` y la columna quedó con la atribución vieja.
2. **La cola acumulativa** (11→13-ago): la columna dejó de actualizarse y se va quedando atrás día a
   día. Es el defecto conocido —el recálculo solo corría al crear/editar un seguimiento— sobre los
   días más recientes.

**Descartado con datos:** no es la doble validación (las 109 filas están `validado = true` sin
`validado_at`, igual que las 912 que sí coinciden) ni «movimiento registrado después» por sí solo
(90,8 % de las filas que difieren lo tienen, pero también el 94,5 % de las que NO difieren).

## 4. La corrección: la misma migración que acompañó a v11 y v12

Ya hay precedente y plantilla — `20260730141000_RecalcularSaldoAlimentoEngordeV12`. Se repite el
patrón, sin inventar nada:

| paso | qué |
|---|---|
| Backup | `_backup_saldo_alimento_engorde_20260818` con `WHERE NOT EXISTS` ⇒ conserva SIEMPRE el valor original aunque se re-ejecute |
| Recálculo | `UPDATE … FROM (LATERAL fn_seguimiento_diario_engorde) … WHERE saldo IS DISTINCT FROM nuevo` ⇒ **idempotente** |
| `Down` | restaura desde el backup |

**Una sola fórmula por número:** el valor nuevo sale de la **propia fn**, que es la dueña. No se
escribe aritmética nueva en ningún lado.

**Simulado en transacción y revertido (17ago26):** cambia **109 filas, todas de ItalcolPanama**
(682.885 kg de movimiento absoluto), **0 filas de ItalcolEcuador**, y deja **0 divergencias**. La
verificación posterior corre dentro de la misma transacción antes del `ROLLBACK`.

## 5. Verificación

1. Simulación en transacción + `ROLLBACK` (hecha, §4) — **antes** de escribir la migración.
2. `dotnet build` + `dotnet test`.
3. `dotnet ef database update` en local: aplica sin error y deja **0 divergencias**.
4. Re-correr la migración ⇒ **0 filas** cambian (idempotencia real, no declarada).
5. `fn_cuadre_alimento_engorde` **antes y después**: el cuadre **no puede moverse** — lee la fn, no la
   columna. Es el control de que esto no toca el número que mira operación.
6. `git diff backend/sql` vacío: **no se toca ninguna función SQL** ⇒ no aplica el gate multipaís de
   cálculo compartido (igualmente Ecuador queda byte a byte por construcción: 0 filas cambian).

## 6. Fuera de alcance, dicho

- **No se toca `fn_seguimiento_diario_engorde`.** La columna se alinea a la fn, nunca al revés.
- **No se corrige la causa de fondo del efecto 2** (que el recálculo no corra en todos los caminos que
  mueven un día ya cargado). Esta migración deja la foto alineada hoy; que no se vuelva a desalinear es
  otro trabajo, con su propio plan.
- **No se tocan las copias congeladas** de liquidación ya existentes: las 90 de Ecuador quedan como
  están (y allí la columna ya coincidía).
