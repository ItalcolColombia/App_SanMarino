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
| «el vencido es una alerta, mostrar que hacen falta» | ⚠️ **Hoy NO es una alerta: bloquea.** Ver §4 — es una decisión que ya tomaste una vez, en sentido contrario. |
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

### 4.1 🔴 ¿«Vencido» sigue bloqueando, o pasa a ser solo alerta?

Dijiste *«el vencido es una alerta, mostrar que hacen falta»*. Hoy **bloquea** el alta de días
nuevos, y eso fue una **decisión explícita tuya del 14-ago-2026**: *«los registros vencidos bloquean
el alta de días nuevos, no solo avisan»*.

Son dos cambios distintos y conviene no mezclarlos:
- **Cambiar el origen del plazo** (fecha → creación): arregla el caso legítimo sin aflojar el control.
- **Cambiar bloqueo → alerta**: afloja el control. Si se hacen los dos juntos y algo sale mal, no se
  sabe cuál fue.

**Recomendación: hacer solo el primero**, medir una semana, y decidir el segundo con ese dato.

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

**Secuencia recomendada:**
1. Cambiar el origen del plazo a `created_at`.
2. Verificar en Panamá una semana (ya tiene el flag encendido y es el caso extremo).
3. Recién entonces encender el flag en Ecuador, por migración.

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
