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

## 9. La ambigüedad que falta cerrar: ¿un día FALTANTE también bloquea?

«Que todo quede cuadrado día a día» admite dos lecturas:

- **(A) Solo los registros que existen y no están confirmados bloquean.** Es lo que hace hoy el
  código: `LeerPendientesDelLoteAsync` lee registros existentes sin validar. Un día que **nunca se
  capturó** no bloquea nada.
- **(B) Un hueco en la serie también bloquea** — «cuadrado día a día» de verdad.

🔴 **El número decide, y es grande.** Huecos en los lotes **abiertos**:

| Empresa | Huecos | Lotes con hueco | Peor hueco |
|---|---:|---:|---:|
| **ItalcolPanama** | 40 | **37** | 2 días |
| **ItalcolEcuador** | 1 | 1 | 4 días |

Con la lectura **(B)**, **37 lotes abiertos de Panamá quedan bloqueados el día del deploy** y no hay
forma de destrabarlos salvo capturando esos días faltantes. Con **(A)**, ninguno.

**Recomendación: empezar por (A)**, que es lo que el usuario describió literalmente («el día anterior
debe estar confirmado» habla de un registro que existe), y tratar los huecos como un **reporte**
aparte. Pasar a (B) sería una segunda etapa, con los 37 lotes saneados antes.

---

## 10. Lo que queda validado de tu planteo

| Lo que dijiste | Veredicto |
|---|---|
| «el bloqueo tiene que quedarse» | ✅ Se queda. Y hay que hacerlo **más estricto** que hoy para que signifique lo que querés. |
| «Panamá depende de tener las reproductoras al día» | ✅ Correcto — y con el arreglo de `14daf32` los 7 días del cruce nacen confirmados, así que dejan de trabar. |
| «Ecuador no tiene reproductoras, su flujo es normal desde el día 1» | ✅ Confirmado: **0 registros `origen_cruce` en Ecuador**; los 5.482 son captura normal. |
| «el día anterior debe estar confirmado para continuar» | ✅ Es implementable, y es **más simple** que lo que hay. Pero pide apoyo de UI en Panamá (§8). |
| «que tenga una confirmación extra» | ✅ Es exactamente lo que da la doble validación; el flag ya existe por empresa. |
