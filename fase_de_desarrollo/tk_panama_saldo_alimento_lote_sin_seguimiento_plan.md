# Ticket Panamá — DOÑA MARÍA / A / 4, lote 95: saldo de alimento heredado, 32 kg separados y 508 kg faltantes

> Diagnóstico del 04-sep-2026 sobre la **copia de producción del 04-sep 02:24** restaurada en la base
> local `sanmarino_tk95`. **Ningún dato de producción se tocó.** El caso está **reproducido al
> decimal** en transacción revertida.

## 1. Qué reportó la operación

1. El alimento del lote 95 «aparece con saldo acumulado de otro lote que ya liquidamos» → la grilla
   diaria muestra **176.246,97 kg** el 04-sep con un solo ingreso de **11.740 kg**.
2. En el stock «se están separando 32 kg que no hemos ingresado».
3. En el stock «deben haber 11.740 y tenemos menos»: muestra **11.232 kg** (disponible 11.200).

Ubicación: granja **DOÑA MARIA** (id 106) · núcleo **A** (147337) · galpón **4** = **G0475** ·
ítem **223 / SM0175 AV. POLLITO PREINICIADOR** · empresa **ItalcolPanama** (5).

## 2. Los tres números, explicados con aritmética exacta

### 2.1 · 176.246,97 kg — el saldo suma TODA la historia del galpón

`fn_seguimiento_diario_engorde` acota **las filas** que muestra con
`li.fecha_corte_alimento` (= encaset − `dias_alimento_previo_encaset`, hoy 10 días) —
`fechas_universo`, línea 836 de `backend/sql/fn_seguimiento_diario_engorde.sql`.

Pero **el saldo** de esas filas lo arma `hist_full` (línea 625), `hist_alimento` (768) y
`docs_por_fecha` (796), y las tres acotan con:

```sql
AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
```

`rs.fecha_min` es el **primer seguimiento diario del lote**. Mientras el lote **no tiene ningún
seguimiento cargado**, `fecha_min` es NULL ⇒ **la condición se vuelve verdadera para todo** ⇒ el saldo
suma **todos** los movimientos de alimento que el galpón recibió en su historia, **sin** los guards de
ciclo que sí protegen la apertura (`lotes_ajenos` v11 y `corte_apertura` v12 solo viven en `apert_mov`,
que además exige `fecha_min IS NOT NULL` y por eso vale 0 acá).

Medido en G0475: `INV_INGRESO` histórico = **173.296,967 kg** desde el **02-jul-2026** (ciclo anterior,
lote 165 «94 - 2», **liquidado el 27-ago**). Sacando la devolución por eliminación (6.350, que la fn
descarta) y el ingreso del 28-ago (2.440, que fue **editado a 11.740 y refechado al 04-sep**):

```
164.506,967  (todo el histórico del galpón, ciclos anteriores incluidos)
+ 11.740,000  (el ingreso real del lote 95)
= 176.246,967  ← exactamente lo que muestra la pantalla
```

**Reproducido** (`ESCENARIO B`, transacción revertida): lote nuevo en G0475, encaset 02-sep, ingreso de
11.740 el 04-sep, **sin seguimiento** ⇒ la fn devuelve **una sola fila**, `edad_dia 2`, `semana 1`,
`saldo_alimento_kg 176246.967`, `ingreso 11740`, `saldo_aves 19110`. Idéntico a la captura del ticket.

**Se corrige solo al cargar el primer seguimiento** (`ESCENARIO A`: mismo lote con un seguimiento el
04-sep ⇒ saldo **11.740**; `ESCENARIO C`: primer seguimiento el 05-sep con 620 kg de consumo ⇒
apertura 11.740 y saldo **11.120**). Por eso el workaround que pide la operación funciona.

**Alcance medido en la copia de prod (04-sep):** lotes vivos, sin cerrar y **sin un solo seguimiento**,
cuyo galpón tiene movimientos de alimento → **1 lote**, y **no es de Panamá**:

| Empresa | Lote | Granja/Galpón | Encaset | Saldo que muestra la grilla |
|---|---|---|---|---|
| ItalcolEcuador | 229 «2605» | 40 / G0039 | 26-ago | **304.470,0 kg** (55 movimientos desde 13-feb) |

⇒ **el defecto NO es de Panamá**: es de la función y alcanza a todas las empresas. Lo que hace que
Panamá lo vea y Ecuador casi no es **operativo**: Ecuador fecha el ingreso el mismo día del primer
seguimiento (110/110 ciclos medidos en jul-2026), así que la ventana «lote creado con alimento y sin
seguimiento» dura minutos; Panamá registra el alimento días antes y vive dentro de esa ventana.

### 2.2 · Los 32 kg «separados» — una reserva huérfana de una PRUEBA

La columna **SEPARADO** = `Σ seguimiento_reserva_alimento` en estado **ACTIVA** para esa ubicación
(`InventarioGestionService.Consulta.cs:276-303`); `DISPONIBLE = CANTIDAD − SEPARADO`
(`InventarioGestionDtos.cs:117`). Una reserva nace cuando se **guarda** un seguimiento diario y se
libera cuando se **valida** — es la doble validación, y **ItalcolPanama es la única empresa con
`requiere_validacion_seguimiento_diario = true`**. Por eso esta columna solo muerde en Panamá.

En G0475 hay **una sola** reserva activa:

| id | ítem | módulo | seguimiento | lote_ref | fecha | kg | estado |
|---|---|---|---|---|---|---|---|
| 425 | 223 | ENGORDE | 12944 | **PRUEBA - 1** | 03-sep | **32,000** | **ACTIVA** |

El seguimiento 12944 (`validado = false`) pertenece al lote **238 «PRUEBA - 1»**, que fue
**borrado (soft-delete) el 28-ago 09:17** — 18 minutos **después** de crearse la reserva (08:59).
🔴 **Borrar el lote no liberó la reserva de sus seguimientos sin validar**, y como la pantalla de stock
suma reservas por **ubicación** (no por lote), esos 32 kg siguen restando disponible al ciclo nuevo.

**Alcance medido:** en toda la base hay **4 reservas ACTIVA** (1.937,12 kg, todas de ItalcolPanama);
**3 son legítimas** (seguimientos vivos pendientes de validar en la granja 107) y **solo la 425 es
huérfana**.

### 2.3 · Los 508 kg «que faltan» — consumo real de la misma PRUEBA

`11.740 − 11.232 = 508`. Son los 7 días de consumo que la prueba del 28-ago dejó **validados**:

```
seguimiento reproductora #890..#896 (lote reproductora 145 «35», del lote engorde 238 PRUEBA)
150 + 100 + 125 + 34 + 56 + 31 + 12 = 508 kg   → INV_CONSUMO reales sobre el ítem 223
```

Todos creados el **28-ago 08:54-08:57** con fechas **futuras** (29-ago → 04-sep) y confirmados, así que
descontaron stock de verdad: 2.440 − 508 = **1.932 kg**, que es el stock que tenía el galpón antes de
que hoy editaran el ingreso a 11.740 (1.932 − 2.440 + 11.740 = **11.232** ✓).

⚠️ **No es un error de cálculo: es consumo aplicado.** Si la operación confirma que esos 508 kg no se
comieron (era una prueba), hay que devolverlos con un movimiento, no «arreglando» el número.

## 3. Enfoque arquitectónico del arreglo

### F1 · `fn_seguimiento_diario_engorde` v18 — el saldo se acota igual que las filas

Cambio **mínimo y quirúrgico** en las tres CTE que hoy pierden la cota (`hist_full`, `hist_alimento`,
`docs_por_fecha`): cuando `rs.fecha_min` es NULL, usar el mismo piso que ya usa `fechas_universo`.

```sql
-- antes
AND (rs.fecha_min IS NULL OR DATE(h.fecha_operacion) >= rs.fecha_min)
-- v18
AND DATE(h.fecha_operacion) >= COALESCE(rs.fecha_min, li.fecha_corte_alimento, DATE(h.fecha_operacion))
```

- Con `fecha_min` presente (todo lote con al menos un seguimiento) la expresión es **idéntica** ⇒
  salida byte a byte igual para el 100 % de los lotes con seguimiento.
- Con `fecha_min` NULL y encaset presente, el saldo cubre exactamente los mismos movimientos que las
  filas que la grilla ya muestra ⇒ desaparece la incoherencia interna.
- Con encaset también NULL, el comportamiento queda como hoy (sin piso): no se inventa una regla.
- La firma no cambia (49 columnas OUT) ⇒ `CREATE OR REPLACE`, sin `DROP`, sin tocar los 5 consumidores.

**Migración EF en el mismo commit** (regla «el .sql es el espejo, la migración es el vehículo»), con
`Down` = v17 verbatim.

### F2 · Liberar las reservas cuando se borra el lote

Que borrar un lote de engorde (o de reproductora) libere las reservas ACTIVA de sus seguimientos, con
el mismo criterio de `LiberarAsync` (pasan a `LIBERADA`, no se borran). Precedente:
`20260831150000_DevolverAlimentoDeReproductorasBorradas`.

### F3 · Dato de producción (requiere OK explícito — NO se ejecuta sin confirmación)

- **F3.1** Liberar la reserva huérfana `id 425` (32 kg) — recupera el disponible del galpón.
- **F3.2** Decisión de operación: ¿los 508 kg de la prueba se devuelven al stock? Si sí, va por
  movimiento de inventario visible (no por UPDATE al número), y hay que elegir el instrumento
  (`AjusteCuadre*` mueve la tabla y no el stock; `AjusteStock` mueve el stock y es invisible para la
  tabla; un `Ingreso` mueve los dos).

## 4. Reglas de negocio que quedan fijadas

- **R1** El saldo de la grilla y las filas de la grilla se acotan con el **mismo** piso. Nunca puede
  mostrarse un saldo que incluya movimientos de días que la propia grilla no lista.
- **R2** Un lote sin seguimientos **no hereda** el alimento de los ciclos anteriores del galpón.
- **R3** Borrar un lote libera lo que sus seguimientos tuvieran **separado**; el rastro queda
  (`LIBERADA`), no se borra.

## 5. Casos de prueba

- **T1** (fn) Lote sin seguimiento, galpón con historia previa ⇒ saldo = solo los movimientos desde
  `encaset − N`. Caso testigo: G0475 con el lote simulado ⇒ **11.740**, no 176.246,967.
- **T2** (fn) Lote con seguimiento ⇒ salida idéntica a v17 (gate de identidad).
- **T3** (fn) Lote sin seguimiento **y sin encaset** ⇒ igual que hoy.
- **T4** (gate multipaís) `backend/sql/verificar_paridad_saldo_engorde.sql` dos veces: las únicas
  diferencias admitidas son los lotes **sin seguimiento** (hoy: Ecuador 229 y el de Panamá del ticket);
  cualquier lote con seguimiento que se mueva es regresión.
- **T5** (cuadre) `backend/sql/verificar_cuadre_alimento_engorde.sql` antes/después: 0 galpones movidos.
- **T6** (reservas) Borrar un lote con un seguimiento sin validar ⇒ su reserva queda `LIBERADA` y el
  disponible del galpón vuelve a `cantidad`.

---

## Revisión adversarial (05-sep-2026) — lo que cambió el diseño

Cinco lentes independientes sobre el cambio completo (SQL, migración de la fn, migración de datos,
C#, completitud). Lo que sobrevivió y se corrigió:

### 1. El piso nuevo entraba sin los guards v11/v12 — el hallazgo que importaba

La primera versión de v18 usaba `li.fecha_corte_alimento` (la ventana cruda de v9) como piso cuando
no hay seguimientos. Pero esa ventana es exactamente la que v11 (`lotes_ajenos`) y v12
(`corte_apertura`) existen para sanear: sin ellos, un lote nuevo se come la limpieza de cierre del
ciclo anterior y puede **abrir en negativo** — el incidente de Ecuador de jul-2026, donde los
galpones encadenan 3-4 ciclos.

El guard no aplicaba porque `corte_apertura` y `lotes_ajenos` exigen `rs.fecha_min IS NOT NULL`, que
es justo lo que este caso no tiene. La corrección: `corte_apertura` deja de exigirlo y usa
`COALESCE(rs.fecha_min, li.fecha_encaset::DATE)` como arranque del ciclo. Con seguimientos, el
COALESCE elige `fecha_min` ⇒ el valor de `desde` no se mueve ni un día.

**Medición del guard** (G0475, transacción revertida): con un encaset hipotético del 15-ago, la
ventana cruda arrancaría el 05-ago, dentro del ciclo del lote 165 (último seguimiento 11-ago), y se
tragaría sus ingresos del 06-ago (15.422 kg) y 08-ago (6.350 kg) = **21.772 kg heredados**. Con el
corte (12-ago) el saldo de esa fila da **0**.

### 2. Un dato de la cabecera era falso

La cabecera afirmaba un segundo caso vivo en Ecuador (lote 229 «2605», G0039, 304.470 kg heredados).
La fn devuelve **0 filas** para ese lote: su galpón no tiene movimientos dentro de la ventana. El
número salía de sumar el histórico del galpón, no de la fn. Reemplazado por la medición real:

| | |
|---|---|
| Lotes vivos sin un solo seguimiento | 26 (25 ItalcolPanama, 1 ItalcolEcuador) |
| De esos, con filas en la grilla | 1 (Panamá 131 «94», PA-87, saldo **0**, inofensivo) |
| Con saldo inflado | 0 en la copia — el lote 95 se creó después del snapshot |

El defecto es **latente**: se despierta el día que el galpón recibe un movimiento en la ventana, que
es exactamente lo que pasó al cargarle los 11.740 kg al lote 95.

### 3. `HardDeleteAsync` liberaba antes de un `Remove` que no puede prosperar

`fk_seg_diario_aves_engorde_lote` y `fk_lrae_lote_ave_engorde` son **RESTRICT**. El hard delete solo
funciona si el lote no tiene seguimientos ni sub-lotes de reproductora — o sea, cuando no hay nada
que liberar. Y en el caso en que sí hubiera algo, el `Remove` fallaría **después** de haber liberado,
dejando reservas sueltas de un lote que sigue vivo y todavía puede validar esos días. Se quitó la
llamada.

### 4. Tres cierres menores

- `DeleteAsync`: el doc-comment prometía «no propaga» y no había `try/catch`. Ahora sí.
- La liberación no cubría las reservas de **REPRODUCTORA** del lote (las que produjeron los 508 kg
  del propio ticket) ni el literal legado `ENGORDE_EC`.
- La migración de datos era fail-closed sobre la **causa** (los 7 consumos) pero no sobre el
  **efecto**: si alguien corregía los 508 kg con el lápiz de stock antes del deploy, los sumaba dos
  veces. Ahora también se abstiene si hay un ajuste manual posterior al diagnóstico.
