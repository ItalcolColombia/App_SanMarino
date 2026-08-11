# Rediseño de la marca `para_proximo_ciclo` (alimento de engorde) — v16

**Fecha:** 2026-08-08 · **Estado del repo:** HEAD `d6aeccb`, working tree limpio.
**Base del feature:** `801b14f` (columna `para_proximo_ciclo` migración `20260808120000`, fn v15 con
`apertura_alimento_kg` migración `20260808130000`, checkbox del front, `PUT /ingresos/{id}/destino-ciclo`).
**Marcas en producción y en la BD local:** `0` filas en `lote_registro_historico_unificado` y `0` en
`inventario_gestion_movimiento` (verificado hoy). Nadie usó la marca todavía: **el rediseño es libre**.

**Qué está roto hoy (v15):**
1. La marca **no es coherente con `fn_cuadre_alimento_engorde`** — A/B controlado sobre el mismo ingreso,
   cambiando solo el booleano, mueve el cuadre a **−5.000** (auditoría §2.3b).
2. Un ingreso marcado **fechado antes de `encaset − N`** no aparece **en ninguna pantalla** hasta que el
   ciclo destino carga su primer seguimiento (auditoría §2.3c). Reproducido: granja 42 / G0049 / lote 132,
   7.000 kg del 06-ago, documento `005-001-000063560` — la fila pasa de `ingreso 7.000 / saldo 11.260 /
   documento 005-001-000063560` a `ingreso 0 / saldo 4.260 / documento vacío`.

**Historia previa (vinculante):** el intento «v16» del 08-ago-2026 se revirtió tras **3 rondas NO-GO**
(bloque del tracker «v16 de engorde … INTENTADA Y REVERTIDA» y commit `d6aeccb`). Cada guarda nueva **mudó
el defecto de lugar**: multiplicación en 4 lotes → descuadre +5.000 permanente en 33/35 galpones de Ecuador
→ 6 de 59 galpones con **saldo negativo**. Este plan no es una cuarta guarda: es un **cambio de modelo**.

---

## 0. Por qué este plan es distinto de los 3 intentos anteriores

Los 3 fracasos comparten **una sola causa**: el modelo de v15 **borra** kg de una pantalla y **espera** que
otra los muestre. Mientras esa espera dura, los kg no existen en ningún lado, y toda guarda que se agregó
para tapar el agujero abrió otro.

> ### PRINCIPIO RECTOR — DIFERIMIENTO CON CONTRAPARTE (handoff suma cero)
>
> **La marca nunca quita kg de una pantalla si no hay, en el mismo acto, otra pantalla que los reciba.**
> El diferimiento se modela como una **ENTREGA al ciclo siguiente** (un movimiento de salida sintético en
> el último día visible del ciclo cedente), **no como un borrado de la fila de ingreso**.

Consecuencias inmediatas, cada una mata un fracaso concreto:

| Fracaso | Causa | Qué lo hace imposible ahora |
|---|---|---|
| Ronda 1 — el ingreso se veía en **4 lotes** (PA-67) y la fila abierta volcaba el galpón-día (13.000 por 5.000) | el predicado «¿existe un lote con primer seguimiento posterior?» no desempata entre lotes **sin** seguimiento | el destino se resuelve por **`fecha_encaset` mínima posterior** (criterio ya probado en ronda 2) y el diferimiento **exige que el destino TENGA seguimiento**; con lotes sin seguimiento el resultado es NEUTRO = idéntico a HEAD |
| Ronda 2 — cuadre **+5.000 permanente** en 33/35 galpones | el CTE `post` del cuadre, sin cota inferior, descontaba marcado que el destino **ya consumió** | **`fn_cuadre_alimento_engorde` no se toca**. Bajo este modelo el cuadre no necesita saber de la marca (demostración en §2.4) |
| Ronda 3 — **6 de 59 galpones con saldo negativo** (peor caso granja 43 / G0055, −8.840) | `pt_calc` acumulaba sobre **dos bases** dentro del mismo lote, y quitar kg de una fecha pasada deja negativas todas las filas siguientes | la entrega es **un solo delta negativo en el ÚLTIMO día visible** del cedente y está **topada** por su propio saldo a esa fecha ⇒ *no existe fila posterior que pueda quedar negativa*. `pt_calc` conserva **una sola base** |
| Los 3 — «invisible» como estado transitorio | v15 borra sin contraparte | la fila de ingreso **nunca** se borra: el documento y los kg siguen en la grilla del cedente el día real de llegada |

---

## 1. Las 3 reglas de negocio y la decisión de diseño que se deriva de cada una

Las reglas las definió el dueño del producto el 08-ago-2026. Son la **especificación**, no sugerencias.

### R1 — CICLOS QUE CONVIVEN

> Si dos ciclos conviven en el mismo galpón, el alimento marcado pertenece **A LOS DOS**. La marca no tiene
> que desempatar entre ciclos que conviven: comparten bodega, exactamente como ya se comportan hoy los
> movimientos SIN marcar (predicado CONVIVEN de v10 / `lotes_ajenos` de v11). La marca solo decide entre
> ciclos **SECUENCIALES** (los que no se solapan).

**Decisión D1 — la marca es NO-OP cuando el ciclo destino CONVIVE con el ciclo cedente.**
Un movimiento marcado en esa topología se comporta byte a byte como no marcado, en la fn y en el cuadre.

**Justificación contra los datos (lente CONVIVENCIA):** el código ya implementa R1 correctamente y **no
hay nada que agregar**. El predicado de convivencia (solape de rangos de seguimiento) está escrito 4 veces
con el mismo criterio y todas las variantes son coherentes: `consumo_galpon_por_fecha` (v10, líneas
470-478) lo incluye; `lotes_ajenos` (v11, 437-442) es su complemento exacto; `corte_apertura` (v12,
386-388) no mueve el piso; `corte_ciclo_siguiente` (v14, 417) no corta `fecha_max`. Además `hist_full`,
`hist_alimento`, `docs_por_fecha` y `fechas_universo` filtran **solo por ubicación** (granja+núcleo+galpón),
sin ningún predicado de lote.

Medición en los 4 pares reales que conviven: los dos lotes ven el mismo movimiento por dos canales (fila
diaria para el que ya venía corriendo, **apertura** para el que arrancó después) y el saldo cierra idéntico:

| galpón | lotes | saldo final A | saldo final B | **dif_saldo** |
|---|---|---|---|---|
| 105/G0491 | 175 / 176 | 10.699,52 | 10.699,52 | **0,00** |
| 105/G0492 | 177 / 178 | 17.761,52 | 17.761,52 | **0,00** |
| 106/G0479 | 179 / 180 | 1.576,47 | 1.576,47 | **0,00** |
| 106/G0490 | 168 / 169 | 19.393,56 | 19.393,56 | **0,00** |

Lo que rompe R1 son **4 líneas de v15** (`fn_seguimiento_diario_engorde.sql:615, 761, 790, 826`), que
excluyen el movimiento marcado de la diaria de **cualquier** lote con seguimiento, conviva o no. El caso no
es teórico: de 19 fechas con movimiento en esos 4 pares, **17 dan resultados iguales en los dos lotes** y
las 2 restantes (G0491 17-jul con 6.087 kg, G0490 03-jul) son exactamente las fechas donde una marca
partiría en dos un número que hoy es el mismo. **D1 es la única forma de conservar `dif_saldo = 0,00`.**

### R2 — LIQUIDACIÓN

> Al liquidar un lote el galpón tiene que quedar en **CERO**; el procedimiento operativo es que al cerrar el
> lote **trasladan** el alimento sobrante fuera del galpón. «Lote destino liquidado con alimento marcado
> pendiente» **no** es un caso a modelar con guardas: es una **ANOMALÍA que el sistema debe SEÑALAR** (el
> cuadre ya es el detector natural), no esconder ni compensar.

**Decisión D2 — nada de guardas compensatorias. El diferimiento se TOPA al saldo real del cedente y el
excedente no diferible se SEÑALA.** El sistema nunca «inventa» kg para que un caso raro cierre: entrega lo
que el cedente todavía tiene, y el resto queda como anomalía visible.

**Decisión D2b — modelar el diferimiento como la operación que la realidad ya hace.** R2 dice que el
procedimiento físico es *trasladar el sobrante*. La entrega sintética de este plan **es exactamente eso en
los libros**: el cedente cierra en 0 y el destino abre con esos kg. El modelo deja de pelearse con la
operación y empieza a describirla.

**Justificación contra los datos (lente LIQUIDACIÓN):**
- El backend **ignora** el alimento al liquidar: en `LoteAveEngordeService.CerrarLoteAsync` (línea 542 en
  adelante) `grep -i alimento` devuelve **una** línea, la 602 (`SaldoAlimentoEngordeAplicador
  .RecalcularPorLoteAsync`). No hay lectura de `inventario_gestion_stock`, ni validación, ni guarda.
  `LiquidacionCongeladaGateCalculos` (lista cerrada B1..B10) no contempla alimento.
- El front avisa pero **no bloquea** (`modal-liquidacion-lote-engorde.component.ts:459`,
  `puedeLiquidarPorAves` es `return true` literal), y ese aviso **ya es un falso positivo**: si el galpón no
  tiene filas de alimento hace *fallback a stock de NÚCLEO* (ts:375-383) y muestra el stock de los galpones
  vecinos. Verificado en SAN GUILLERMO (granja 37, núcleo 198400): 7 de 11 galpones con stock 0 disparan
  «Hay alimento en inventario» con los 19.160 kg de G0025-G0028. Un detector que miente entrena a la
  operación a ignorarlo.
- La anomalía **ya existe a escala**: de 84 liquidaciones congeladas vigentes (todas de ItalcolEcuador),
  **24 (28,6 %) congelaron con `saldo_alimento_kg > 0`, 111.821 kg**. Ponerle guardas al caso lo taparía;
  señalarlo es lo que pidió el dueño del producto.
- **Topología «destino liquidado con marcado pendiente»: no existe hoy en la BD** (búsqueda exhaustiva = 0)
  ⇒ es un caso a **definir y testear**, no a inferir de datos.

### R3 — SIN DESTINO

> Si un movimiento marcado no tiene ciclo destino todavía, el alimento **DEBE VERSE** en algún lado para que
> la operación pueda marcarlo/corregirlo. **«Invisible» NUNCA es una respuesta válida.** Prohibido cualquier
> diseño donde kilos reales dejen de aparecer en toda pantalla.

**Decisión D3 — la fila de ingreso NUNCA se borra.** El diferimiento se escribe como **salida en el último
día del cedente**, no como ausencia en el día del ingreso. R3 pasa de ser una condición que hay que vigilar
a ser una propiedad **estructural**: no existe camino de código que quite una fila de `fechas_universo`.

**Decisión D3b — fail-closed hacia HEAD.** Ante cualquier condición que no se pueda resolver (no hay
destino, el destino no tiene seguimiento, el destino está congelado, el cedente no tiene seguimiento, el
movimiento no es una entrada, está anulado), el estado es **NEUTRO** = comportamiento idéntico al de un
movimiento sin marca. La marca **suma** atribución cuando puede; nunca **resta** visibilidad.

**Justificación contra los datos (lente CORRECCIÓN):**
- Con D3 desaparece §2.3c **por construcción**: el hueco venía de que `fechas_universo` aplicaba la
  exclusión de la marca (línea 826) pero dejaba el corte `>= fecha_corte_alimento` fuera del disyunto. Si
  nada se excluye, no hay interacción posible con `fecha_corte_alimento`.
- La única superficie donde hoy el kilo marcado es indistinguible del no marcado es el **tab Stock**
  (`InventarioGestionService.cs:497` suma a `inventario_gestion_stock` antes y con independencia de la
  marca). El **tab Histórico** ya devuelve `ParaProximoCiclo` desde el backend (`:1806`) pero el front no lo
  pinta en ninguna de sus 15 columnas. El **Historial → Ingresos** sí lo pinta (badge + toggle,
  `inventario-historial-page.component.html:326-380`), con `Take(2000)` sin paginación.
- La rama **CONGELADA** de la fn ignora la marca por completo: una corrección posterior nunca se refleja en
  los 84 lotes ya liquidados. Es la razón de la guarda «destino congelado ⇒ NEUTRO» (§3, caso 5).

---

## 2. El modelo

### 2.1 Una sola función de atribución (una sola fórmula por número)

Se crea **`fn_alimento_marcado_atribucion(p_farm_id INT, p_nucleo_id TEXT, p_galpon_id TEXT)`**
(`backend/sql/fn_alimento_marcado_atribucion.sql`), **dueña única** de la decisión. Devuelve una fila por
movimiento marcado del galpón:

| columna | significado |
|---|---|
| `hist_id`, `fecha_operacion`, `kg`, `numero_documento` | el movimiento |
| `lote_cedente_id` | el ciclo en posesión del galpón en esa fecha |
| `lote_destino_id` | el ciclo que recibe |
| `fecha_entrega` | último día visible del cedente (donde se escribe la entrega) |
| `kg_diferido` | kg efectivamente entregados (topados) — `0` si NEUTRO |
| `kg_no_diferible` | residuo = anomalía R2 |
| `estado` | `DIFERIDO`, `DIFERIDO_PARCIAL`, `NEUTRO_SIN_DESTINO`, `NEUTRO_DESTINO_SIN_SEGUIMIENTO`, `NEUTRO_CONVIVENCIA`, `NEUTRO_DESTINO_LIQUIDADO`, `NEUTRO_CEDENTE_SIN_SEGUIMIENTO`, `NEUTRO_DENTRO_DEL_DESTINO`, `IGNORADA_NO_ENTRADA`, `IGNORADA_ANULADO` |
| `motivo` | texto para la UI de la Fase 2 |

La consumen **la fn diaria (como cedente y como destino), la bandeja de reservados (Fase 2) y el
señalamiento de anomalías (Fase 3)**. Ningún consumidor reimplementa el criterio. La fn diaria **no** llama
a la fn diaria: el helper recompone la serie del galpón desde el histórico y los seguimientos, sin
recursión.

**Rendimiento:** el helper devuelve vacío de inmediato si el galpón no tiene marcas. Requiere el índice
parcial `ix_lote_hist_para_proximo_ciclo ON lote_registro_historico_unificado (farm_id, nucleo_id,
galpon_id, fecha_operacion) WHERE para_proximo_ciclo` (hoy **no existe**: los 6 índices de la tabla son
`_pkey`, `uq_lote_hist_origen`, `ix_lote_hist_lote_fecha`, `ix_lote_hist_company_fecha`, `ix_lote_hist_tipo`,
`ix_lote_hist_farm_fecha`). Con 0 marcas el costo es ≈ 0, que es el estado de producción hoy.

### 2.2 Cómo se resuelve el destino y el cedente

- **`destino(m)`** = el lote de la ubicación (`deleted_at IS NULL`) con la **mínima `fecha_encaset`
  estrictamente posterior** a `DATE(m)`. Desempate: menor `lote_ave_engorde_id`.
  Criterio ya **probado correcto** en la ronda 2 (01-may→121, 16-may→121, 18-may→122) y es el que cierra la
  multiplicación entre lotes sin seguimiento de la ronda 1.
- **`cedente(m)`** = el lote de la ubicación con la **máxima `fecha_encaset` ≤ `DATE(m)`**. Desempate: mayor
  `lote_ave_engorde_id`. Definición **estructural** (no depende de `rango_final` ⇒ no hay circularidad).
- **`convive(A,B)`** = solape de rangos de seguimiento: `A.prim_seg <= B.ult_seg AND A.ult_seg >= B.prim_seg`.
  Mismo predicado de v10/v11 — un lote sin seguimiento nunca convive, igual que hoy.

Verificación de estas definiciones contra los 7 galpones testigo (topología leída hoy de la BD local):

| granja/galpón | lotes (encaset · primer seg → último seg) |
|---|---|
| 37/G0025 | 53 (25-ene · 27-ene→20-mar) → 70 (01-abr · 03-abr→20-may) → 189 (30-jul · 31-jul→07-ago) |
| 37/Galpon-11 | 25 (11-ene · 13-ene→13-mar) → 44 (18-mar · 20-mar→13-may) → 85 (31-may · 02-jun→09-jul) |
| 43/G0055 | 57 (11-ene) → 16 (18-mar) → 86 (31-may · 02-jun→18-jul) → 193 (03-ago · 04-ago→06-ago) |
| 96/PA-67 | 119, 120, 121, 122 (encasets 07-ene, 15-mar, 17-may, 20-may) — **los 4 sin ningún seguimiento** |
| 105/G0491 | 175 (17-jul · 16-jul→27-jul) ‖ 176 (20-jul · 19-jul→27-jul) — conviven |
| 105/G0492 | 177 (20-jul · 19-jul→25-jul) ‖ 178 (21-jul · 20-jul→26-jul) — conviven |
| 106/G0479 · 106/G0490 | 179 ‖ 180 · 168 ‖ 169 — conviven |

⚠️ Dato que obliga a una guarda: **`prim_seg` puede ser ANTERIOR al `fecha_encaset`** (lote 175: encaset
17-jul, primer seguimiento 16-jul). Por eso `DIFERIDO` exige además `DATE(m) < destino.prim_seg`; si no, el
destino ya ve el movimiento como fila propia y diferirlo lo haría desaparecer.

### 2.3 La ENTREGA (el corazón del rediseño)

Cuando el estado es `DIFERIDO`/`DIFERIDO_PARCIAL`:

- El **cedente conserva intacta** su fila del día real del ingreso: `ingreso_alimento_kg`, `documento`,
  y la fila en `fechas_universo`. **No se toca ninguna de las 4 exclusiones de v15: se revierten a v14.**
- El cedente **emite una salida sintética** en `fecha_entrega = rango_final.fecha_max` (que bajo `DIFERIDO`
  siempre existe y vale `destino.prim_seg − 1`, por `corte_ciclo_siguiente` de v14), por
  `kg_diferido = LEAST(Σ kg marcados hacia ese destino, saldo base del cedente a esa fecha)`, acreditada en
  `traslado_salida_kg` con `documento = 'Entrega al ciclo siguiente — <docs>'`.
- El **destino la recibe en su apertura** (`apert_mov`), por el **mismo** `kg_diferido` que salió del helper.

Propiedades que esto compra, y que hay que leer como el contrato del diseño:

1. **No puede generar filas negativas.** La entrega es un único delta negativo en el **último día visible**
   del cedente y está topada por su propio saldo a esa fecha ⇒ no queda ninguna fila posterior que restar.
   La invariante I1 del gate deja de ser una esperanza y pasa a ser una consecuencia.
2. **`pt_calc` conserva UNA SOLA BASE.** La entrega es un delta más de `hist_alimento`, exactamente como un
   `INV_TRASLADO_SALIDA` real. **Prohibido** introducir un segundo piso/serie (fue el defecto de la ronda 3).
   El tope se calcula desde una serie base **no expuesta** (variante sin piso de `saldo_running`, que la fn
   ya materializa para `saldo_close`) — es un escalar, no una segunda acumulación.
3. **El cedente CIERRA.** El motivo original de v15 para excluir la fila («evitar que sostenga
   artificialmente mi saldo y me impida cerrar por `saldo_close`») se cumple mejor: el saldo baja a 0 el
   último día y `saldo_close` dispara. Sin circularidad: `fecha_entrega = corte_ciclo_siguiente`, que es
   independiente del cálculo del propio cedente, y cae **en** el último día del rango, no antes.
4. **La grilla explica el número.** En vez de kg que se evaporan de una fila pasada, el operador ve una
   línea «Entrega al ciclo siguiente» el último día. Es lo contrario del parpadeo que motivó el feature.
5. **Contabilidad conserva la fecha real de llegada**, que es el pedido original (Ecuador aplica el
   workaround de re-fechar en 110 de 110 ciclos).

### 2.4 Por qué `fn_cuadre_alimento_engorde` NO se toca (demostración)

El cuadre compara, **por galpón**, el saldo del **ciclo activo** `A` (`rn = 1`, el de mayor `seg_max`)
contra `stock − mov_post`, donde `mov_post` son los movimientos posteriores a `A.seg_max`. Bajo este modelo:

- **El cedente nunca es el ciclo activo.** `DIFERIDO` exige que el destino sea secuencial ⇒
  `destino.prim_seg > cedente.ult_seg` ⇒ `destino.seg_max > cedente.seg_max` ⇒ `A ≠ cedente`. Por lo tanto
  **la entrega jamás entra en el número que mira el cuadre**.
- **Si `A = destino`:** el saldo de `A` gana `kg_diferido` vía apertura y el stock físico ya los tenía
  (estaban en la bodega, sin consumir — eso lo garantiza el tope) ⇒ el descuadre **mejora** de `−kg` a `0`.
  Es literalmente el defecto que el feature vino a arreglar («sin marca ese mismo ingreso dejaba al nuevo en
  −300 kg»).
- **Si `A ≠ destino`** (el destino ya cerró y vino otro ciclo después): los kg entraron a la apertura del
  destino y se consumieron ahí; el stock ya no los tiene y el saldo de `A` tampoco los cuenta ⇒ **cero
  cambios**. Este es exactamente el punto donde la ronda 2 los restaba de nuevo y producía **+5.000
  permanente en 33/35 galpones**: al no tocar el cuadre, ese error es inconstruible.
- **NEUTRO:** identidad con HEAD.

⇒ **Fase 1 no modifica una sola línea de `fn_cuadre_alimento_engorde`.** El cuadre sigue siendo el detector
**independiente** (que es lo que R2 quiere), y el gate lo usa como invariante, no como parte del fix.
Línea base a re-medir antes de empezar: **61 filas, 1 descuadrado preexistente (Panamá, lote 182)**.

---

## 3. Semántica COMPLETA de la marca — tabla de casos

`d` = `DATE(fecha_operacion)` del movimiento marcado. **Ningún caso termina en «no se ve en ningún lado».**

| # | Caso | Estado | fn diaria — CEDENTE | fn diaria — DESTINO | `fn_cuadre_alimento_engorde` | Dónde se ve el alimento |
|---|---|---|---|---|---|---|
| 1 | **Convivencia** — `destino` existe, tiene seguimiento y **convive** con `cedente` (R1) | `NEUTRO_CONVIVENCIA` | fila de ingreso intacta (kg + documento); **sin** entrega | lo ve por el canal de siempre: fila diaria si `d` cae en su rango, `apertura` si `d < fecha_min` | **sin cambio** (idéntico a HEAD) | grilla de **los dos** lotes; `dif_saldo` sigue en 0,00; Stock; Histórico; Historial→Ingresos |
| 2 | **Secuencial con destino operativo** — `destino` existe, tiene seguimiento, **no** convive, no está congelado, `d < destino.prim_seg`, y el cedente tiene respaldo | `DIFERIDO` | fila de ingreso intacta **+ fila de ENTREGA** (`traslado_salida_kg = kg`, doc «Entrega al ciclo siguiente») en `fecha_max` | `apertura_alimento_kg += kg`; el documento viaja en `apertura_documentos` | **mejora o queda igual**: si `A = destino` pasa de `−kg` a `0`; si `A ≠ destino`, sin cambio | grilla del cedente (ingreso **y** entrega) + apertura del destino + Stock + Histórico |
| 3 | **Secuencial sin destino operativo** — `destino` existe pero **aún no tiene seguimiento** (p. ej. lote recién creado) | `NEUTRO_DESTINO_SIN_SEGUIMIENTO` | fila de ingreso intacta; **sin** entrega | el lote sin seguimiento ya muestra los movimientos de su ventana como filas propias (comportamiento actual) | **sin cambio** | grilla del cedente + vista pre-seguimiento del destino + Stock + Histórico + **bandeja de reservados** (Fase 2) |
| 4 | **Sin destino** — no hay ningún lote del galpón con `fecha_encaset > d` | `NEUTRO_SIN_DESTINO` | fila de ingreso intacta; **sin** entrega | — | **sin cambio** | grilla del cedente + Stock + Histórico + **bandeja de reservados** (Fase 2) |
| 5 | **Destino LIQUIDADO / congelado** (`liquidacion_lote_engorde_congelada` vigente) | `NEUTRO_DESTINO_LIQUIDADO` | fila de ingreso intacta; **sin** entrega | su foto congelada no cambia (nunca se reescribe) | **sin cambio** | grilla del cedente + Stock + Histórico + **anomalía señalada** (Fase 3) |
| 6 | **Sin respaldo** — el cedente ya consumió los kg (saldo base a `fecha_entrega` < kg marcados) | `DIFERIDO_PARCIAL` (o NEUTRO si el tope da 0) | entrega **topada** al saldo real; nunca deja el saldo bajo cero | recibe **solo** lo entregado | **sin cambio** (el cedente no es el activo) | grilla del cedente + `kg_no_diferible` en la **anomalía R2** (Fase 3); nada desaparece |
| 7 | **Movimiento ANULADO** con la marca puesta | `IGNORADA_ANULADO` | inerte en todas partes (`NOT h.anulado` ya está en todos los CTE) | — | **sin cambio** | Historial→Ingresos lo muestra anulado; no aporta kg a ninguna pantalla (correcto: no existen) |
| 8 | **SALIDA marcada** (`INV_TRASLADO_SALIDA` u otro tipo no-entrada) | `IGNORADA_NO_ENTRADA` | se comporta como salida normal | — | **sin cambio** | grilla del cedente + Histórico |
| 9 | **Cedente sin seguimiento** (galpón con lotes que nunca cargaron seguimiento, p. ej. 96/PA-67) | `NEUTRO_CEDENTE_SIN_SEGUIMIENTO` | no hay `fecha_max` donde escribir la entrega ⇒ nada se difiere | — | **sin cambio** | los lotes sin seguimiento siguen mostrando el movimiento como hoy (los 4 de PA-67) |
| 10 | **`d` ya cae dentro del rango del destino** (`d >= destino.prim_seg`; posible porque `prim_seg` puede preceder al encaset) | `NEUTRO_DENTRO_DEL_DESTINO` | fila intacta | ya lo ve como fila diaria propia | **sin cambio** | grilla del cedente y/o del destino |
| 11 | **Movimiento sin galpón** | no marcable | — | — | — | el endpoint ya lo rechaza (`InventarioGestionService.cs:2493`); los 457 ingresos EC sin galpón son insumo/medicamento/gas, **0 de alimento** |

**Regla de cierre (D3b):** cualquier condición no contemplada ⇒ **NEUTRO**. Fail-closed hacia el
comportamiento de HEAD, que es el único estado ya validado en producción.

**Caso 8 es un defecto vivo de v15:** hoy `apert_mov`, `hist_full`, `hist_alimento`, `docs_por_fecha` y
`fechas_universo` incluyen `INV_TRASLADO_SALIDA` en el disyunto de la marca, así que **una salida marcada
saldría de la diaria del cedente y entraría como delta NEGATIVO a la apertura del destino**. Se cierra
explícitamente restringiendo la marca a entradas dentro de la fn (el endpoint ya la restringe al escribir,
pero la fn no puede confiar en eso: la carga masiva y el espejo escriben por otros caminos).

---

## 4. Arquitectura y archivos

### 4.1 SQL (`backend/sql/`)

| Archivo | Cambio |
|---|---|
| `fn_alimento_marcado_atribucion.sql` | **NUEVO.** Dueño único de la atribución (§2.1). Cabecera con changelog, como el resto de las fns |
| `fn_seguimiento_diario_engorde.sql` | **v16.** (a) revertir a la forma de v14 las 4 exclusiones de la marca (líneas 615, 761, 790, 826); (b) `apert_mov`: el disyunto marcado pasa a `lote_destino_id = p_lote_id` con `kg_diferido` del helper, reemplazando la guarda por «primer seguimiento interpuesto»; (c) nuevo CTE `entrega_ciclo_siguiente` + su fusión en `hist_full` / `hist_alimento` / `docs_por_fecha` / `fechas_universo`; (d) CTE escalar del tope (variante sin piso de `saldo_running`). **La firma NO cambia** ⇒ `CREATE OR REPLACE` alcanza, sin `DROP FUNCTION` (la entrega reusa `traslado_salida_kg` + `documento`) |
| `create_lote_registro_historico_unificado.sql` | solo anotación del índice parcial nuevo, si corresponde |
| `verificar_marca_proximo_ciclo.sql` | **NUEVO.** El gate ejecutable de §6 (A/B con la marca prendida, tx + ROLLBACK) |
| `fn_cuadre_alimento_engorde.sql` | **NO SE TOCA** (§2.4) |

⚠️ **Un `.sql` cambiado sin migración queda MUERTO** (aprendizaje del repo, 9 días de divergencia en prod).
Cada cambio de fn va **también** en su migración EF, byte a byte.

### 4.2 Migraciones EF (`backend/src/ZooSanMarino.Infrastructure/Migrations/`)

Última aplicada: `20260808130000`. Se agregan, en orden:

1. `2026080915xxxx_IndiceParcialMarcaProximoCiclo` — `CREATE INDEX IF NOT EXISTS` (idempotente).
2. `2026080915xxxx_FnAlimentoMarcadoAtribucion` — `CREATE OR REPLACE FUNCTION` del helper.
3. `2026080915xxxx_FnSeguimientoEngordeV16EntregaCicloSiguiente` — `CREATE OR REPLACE` de la fn diaria,
   con el `Down()` reponiendo v15 completa.

Sin `Recalcular…` masivo: con **0 marcas** no hay una sola fila persistida que cambie. *(El descuadre
persistido vs fn de Panamá —69 filas, hasta 23.355 kg— es un pendiente **anterior** a este trabajo y sigue
fuera de alcance; ver §8.)*

### 4.3 Backend C#

| Archivo | Cambio |
|---|---|
| `Application/Calculos/SaldoAlimentoEngordeCalculos.cs` | reescribir `EntraPorMarcaProximoCiclo` (líneas 212-233) y **eliminar/reemplazar** `ExcluidoDeFilaDiariaPorMarca` (248-249, hoy `marcado && miPrimerSeguimiento.HasValue` — la simplificación que causó los 17 falsos verdes). Nuevas primitivas puras: `ResolverDestino`, `ResolverCedente`, `Conviven`, `EstadoAtribucion`, `TopeEntrega` |
| `Application/Calculos/SeguimientoAvesEngordeCalculos.cs` | actualizar las 3 llamadas (líneas 100, 164, 228) a la semántica nueva |
| `Infrastructure/Services/InventarioGestionService.cs` | `ActualizarDestinoCicloAsync` (≈2470-2520): el mensaje/aviso debe reportar el **estado resuelto** («se difiere al lote X», «queda reservado: el ciclo destino todavía no existe») en vez de un texto fijo. Además, tras togglear, `RefrescarSaldoAlimentoEngordeAsync` ya recalcula por ubicación ✅ |
| `Infrastructure/Services/SaldoAlimentoEngordeAplicador.cs` | **no cambia su lógica**, pero hay que **invocar `RecalcularPorUbicacionAsync` en el cruce de umbral**: cuando un lote carga su **primer** seguimiento en un galpón con movimientos marcados, el estado pasa de `NEUTRO_DESTINO_SIN_SEGUIMIENTO` a `DIFERIDO` y el saldo persistido del cedente queda viejo |

### 4.4 Frontend (solo Fase 2a, ver §5)

| Archivo | Cambio |
|---|---|
| `features/gestion-inventario/pages/inventario-gestion-page/…` (tab **Histórico**) | agregar la columna «Próx. ciclo»: el backend **ya** la devuelve (`InventarioGestionService.cs:1806`) y el front no la pinta en ninguna de sus 15 columnas |
| grilla de seguimiento de engorde | la fila de entrega ya llega con `traslado_salida_kg` + `documento`: verificar que se renderiza sin cambios de componente |

---

## 5. Fases

### 🟢 FASE 1 — NÚCLEO · **ENTRA AHORA**

- **F1.1** `fn_alimento_marcado_atribucion` + índice parcial + migraciones idempotentes.
- **F1.2** fn diaria **v16**: revertir las 4 exclusiones de v15; apertura por `lote_destino_id`; CTE de
  entrega con tope; marca restringida a entradas; guardas de los casos 3, 5, 9 y 10 de §3.
- **F1.3** espejo C# (`SaldoAlimentoEngordeCalculos`, `SeguimientoAvesEngordeCalculos`) + **tests que
  construyen las topologías** (§6, G3).
- **F1.4** recálculo del saldo persistido al **cruzar el umbral** (primer seguimiento de un lote en un
  galpón con marcados) vía `RecalcularPorUbicacionAsync`.
- **F1.5** **decisión explícita: el cuadre NO se toca** (§2.4). La obligación de prueba es del gate.
- **F1.6** `backend/sql/verificar_marca_proximo_ciclo.sql` (el gate ejecutable).

**Criterio de entrada a Fase 2:** el gate de §6 sale **GO**, leído por alguien que no escribió el fix.

### 🟡 FASE 2a — VISIBILIDAD BARATA (R3) · **ENTRA AHORA** (riesgo ≈ 0, solo lectura)

- **F2a.1** columna «Próx. ciclo» en el tab **Histórico** (el dato ya viaja; es front puro).
- **F2a.2** verificar en pantalla que la fila de **entrega** se lee bien (etiqueta y signo).

### 🟠 FASE 2b — BANDEJA DE ALIMENTO RESERVADO (R3 operativo) · **NO AHORA**

Endpoint + pantalla que lista, por empresa/granja, los movimientos marcados con su `estado` y `motivo`
(directo del helper), con la corrección en línea (el `PUT /ingresos/{id}/destino-ciclo` ya existe). Es lo
que convierte «se ve en la grilla del cedente» en «la operación lo encuentra sin saber dónde buscar».
Se difiere porque **no es necesaria para cumplir R3** bajo D3 (nada desaparece) y agrega superficie nueva.

### 🔴 FASE 3 — SEÑALAMIENTO DE LA ANOMALÍA (R2) · **NO AHORA**

- **F3.1** `fn_cuadre_alimento_engorde` gana columnas informativas (`marcado_no_diferible_kg`,
  `liquidado_con_saldo_kg`). ⚠️ Cambia el `RETURNS TABLE` ⇒ **exige `DROP FUNCTION`** y toca una fn
  compartida por 5 consumidores ⇒ riesgo alto, va sola y después.
- **F3.2** reporte «lotes liquidados con alimento sin trasladar»: hoy **24 de 84 (28,6 %), 111.821 kg**.
- **F3.3** arreglar el **falso positivo del aviso de liquidación**: el fallback a stock de **núcleo**
  (`modal-liquidacion-lote-engorde.component.ts:375-383`) muestra el stock de los galpones vecinos —
  7 de 11 galpones de SAN GUILLERMO avisan con 19.160 kg ajenos. Es un bug independiente del rediseño y
  **degrada el único detector de R2 que hoy ve un humano**.
- **F3.4** exponer `GET /api/CuadreAlimentoEngorde` en alguna pantalla (hoy tiene **cero** consumidores en
  el front; solo emite un `LogWarning`).

---

## 6. LA COMPUERTA (explícita y ejecutable)

Los 4 aprendizajes de las rondas fallidas están incorporados como compuertas, no como comentarios.

### G0 — Identidad SIN marcas · **necesaria, JAMÁS suficiente**

> 🔴 En las 3 rondas fallidas la identidad dio **0/0 siempre**, incluida la ronda que producía saldos
> negativos. Con `para_proximo_ciclo` en 0 filas, **todo pasa**. Nadie declara GO con esto.

```bash
# ANTES del cambio (congela la línea base)
psql "postgresql://postgres:***@127.0.0.1:5433/sanmarinoapplocal" -f backend/sql/verificar_paridad_saldo_engorde.sql
# ... aplicar v16 ...
psql ... -f backend/sql/verificar_paridad_saldo_engorde.sql        # DESPUÉS (compara)
```
Cobertura obligatoria (las 5 fns, **las dos empresas**): diaria (5.804 filas), cuadre (61), aves (172),
costos (224), informe semanal (898). **Toda empresa que no sea el objetivo: 0 en todas las columnas.**

### G1 — A/B con la marca **PRENDIDA** sobre movimientos **REALES** · **ésta sí es la compuerta**

`backend/sql/verificar_marca_proximo_ciclo.sql` (nuevo, line endings **LF**). Para cada movimiento
candidato del censo: `SAVEPOINT` → `UPDATE … SET para_proximo_ciclo = TRUE` (histórico **y** espejo) →
recalcular las 5 fns para **todos** los lotes del galpón → registrar deltas contra HEAD → `ROLLBACK TO`.
Escala de referencia de las rondas anteriores: **59 galpones reales / 2.344 movimientos**. Todo dentro de
una transacción, con verificación final de **0 rastro** (`SELECT count(*) … WHERE para_proximo_ciclo` = 0).

Invariantes evaluados **por movimiento** (cualquiera que falle ⇒ **NO-GO**, sin excepción negociable):

| Id | Invariante | Umbral | Fracaso que ataja |
|---|---|---|---|
| **I1** | **Ninguna fila diaria queda negativa** — `COUNT(*) FILTER (WHERE saldo_alimento_kg < -0.001)` en TODO el universo | `0`, y sin aumentar respecto de HEAD en ningún galpón | Ronda 3 (**6 de 59** vs 0 de 59) |
| **I2** | **Conservación (suma cero)** — por galpón, `Σ_lotes vivos (apertura_alimento_kg + Σ(ingreso + traslado_entrada − traslado_salida) de sus filas)` invariante vs HEAD | diferencia `0,00` | kg que se evaporan **o** que se duplican |
| **I3** | **Visibilidad (R3)** — todo movimiento marcado aparece en ≥ 1 lote del galpón (fila con su documento **o** `apertura_documentos`) | `COUNT(marcados invisibles) = 0` | «invisible en el 100 % de las pantallas» (rondas 1-2) |
| **I4** | **No multiplicación** — el marcado lo cuenta **el mismo número de ciclos** que sin marca | igualdad exacta | Ronda 1 (se veía en **4 lotes** en vez de 1) |
| **I5** | **Cuadre** — `descuadre_kg` no se aleja de 0 en ningún galpón y `filas_negativas` sigue en 0 | ≤ línea base (61 filas, 1 preexistente: Panamá lote 182) | Ronda 2 (+5.000 en 33/35) |
| **I6** | **R1 convivencia** — en los 4 pares reales, `dif_saldo` entre los dos lotes sigue en **0,00** en toda fecha, con marca y sin marca | `0,00` exacto (10.699,52 · 17.761,52 · 1.576,47 · 19.393,56) | romper la bodega compartida |
| **I7** | **Rendimiento** — tiempo de `fn_cuadre_alimento_engorde(NULL)` (llama a la fn diaria 61 veces) | ≤ 1,5× la línea base | el helper anidado degradando 5 consumidores |

> 🔴 **El cuadre solo mira lotes CON seguimiento** ⇒ es **ciego al lote destino recién creado**, que es justo
> donde aparecieron los negativos de la ronda 3. Por eso I5 **no reemplaza** a I1/I2/I3: los cuatro corren.

### G2 — Censo, no muestra

El A/B recorre **todos** los galpones con movimientos de alimento de **las dos empresas**, no los 7
testigos. Los testigos de §7 son casos con veredicto **esperado escrito de antemano**; el censo es la red.

### G3 — Tests C# que **construyen** las topologías rotas (anti falso-verde)

> 🔴 Los 17 tests del primer intento pasaron **con los defectos adentro** por hardcodear
> `miPrimerSeguimiento: null`. Un test que no puede construir la topología rota es un **falso verde**.

- Los helpers de test deben aceptar un **galpón completo**: lista de ciclos con `fecha_encaset`,
  `primer_seg`, `ultimo_seg`, `congelado`, más los movimientos con `tipo_evento`, `anulado` y `marca`.
  Si el helper no puede expresar «destino sin seguimiento» o «cedente sin respaldo», **el helper se arregla
  primero**.
- **Cobertura obligatoria: los 11 casos de §3**, uno por uno, con su valor esperado.
- **Prueba de mutación manual y registrada:** por cada guarda nueva, comentarla, correr los tests y
  **verificar que se ponen en rojo**. Una guarda cuyo test sigue verde al quitarla no está testeada. El
  resultado se anota en el tracker.
- Archivos: `backend/tests/ZooSanMarino.Application.Tests/AtribucionAlimentoMarcadoCalculosTests.cs`
  (nuevo) + actualizar `AperturaAlimentoEngordeV15CalculosTests.cs` y `SaldoAlimentoEngordeCalculosTests.cs`.
- ⚠️ `pt_calc` **no tiene espejo C#** ⇒ los tests C# **no son** la compuerta del saldo: la compuerta del
  saldo es G1 en SQL. Los tests son la compuerta de la **atribución**.

### G4 — Quién declara GO

> 🔴 **El que corrige no declara GO.** En la ronda 2 el agente que aplicó los fixes se autoevaluó verde y la
> verificación independiente encontró después la regresión de los 6 galpones negativos.

El gate lo ejecuta y lo lee una sesión/agente que **no** escribió la v16. Veredicto por escrito en el
tracker, con los números crudos de I1..I7.

### G5 — Disciplina de BD local

Postgres 17 en `127.0.0.1:5433`, db `sanmarinoapplocal` (credenciales en
`backend/src/ZooSanMarino.API/appsettings.Development.json`). **Toda escritura en transacción con
`ROLLBACK`**, y verificación final de 0 rastro. Scripts con line endings **LF** (`psql.exe` duplica el CR).
Puede haber otra sesión sobre la misma BD: si un statement se bloquea > 2 min, cancelar y reportar.
Al terminar: `make down` / sin procesos huérfanos; build servers apagados.

---

## 7. Casos de prueba concretos (galpones REALES, veredicto escrito de antemano)

Topología verificada hoy contra la BD local. Cada caso se ejecuta con `SAVEPOINT`/`ROLLBACK TO`.

| # | Galpón | Movimiento a marcar | Estado esperado | Veredicto esperado |
|---|---|---|---|---|
| **P1** | **96/PA-67** (Panamá, company 5) — **4 lotes sin ningún seguimiento** (119, 120, 121, 122; encasets 07-ene, 15-mar, 17-may, 20-may) y **0 movimientos históricos** | inyectar un ingreso de 5.000 kg (p. ej. 01-may y 18-may) y marcarlo | `NEUTRO_CEDENTE_SIN_SEGUIMIENTO` / `NEUTRO_DESTINO_SIN_SEGUIMIENTO` | **Idéntico a HEAD.** El movimiento se sigue viendo en los mismos lotes que sin marca (HEAD: 4). ⛔ Si aparece en menos o en más lotes ⇒ NO-GO (es la ronda 1: «20.000 kg por 5.000») |
| **P2** | **105/G0491** — 175 (enc 17-jul, seg 16→27 jul) ‖ 176 (enc 20-jul, seg 19→27 jul) | el ingreso real del **17-jul** y el del **22-jul (6.087 kg)** | `NEUTRO_CONVIVENCIA` (17-jul) · `NEUTRO_SIN_DESTINO` (22-jul) | `dif_saldo` entre 175 y 176 sigue **0,00**; saldo final **10.699,52** en los dos. R1 intacto |
| **P3** | **105/G0492** (177 ‖ 178) · **106/G0479** (179 ‖ 180) · **106/G0490** (168 ‖ 169) | ingresos reales de cada par, incluido el **03-jul de 18.733,56 kg** de G0490 | `NEUTRO_CONVIVENCIA` | saldos finales **17.761,52 · 1.576,47 · 19.393,56**, iguales en ambos lotes del par |
| **P4** | **37/G0025** — cadena **53 → 70 → 189** con alimento ya consumido | `id 6337` (19-may, 120 kg, `INV_TRASLADO_ENTRADA`, etiquetado 70) y `id 6245` (16-may, 1.680 kg) | `DIFERIDO` o `DIFERIDO_PARCIAL` (destino **189**, encaset 30-jul, seg 31-jul) | lote 70 conserva la fila del 19-may **y** emite entrega el 30-jul; 189 abre con esos kg; **I1 = 0 negativas**; cuadre igual o mejor. ⛔ Si el cuadre se mueve a +kg ⇒ es la ronda 2 |
| **P5** | **37/G0025** — anulados | `id 13266` (31-jul, 2.280 kg, **anulado**, mismo documento `63029` que el vigente `id 13307`) | `IGNORADA_ANULADO` | **cero efecto** en toda pantalla y en las 5 fns |
| **P6** | **37/Galpon-11** — cadena 25 → 44 → 85, **sin ciclo posterior a 85** | `id 9087` (08-jul, 3.600 kg) e `id 8231` (01-jul, 16.120 kg) | `NEUTRO_SIN_DESTINO` | idéntico a HEAD; **0 filas negativas** (es uno de los galpones donde la ronda 3 las produjo) |
| **P7** | **43/G0055** — 86 (seg 02-jun→18-jul) → 193 (enc 03-ago, seg 04-ago→06-ago) | `id 14047` (**04-ago, 5.600 kg**, etiquetado 193) — el testigo del **−8.840** de la ronda 3 | `NEUTRO_SIN_DESTINO` (no hay lote con encaset > 04-ago) | **saldo NUNCA negativo.** Este es el caso que hundió la ronda 3; bajo el modelo nuevo la marca es inerte |
| **P8** | **43/G0055** — el traslado de cierre real | los 14 `INV_TRASLADO_SALIDA` del 27-jul al 31-jul (0173…0188, ~1.200 kg c/u) | `IGNORADA_NO_ENTRADA` | la marca sobre una salida **no** puede restar en la apertura del destino (defecto vivo de v15) |
| **P9** | **43/G0055** — marcado sin respaldo | `id 7189` (08-jun, 11.520 kg, dentro del ciclo 86, que lo consumió) | `DIFERIDO_PARCIAL` con tope, o NEUTRO si el tope da 0 | 193 recibe **solo** lo que quedaba; **0 filas negativas**; el residuo queda como `kg_no_diferible` |
| **P10** | **destino LIQUIDADO** — no existe en la BD (búsqueda exhaustiva = 0) | construir con `SAVEPOINT`: congelar el destino y marcar un movimiento anterior | `NEUTRO_DESTINO_LIQUIDADO` | los kg siguen visibles en el cedente. ⛔ Si desaparecen ⇒ R3 violado ⇒ NO-GO |
| **P11** | **cruce de umbral** — `NEUTRO_DESTINO_SIN_SEGUIMIENTO` → `DIFERIDO` | en 37/G0025: borrar (en tx) el primer seguimiento de 189, marcar, y volver a crearlo | transición de estado | el saldo **persistido** del cedente se refresca (`RecalcularPorUbicacionAsync`). Sin esto, la grilla y la tabla persistida divergen |
| **P12** | **regresión E1 de la auditoría** — granja 42/G0049/lote 132 | `hist 13968` / `mov 10720`: 7.000 kg del 06-ago, doc `005-001-000063560`, **sin ciclo posterior** | `NEUTRO_SIN_DESTINO` | la fila del 06-ago conserva `ingreso 7.000`, `saldo 11.260` y el documento. ⛔ Si vuelve a `0 / 4.260 / vacío` ⇒ el rediseño falló en su objetivo principal |

---

## 8. Qué NO se toca, y por qué

| Área | Motivo |
|---|---|
| **`fn_cuadre_alimento_engorde`** (fórmula) | Bajo el modelo de entrega no lo necesita (§2.4), y tocarlo fue **exactamente** el error de la ronda 2 (+5.000 en 33/35 galpones). Debe seguir siendo el detector **independiente** — si forma parte del fix, deja de poder validarlo. Las columnas informativas son Fase 3 |
| **Rama CONGELADA** (`liquidacion_lote_engorde_congelada[_fila]`) | 84 fotos vigentes. CLAUDE.md: la liquidación congelada no se reescribe. Un lote liquidado no vuelve a mostrar su apertura — divergencia consciente ya declarada en v15 |
| **Columna `para_proximo_ciclo` y su trigger** (migración `20260808120000`, commiteada) | El modelo de datos está bien: el problema es cómo se **interpreta**. Cambiar el esquema obligaría a una migración de datos sobre 0 filas: puro riesgo sin beneficio |
| **`vw_seguimiento_pollo_engorde`** | Es una **reimplementación set-based** que no invoca la fn (Power BI, `backend/documentacion/VISTAS_POWERBI_POLLO_ENGORDE.md`). Sincronizarla dobla la superficie del cambio. Divergencia **documentada** y sin impacto hoy (0 marcas); queda como seguimiento |
| **`dias_alimento_previo_encaset` / excepción D4** | Hueco §2.3a de la auditoría (backend + 184 líneas de test escritos, el front la bloquea en 3 lugares y no hay endpoint que exponga la ventana). Es **otro** feature; mezclarlo impide leer el A/B |
| **`ReporteContableService.cs`, `ReporteContableBultosCalculos.cs`, `FarmInventoryMovementService.cs`, `CatalogItemService.cs`, `.devpilot/`** | **Sesiones paralelas** trabajando esos archivos |
| **§2.1 saldo de bultos con consumo restado dos veces · §2.2 inventario de postura partido sin puente · §2.4 kardex de granja por lote padre** | Defectos **preexistentes** de postura/inventario, ajenos al engorde y a la marca. Tienen su propio bloque en el tracker |
| **Descuadre persistido vs fn en Panamá** (69 filas, hasta 23.355 kg) | Detectado en la auditoría, **no** determinado si necesita la migración `Recalcular…`. Anterior a este trabajo; con 0 marcas la v16 no lo mueve. Se re-mide en G0 como control, no se arregla acá |
| **Decidir por empresa/país** (`if (pais == X)`) | CLAUDE.md prohíbe el anti-patrón. La marca es **dato por movimiento**, no un flag de tenant: no se agrega ninguna columna a `companies` |
| **Commits / push / deploy** | El orquestador commitea. Esta sesión **no** commitea |

---

## 9. Riesgos abiertos y decisiones que quedan a la vista

1. **La transición es temporal:** un movimiento pasa de `NEUTRO_DESTINO_SIN_SEGUIMIENTO` a `DIFERIDO` el día
   en que el destino carga su primer seguimiento, y la grilla del cedente **cambia retroactivamente** (gana
   su fila de entrega). Es inherente al feature y ya lo era en v15; lo nuevo es que ahora **se ve** (fila
   explícita) en vez de que los kg se evaporen. Requiere F1.4 o la tabla persistida queda vieja.
2. **El tope puede sorprender:** en un galpón bien operado (43/G0055 saca el sobrante con 14 traslados de
   cierre), el saldo del cedente al final es ≈ 0 y un marcado *dentro* del ciclo se difiere **parcialmente o
   nada**. Es correcto —no se puede entregar lo que ya se comió— pero la UI de la Fase 2b tiene que decirlo
   con todas las letras o la operación creerá que la marca «no funciona».
3. **El uso primario no necesita tope:** el caso real es el ingreso que cae en el **hueco** entre ciclos y
   más de `N` días antes del encaset (39 de 110 encasets 2026 de Ecuador, 35 %). Ahí el saldo del cedente lo
   cubre entero y el handoff es limpio. El tope es el **anti-abuso**, no el camino feliz.
4. **Rendimiento anidado:** la fn diaria pasa a llamar al helper, y el cuadre llama a la fn 61 veces. Con 0
   marcas y el índice parcial el costo debería ser ≈ 0; **I7 lo mide**, no se asume.
5. **La marca sigue sin dueño en la UI:** `GET /api/CuadreAlimentoEngorde` no tiene **ningún** consumidor en
   el front y el aviso de liquidación miente en 7 de 11 galpones de una granja real. Mientras eso siga así,
   la anomalía de R2 la detecta el sistema pero **no la lee nadie**. Es el argumento para hacer la Fase 3.
