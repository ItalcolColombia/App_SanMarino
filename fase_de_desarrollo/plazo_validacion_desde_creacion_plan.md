# El plazo de validación debería contarse desde la CREACIÓN, no desde la fecha del registro

> **Estado: ANÁLISIS Y VALIDACIÓN. Nada implementado.** Se abre en su propia sesión.
>
> **Origen:** propuesta del usuario (25-ago-2026), tras cerrar el defecto del cruce
> (`cruce_reproductora_nace_sin_validar_plan.md`): *«si yo registro información vieja de días atrás,
> debo tenerla máximo para confirmar mañana, porque hoy hice la creación — no de cuándo es, sino de
> acuerdo a la creación»*.
>
> **Base de medición:** copia de producción en `sanmarinoapplocal:5433`.

---

## 1. Lo que se validó de la propuesta, punto por punto

| Lo que planteaste | Qué encontré |
|---|---|
| «el plazo debería contarse desde la creación, no desde la fecha» | ✅ Hoy se cuenta desde la **fecha del seguimiento**: `FechaLimiteValidacion(fechaSeguimiento) = fecha + 1`. Tu lectura del problema es correcta. |
| «cuando termine los 7 días, en pollo engorde me va a aparecer confirmados, no vencidos» | ✅ **Ya quedó así** con el arreglo de hoy (`14daf32`): los días del cruce nacen validados. |
| «seguimiento diario no confirmado el día anterior me aparecería vencido» | ✅ Es el comportamiento actual y **no cambia** con tu propuesta: si capturás hoy el día de hoy, las dos reglas dan lo mismo. |
| «el vencido es una alerta, mostrar que hacen falta» | ✅ Compatible con lo que aclaraste después: el **bloqueo se queda**, pero pasa a colgar de «el anterior tiene que estar confirmado», y `EN_RETRASO` queda como señal visual. Ver §4.1 y §4.1-bis. |
| «habilitar la doble validación en Panamá» | ℹ️ **Ya está encendida** (`requiere_validacion_seguimiento_diario = true`). Es la única empresa. |
| «y también habilitarla en Ecuador» | ⚠️ Se puede, pero **el orden importa**. Ver §5. |

---

## 2. El dato que decide

Cómo se captura realmente, **últimos 30 días, excluyendo el cruce**:

| Empresa | Registros | Al día (0-1) | Retroactivos (2+) | % | Peor atraso |
|---|---:|---:|---:|---:|---:|
| **ItalcolEcuador** | 671 | 577 | 94 | **14 %** | 6 días |
| **ItalcolPanama** | 1.030 | 139 | 891 | **86,5 %** | 43 días |

De los 891 de Panamá, **465 son una carga masiva** (un solo autor, 2 días de carga). Los otros
**426 los cargaron personas reales**, con **2 a 21 días de atraso**, repartidos en 6 a 13 días de
carga distintos. **No es una anomalía: es cómo opera Panamá.**

> La regla actual funciona hoy en Panamá solo porque el operario valida **en la misma sesión** en que
> carga. Si carga y deja pendiente, el lote se traba en el acto. Esa es exactamente la fricción que
> describís.

---

## 3. Tu propuesta ataca la raíz de tres parches que ya existen

Los tres son el mismo caso —**un registro creado hoy con fecha vieja**— y cada uno se resolvió aparte:

1. **`ModoCargaHistorica`** (carga masiva y puente Panamá). Su propio doc dice por qué existe:
   *«la primera fila insertada queda vencida en el acto (el plazo es de un día) y
   `AsegurarPuedeRegistrarDiaAsync` rechazaba la segunda. Un lote de 40 días entraba con una sola
   fila»*.
2. **El cruce de reproductora** — arreglado hoy haciendo que nazca validado.
3. **El push offline de la PWA** (`SyncPushService`) — **sigue abierto**: un día capturado sin señal y
   sincronizado 24 h después nace vencido.

Contar desde `created_at` los resuelve **de raíz** y vuelve innecesarios (1) y (3). Es la
generalización correcta de dos parches puntuales.

---

## 4. Lo que hay que decidir antes de tocar código

### 4.1 ✅ RESUELTO por el usuario: el bloqueo se queda, y se vuelve MÁS estricto

Decisión textual (25-ago-2026): *«no quiero quitar la validación de que el día vencido no me va a
dejar crear otro… si hay un registro anterior vencido, lo tienen que confirmar para poder habilitar
el día siguiente. **El día anterior debe estar confirmado para poder continuar al día siguiente**…
así validamos que todo quede cuadrado día a día, y que tenga una confirmación extra»*.

🔴 **Ojo: eso NO es el comportamiento actual — es más estricto.** Hoy el bloqueo se dispara con
*«hay algún registro VENCIDO»*, y un registro de ayer sin validar todavía está **PENDIENTE** (dentro
del plazo de 1 día), así que **hoy no bloquea**. Medido, en este momento:

| Lote | Fecha | Creado | Hoy | Con la regla nueva |
|---|---|---|---|---|
| 177 (Panamá) | 20-ago | 21-ago | **VENCIDO — bloquea** | bloquea |
| 180 (Panamá) | 24-ago | 25-ago | PENDIENTE — **no bloquea** | **bloquea** |

O sea: la regla nueva **elimina la gracia de un día**. Es coherente con lo que se busca («cuadrado
día a día»), pero es un cambio de comportamiento, no una confirmación del status quo.

### 4.1-bis 🔴 La consecuencia que simplifica todo: el plazo deja de gobernar el bloqueo

Si el bloqueo pasa a ser **«el registro anterior tiene que estar confirmado»**, entonces **ya no
depende del plazo**. Y eso reconcilia las dos cosas que dijiste:

- *«el vencido es una alerta, mostrar que hacen falta»* ⇒ `EN_RETRASO` queda como **señal visual**.
- *«el día anterior debe estar confirmado»* ⇒ **eso** es lo que bloquea.

**Consecuencia práctica: el cambio de `fecha` → `created_at` pasa a ser cosmético.** Si el bloqueo no
mira el plazo, mover el origen del plazo solo cambia **de qué color se pinta la fila**, no quién puede
registrar. Los tres parches de §3 (`ModoCargaHistorica`, el cruce, el push offline) dejarían de
hacer falta **por el cambio de bloqueo**, no por el del plazo.

> Esto reordena la prioridad de todo este plan: **el cambio que resuelve el problema es el del
> bloqueo, no el del plazo.** El del plazo pasa a ser una mejora de la señal visual, opcional y
> separable.

### 4.2 ¿Qué se pierde al cambiar de fecha a creación?

Hoy «vencido» significa dos cosas a la vez: **no validaste a tiempo** *y* **no cargaste a tiempo**.
Con `created_at` queda solo la primera. Cargar 20 días tarde deja de generar señal mientras se valide
al día siguiente.

Si esa señal importa —y en Panamá, con 2 a 21 días de atraso habituales, probablemente sí— hay que
reponerla **por separado**: un indicador de «días sin capturar» o de antigüedad de la captura, que es
lo que en realidad se quiere vigilar. No debería seguir viajando escondida dentro de «vencido».

### 4.3 El detalle fino: `created_at` se reinicia al borrar y recrear

Borrar un pendiente y volver a crearlo da un `created_at` nuevo, o sea plazo nuevo. Para un registro
normal es inocuo (borrar y recrear ya es el camino de corrección). **Pero el cruce hace
`DELETE`+`INSERT` en cada regeneración**, así que ahí el reloj se reiniciaría solo. Hoy no importa
porque nacen validados — conviene que siga siendo así y **no** apoyarse en el plazo para el cruce.

---

## 5. 🔴 El orden importa: la regla primero, Ecuador después

Ecuador hoy tiene **5.482 registros, 0 sin validar, 0 vencidos** ⇒ encender el flag **no bloquea nada
retroactivamente**. Buena noticia.

**Pero con la regla actual, el 14 % de su captura nace vencida**: 94 registros de los últimos 30 días
se cargaron con 2 o más días de atraso (73 con 2 días, 21 con 3-6). Encender el flag en Ecuador
**antes** de cambiar la regla reproduce exactamente el problema que acabamos de cerrar en Panamá, con
94 casos por mes.

**Secuencia recomendada (reordenada tras la aclaración del usuario — ver §4.1-bis):**
1. **Cambiar el BLOQUEO** a «el registro anterior tiene que estar confirmado». Es lo que resuelve el
   problema; el plazo deja de gobernar quién puede registrar.
2. **Dar el apoyo de UI que la regla exige** (§8): «guardar y validar» o «validar todos los
   pendientes del lote». Sin eso Panamá la sufre — 41 sesiones de 6+ días en un mes.
3. Verificar en Panamá una semana (ya tiene el flag encendido y es el caso extremo).
4. Recién entonces **encender el flag en Ecuador**, por migración.
5. **Opcional y separable**: mover el origen del plazo a `created_at` para que la señal visual
   `EN_RETRASO` deje de mentir en las cargas retroactivas.

---

## 6. Alcance técnico estimado (para la sesión que lo tome)

| Qué | Dónde |
|---|---|
| La regla | `ValidacionSeguimientoCalculos.FechaLimiteValidacion` / `Estado` / `EstaEnRetraso` — hoy reciben `fechaSeguimiento`; pasarían a recibir también la fecha de creación |
| Los llamadores | `ValidacionSeguimientoService` (`LeerEstadoAsync`, `LeerPendientesDelLoteAsync`, `AsegurarPuedeRegistrarDiaAsync`) |
| El front | `estado-validacion-seguimiento.funcion.ts` calcula el estado **en el cliente** — hay que pasarle `createdAt`, que hoy puede no viajar en el DTO |
| Los 5 services de seguimiento | Solo si el DTO cambia de forma |
| `ModoCargaHistorica` | Queda redundante. **No borrarlo en el mismo cambio** — primero medir |
| Migración | Solo para encender el flag en Ecuador (paso 3), no para la regla |

**Tests obligatorios** (gate de CI): equivalencia para la captura del mismo día (las dos reglas dan
igual), y los casos retroactivos que hoy nacen vencidos y dejarían de hacerlo.

---

## 7. Lo que NO hay que perder de vista

- **El caso del cruce ya está cerrado** y no depende de este cambio. Si esto se hace, revisar que
  siga siendo correcto que nazcan validados (lo es: no hay nada que validar ahí).
- **`fecha` sigue siendo la verdad para el negocio** — el consumo, el saldo y los reportes se calculan
  por fecha del seguimiento. Este cambio toca **solo el plazo administrativo de validación**, nada
  del cálculo.
- Medir antes y después con `backend/sql/verificar_cruce_nace_validado.sql` (chequeo 3: qué lotes
  quedan bloqueados y por qué).

---

## 8. Lo que cuesta la regla estricta, medido

Con «el anterior debe estar confirmado», cargar N días seguidos exige **N ciclos de
cargar → validar**, alternados. Y hoy **no existe validación en lote**: el endpoint es
`POST /{modulo}/{id}/validar`, de a uno, y el front valida fila por fila (`puedeValidarFila`).

Cuántos días se cargan por sesión (mismo lote, mismo día de carga), últimos 30 días, sin el cruce:

| Empresa | Sesiones de 1 día | De 2 a 5 días | **De más de 5** | Peor sesión |
|---|---:|---:|---:|---:|
| **ItalcolEcuador** | 326 | 156 | **0** | 5 días |
| **ItalcolPanama** | 78 | 97 | **41** | **34 días** |

**Ecuador aguanta la regla estricta tal cual**: nunca carga más de 5 días juntos.

**Panamá no, sin ayuda de la UI**: 41 veces en un mes cargó 6 o más días de un lote en la misma
sesión, con un pico de **34 días**. Bajo la regla estricta esa sesión son 34 cargas y 34
validaciones intercaladas, a un clic cada una.

**Lo que hace falta junto con la regla** (una de las dos, no las dos):
- un **«guardar y validar»** en la misma acción, o
- un **«validar todos los pendientes del lote»** (endpoint de lote + botón).

Sin eso, la regla es correcta y la operación la va a sentir como un castigo.

---

## 9. ✅ RESUELTO por el usuario: un día FALTANTE también bloquea

Decisión textual: *«todos los días se tienen que llenar hasta que se liquide el lote (…) debe mostrar
cuáles no hay registro (…) y también hace esa validación, de tener registros para esos días que
tienen esos huecos. Sí o sí, antes de dejarlo seguir con el proceso.»*

Queda elegida la lectura **(B)**: el hueco bloquea. Lo que sigue es lo que hay que resolver para que
esa regla se pueda cumplir sin dejar a nadie encerrado.

### 9.1 🔴 Tal como está escrita hoy, la guarda se muerde la cola

`AsegurarPuedeRegistrarDiaAsync(modulo, loteId)` **no recibe la fecha** del registro que se está
creando, y corre en la **primera línea** del create (`SeguimientoAvesEngordeService.Crud.cs:96`),
antes incluso de validar la fecha contra el encasetamiento.

Si a esa guarda se le agrega «los huecos bloquean», entonces bloquea **también el POST que vendría a
llenar el hueco**. El lote queda encerrado y no hay pantalla que lo destrabe: es **exactamente** el
callejón sin salida del cruce que se arregló en `14daf32`, reintroducido por otra puerta.

**El fix es de diseño, no de detalle:** la guarda tiene que recibir la fecha y **eximir el día que se
está llenando** cuando ese día es uno de los faltantes. Son 5 call sites
(`ProduccionService.Seguimiento.cs:236`, `SeguimientoAvesEngordeService.Crud.cs:96`,
`SeguimientoAvesEngordeEcuadorService.Crud.cs:40`, `SeguimientoDiarioLoteReproductoraService.cs:270`,
`SeguimientoLoteLevanteService.Crud.cs:35`). **Ningún test actual cubre esto**, porque hoy la fecha no
participa de la decisión.

### 9.2 ✅ El camino para llenar el hueco existe y está limpio

Era la pregunta directa del usuario («que el campo fecha en el modal me deje agregar el día
específico que hace falta»). Verificado punta a punta:

| Qué | Estado |
|---|---|
| Campo fecha del modal (`modal-seguimiento-engorde.component.html:36`) | ✅ `<input type="date">` **sin `min` ni `max`** — acepta cualquier día |
| Duplicar un día por accidente | ✅ Imposible: índice único `uq_seg_diario_aves_engorde_lote_fecha (lote, fecha)` |
| Ventana de fecha retroactiva (el permiso de Lady) | ✅ **No aplica**: `ValidarVentanaFechaRegistro` sólo está en los controllers de inventario, gastos, movimientos y traslados. Los seguimientos **nunca** estuvieron limitados por fecha |
| Única cota de fecha en el create | ✅ No anterior al primer día del lote (encasetamiento + hora), `Crud.cs:118`. Correcta, no estorba |
| Aritmética al insertar un día **en el medio** | ✅ `RecalcularPorLoteAsync` reescribe `saldo_alimento_kg` de **todos** los días del lote desde la fn — los días posteriores se corrigen solos |
| La guarda de vencidos | 🔴 Ver §9.1 — es lo único que hay que tocar |

### 9.3 🔴 El número que di antes era el chico: la cola cambia la escala

Los **40 huecos / 37 lotes** que se reportaron eran sólo los **interiores** (entre el primer y el
último registro). Con la definición del usuario —*todos los días hasta que se liquide*— hay que contar
también la **cola**: desde el último registro hasta ayer. Medido el 25-ago-2026 sobre la copia de
producción, lotes abiertos (`liquidado_at IS NULL`):

| Empresa | Días faltantes | Lotes | **Interiores** | **Cola** | Hueco más viejo |
|---|---:|---:|---:|---:|---:|
| **ItalcolPanama** | **565** | 44 | 41 | **524** | 72 días |
| **ItalcolEcuador** | **133** | 5 | 4 | **129** | 130 días |

**El 93 % del problema es cola, no hueco interior.** Y la cola es otra cosa:

| Panamá — tramo | Lotes | Días | Edad del lote |
|---|---:|---:|---:|
| al día | 11 | 0 | 18–33 |
| cola 1–3 días | 5 | 8 | 8–40 |
| cola 4–7 días | 10 | 52 | 12–47 |
| **cola > 7 días** | **18** | **464** | **15–78** |

### 9.4 🔴 La cola de Panamá son lotes TERMINADOS que nadie cerró

Los 18 lotes de cola larga tienen entre 50 y 78 días de edad —un engorde se saca a ~42— y **cero
salidas registradas**, con unas 650.000 aves todavía en papel. La causa está a la vista:

> **Panamá tiene 3 ventas registradas en todo el sistema. Ecuador tiene 1.452.**

Panamá no registra la venta ni liquida el lote: **la cola *es* su final de lote normal**. El caso de
Ecuador es el mismo cuadro al revés — el lote 2601 lleva 125 días de cola, 191 de edad, y ya tiene
50.896 aves vendidas contra 25.400 encasetadas: terminado hace meses.

⚠️ **Consecuencia:** aplicar la regla literal a la cola le pide al operario que **invente ~460
registros diarios** de lotes cuyas aves ya no están en la granja. Eso no cuadra nada — ensucia el
histórico con datos falsos y no es lo que el usuario quiere lograr.

### ✅ DECIDIDO por el usuario (25-ago-2026): **«el hueco interior bloquea, la cola se liquida»**

Queda cerrada la última ambigüedad del planteo. Las dos causas se tratan distinto:

| Caso | Qué hace el sistema | Costo medido |
|---|---|---|
| **Hueco interior** | **Bloquea** el alta de días nuevos hasta que se registre | 41 días en Panamá (33 lotes con **un solo día**), 4 en Ecuador. Sin patrón de fin de semana (jue 14, dom 10, mié 9) ⇒ olvidos legítimos y llenables |
| **Cola** | **No bloquea capturando: se liquida el lote** | 18 lotes en Panamá (>7 días de cola), 1 en Ecuador. Ninguno se llena inventando registros |

🔴 **La decisión abre una obligación nueva:** si la salida que le ofrecemos al operario es
*liquidar*, esa salida **tiene que existir y funcionar hoy**. Un lote de cola de Panamá tiene ~45.000
aves en papel y cero salidas registradas — si el liquidador exige aves en cero, o exige una venta, o
exige merma no nula, le estaríamos ofreciendo una puerta cerrada, que es el mismo error que §9.1.
**Verificado en §9.8 antes de implementar nada.**

### 9.5 Los días concretos que faltan (interiores, para el aviso)

Ecuador — Kilometro 86, lote 12 «2601»: 2026-04-17 al 20 (4 días).

Panamá, 37 lotes / 41 días. Los de más de un día:

| Granja | Lote | Días faltantes |
|---|---|---|
| MENDOZA | 160 «17 - 2» | 2026-06-27, 2026-06-28 |
| DOÑA MARIA | 165 «94 - 2» | 2026-07-08, 2026-07-28 |
| DOÑA MARIA | 169 «60 - 4» | 2026-07-12, 2026-07-31 |
| DOÑA MARIA | 171 «60 - 2» | 2026-07-16, 2026-08-10 |

Los 33 restantes tienen **exactamente un día** cada uno. La consulta que los lista está en
`backend/sql/verificar_huecos_dias_seguimiento_engorde.sql`.

### 9.6 El aviso que pidió el usuario

*«darle un mensaje, alguna novedad, decirle: ese lote le hace falta tales días»*. El molde ya existe:
`ValidacionSeguimientoCalculos.MensajeBloqueoPorVencidos(fechas)` nombra las fechas concretas en vez
de un «tiene pendientes» genérico. El mensaje de huecos debe seguir el mismo criterio y **distinguir
las dos causas**, porque se arreglan distinto:

- «Faltan los días 2026-07-08 y 2026-07-28 — registralos para continuar.» (hueco interior)
- «Este lote no tiene registros desde 2026-06-20 (65 días). Si ya salió, liquidalo.» (cola)

### 9.7 Descartado: la cola **no** la causa el bloqueo

Hipótesis razonable que había que descartar antes de culpar al operario: *un lote bloqueado no puede
registrar, así que el bloqueo se fabrica su propia cola y después la castiga*. Medido sobre la copia
de producción **con el arreglo del cruce (`14daf32`) ya aplicado**:

| Estado del lote | Lotes | Días de cola |
|---|---:|---:|
| BLOQUEADO hoy por vencidos | 1 | 4 |
| no bloqueado | 43 | **520** |

**520 de los 524 días de cola están en lotes que hoy nadie bloquea.** La cola es abandono operativo,
no un efecto del bloqueo — lo que refuerza la lectura de §9.4: son lotes terminados sin cerrar.

De paso queda verificado el arreglo del cruce: **0 vencidos `origen_cruce`** y los 4 lotes de DAYLAND
(215, 216, 224, 225) ya no están trabados. El lote **215** conserva 9 días de cola que sí son reales y
ahora sí se pueden capturar.

### 9.9 Lo que la decisión cambia en el costo del deploy

Con **«la cola no bloquea»**, el número del día del deploy baja mucho: la cola era el 93 % y sale de
la ecuación. Queda sólo el hueco interior, y **sólo en Panamá**, porque Ecuador tiene el flag
apagado:

| | Antes (regla literal) | **Con la decisión tomada** |
|---|---:|---:|
| Lotes bloqueados el día del deploy | 44 Panamá + 5 Ecuador | **37 Panamá, 0 Ecuador** |
| Días que hay que capturar para destrabar | 565 + 133 | **41**, y de esos sólo **23** son de lotes vivos |

Y todavía baja más. **De los 37 lotes con hueco interior, 15 son ADEMÁS cola** (edad 50–78 días): se
van a liquidar igual, así que sus 18 días son irrelevantes. Lo mismo el único lote de Ecuador (el
2601, 191 días de edad). Lo que queda de verdad:

> **22 lotes vivos de Panamá (edad 16–47 días) y 23 días a capturar.**

Eso no es una tarde: es un rato. **Pero el orden importa** — hay que liquidar primero los 19 lotes de
cola; si no, arrancan bloqueados por un hueco que nadie va a llenar porque el lote ya terminó.

La cola pasa a ser **limpieza operativa** (liquidar 19 lotes terminados: 18 de Panamá + el 2601 de
Ecuador), no un bloqueo.

### 9.10 🔴 La interacción entre las dos reglas: el día que se llena nace vencido

Es la trampa de segundo orden, y hay que resolverla en el mismo cambio.

El operario llena hoy (25-ago) el hueco del 08-jul. Con el plazo contado desde la **fecha del
registro**, ese registro nace **inmediatamente vencido** (`FechaLimiteValidacion` = 09-jul). Y un
vencido sin confirmar **bloquea el alta de días nuevos**. O sea: *llenar el hueco vuelve a trabar el
lote*, salvo que se confirme en el acto.

Dos consecuencias concretas para quien lo implemente:

1. **La exención de §9.1 tiene que ser «el día que se crea es UN día faltante», no «es EL único
   faltante».** Si no, el lote 12 de Ecuador —que tiene 4 huecos seguidos (17 al 20 de abril)— se
   traba al llenar el primero: el 17 nace vencido y bloquea la creación del 18. Con la exención bien
   escrita, los cuatro se pueden cargar de corrido.
2. **Hace falta «guardar y validar» en el mismo paso, o «validar todos los pendientes del lote».**
   Hoy el endpoint es `POST /{modulo}/{id}/validar`, de a uno, y el front valida fila por fila. Ya
   estaba anotado en EC4 como requisito de UI para Panamá (que llegó a cargar 34 días en una sesión);
   la regla de huecos lo vuelve **obligatorio**, no deseable.

> Esto refuerza lo de §4.1-bis: **el cambio que resuelve el problema es el del bloqueo, no el del
> plazo.** Pero mientras el plazo se cuente desde la fecha del registro, llenar un hueco exige
> confirmar en el acto — y por eso los dos cambios conviene que viajen juntos.

---

---

## 10. Lo que queda validado de tu planteo

| Lo que dijiste | Veredicto |
|---|---|
| «el bloqueo tiene que quedarse» | ✅ Se queda. Y hay que hacerlo **más estricto** que hoy para que signifique lo que querés. |
| «Panamá depende de tener las reproductoras al día» | ✅ Correcto — y con el arreglo de `14daf32` los 7 días del cruce nacen confirmados, así que dejan de trabar. |
| «Ecuador no tiene reproductoras, su flujo es normal desde el día 1» | ✅ Confirmado: **0 registros `origen_cruce` en Ecuador**; los 5.482 son captura normal. |
| «el día anterior debe estar confirmado para continuar» | ✅ Es implementable, y es **más simple** que lo que hay. Pero pide apoyo de UI en Panamá (§8). |
| «que tenga una confirmación extra» | ✅ Es exactamente lo que da la doble validación; el flag ya existe por empresa. |
| «todos los días deben tener registro hasta liquidar» | ⚠️ Correcto como principio, pero el número real es **565 días en Panamá**, no 40, y **93 % es cola de lotes terminados sin cerrar**. Ver §9.3–9.4: el hueco interior se llena, la cola se liquida. |
| «que muestre cuáles días no hay registro» | ✅ Implementable y ya medido — la lista concreta está en §9.5 y la reproduce `verificar_huecos_dias_seguimiento_engorde.sql`. |
| «que el campo fecha me deje agregar el día que hace falta» | ✅ **Verificado punta a punta** (§9.2): el input no tiene `min`/`max`, el índice único impide duplicar, la ventana retroactiva no aplica a seguimientos y al insertar un día del medio se recalcula el lote entero. |
| — | 🔴 **Lo único que hay que arreglar antes:** la guarda no recibe la fecha y bloquearía el POST que llena el hueco (§9.1). Es el mismo callejón sin salida del cruce, por otra puerta. |
