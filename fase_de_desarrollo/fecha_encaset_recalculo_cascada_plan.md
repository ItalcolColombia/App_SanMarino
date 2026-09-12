# Editar la fecha/hora de encasetamiento y que recalcule en cascada — plan

> Ticket Panamá (11-sep-2026), granja **DOÑA MARIA**, lote engorde **255 «95 - 3» ERP G-4001095**.
> «no se le colocó la hora de ingresar la información en lote… la fecha que debe ingresar el primer
> registro debe ser el día 4 de septiembre de 2026 … corrige para que no tenga que pasar a desarrollo
> editar la fecha y que eso recalcule en los lotes reproductora si tiene y en seguimiento pollo
> engorde … la idea es que el usuario que tenga el permiso pueda modificar».

---

## 1 · Diagnóstico — medido contra la copia local de producción

El ticket llega como «hay que poder editar la fecha», pero el número que el usuario ve mal **no sale
de una fecha mal cargada**: sale de que el cruce **desplaza la serie dos veces**.

| Dato | Valor |
|---|---|
| Lote engorde 255 | encaset **2026-09-03**, hora **21:35** (informada el 10-sep), galpón G0475 |
| Reproductora 160 (hija) | encaset 2026-09-03; registros **04/09 → 09/09** = edades **1..6** |
| Cruce → `seguimiento_diario_aves_engorde` | filas **05/09 → 10/09**, `metadata.desplazamientoHora = 1` |
| Mortalidades de esas filas | 12 · 25 · 20 · 25 — idénticas a la captura del ticket |

`fn_cruce_reproductora_a_engorde` fecha el destino como `fecha_encaset + v_desp + d`. El
desplazamiento por llegada tardía (`hora >= 13:00 ⇒ v_desp = 1`, ago-2026) se escribió para el caso
en que **la reproductora sí capturó el día del encaset (edad 0)**: ahí correr la serie es correcto,
porque las aves llegaron 21:35 y ese consumo pertenece al día siguiente.

Cuando la reproductora **ya arrancó en la edad 1** —porque es la misma llegada tardía y el operario
hizo lo correcto— el `+1` se aplica encima de un día que ya estaba corrido: el registro del 04/09
aterriza en el 05/09.

**Prueba independiente de que es un defecto y no una convención:** `EncasetamientoCalculos.PrimerDiaConRegistro`
(C#, el guarda que valida la captura manual) dice que el primer día válido del lote 255 es el
**04/09**, y el cruce escribe el **05/09**. Backend y BD se contradicen hoy.

### Radio medido (todas las empresas, todos los lotes vivos)

| empresa | `v_desp` | primera edad con registro | lotes | ids |
|---|---:|---:|---:|---|
| ItalcolPanama | 0 | 0 | 32 | (sin cambio) |
| ItalcolPanama | 0 | 1 | 2 | 242, 254 (sin cambio) |
| ItalcolPanama | 0 | 2 | 1 | 232 (sin cambio) |
| ItalcolPanama | 1 | 0 | 2 | 215, 216 (sin cambio — la reproductora sí capturó la edad 0) |
| **ItalcolPanama** | **1** | **1** | **4** | **239, 255, 256, 257 ← los únicos que se mueven** |

ItalcolEcuador / Demo / Sanmarino: **0 filas** `origen_cruce` en toda la BD ⇒ impacto cero.

---

## 2 · La regla nueva (una sola línea de aritmética)

```
desplazamiento_efectivo = GREATEST(0, v_desp - primera_edad_generada)
```

* `primera_edad_generada` = la menor edad `d ∈ [0,7]` en la que **todos** los lotes reproductora
  tienen registro confirmado (la primera que el cruce realmente escribe).
* Con `v_desp = 1` y primera edad 0 → 1: **idéntico a hoy** (215, 216).
* Con `v_desp = 1` y primera edad ≥ 1 → 0: la fila aterriza el día en que se capturó (239, 255-257).
* Con `v_desp = 0` → 0 siempre; el `GREATEST` es el que impide que un lote sin hora se corra **hacia
  atrás** sobre el día del encaset (242, 254, 232).

Es la misma forma que ya usa el front para numerar (`desplazamientoNumeracion`: el corrimiento lo
manda la menor edad CON registro, con tope de 1 día). La fn es la **dueña** del número; el espejo
puro en C# (`EncasetamientoCalculos.DesplazamientoCruce`) es su **especificación con tests**.

---

## 3 · Remediación de datos — decidida con el usuario

| Lote | Situación | Qué se hace |
|---|---|---|
| **255** (ticket) | cruce 05→10/09, sin captura manual | recalcular ⇒ cruce **04→09/09**, primer registro el **4 de septiembre** |
| **256**, **257** | cruce 08→10/09 (3 días), sin captura manual | recalcular ⇒ 07→09/09 |
| **239** | cruce 29/08→04/09 **+ 5 filas manuales 05→09/09** | **corregir todo**: cruce a 28/08→03/09 y las 5 manuales un día atrás (04→08/09). Serie contigua, sin hueco, alineada con la realidad |

Las filas manuales del 239 se mueven **fila por fila en orden ASC** dentro de la transacción (se mueven
hacia atrás: cada día destino queda libre antes de escribirlo). La tabla solo tiene un trigger
`AFTER DELETE` (tombstone PWA) ⇒ un `UPDATE fecha` no dispara nada; las FK apuntan al `id`, que no cambia.

---

## 4 · La funcionalidad pedida — editar la fecha recalcula en cascada

Hoy el formulario **ya deja editar** `fechaEncaset` y `horaEncasetamiento` (front y `PUT`), pero
**nada recalcula**: el cruce solo se re-corre con el trigger de la tabla de seguimiento reproductora.
Por eso cada caso termina en desarrollo.

### 4.1 Permiso nuevo — `lote.corregir_fecha_encaset`

* Key propia (decisión del usuario), sembrada por migración data-only idempotente.
* **Anti-lockout:** se hereda de `lote.corregir_aves` (+ rol 1), que es su padre natural: quien hoy
  corrige el encasetamiento de un lote es quien corrige su fecha. Nadie queda trabado el día del deploy.
* `company_permissions` en todas las empresas (fail-closed: sin esa fila no viaja en el JWT).
* **Enforcement en el service, por DELTA y no por verbo:** solo se exige cuando el `PUT` cambia
  `fecha_encaset`/`hora_encasetamiento` **y** el lote ya tiene registros (propios o de reproductora).
  Crear un lote o corregirle el técnico no pide nada — el mismo criterio que `lote.corregir_aves`.

### 4.2 La cascada (misma transacción)

Al cambiar fecha u hora del lote pollo engorde:

1. **Propagar a TODOS los lotes reproductora hijos** (decisión del usuario) fecha + hora.
2. **Pre-validar antes de escribir**: con la fecha nueva, cada registro de cada reproductora tiene que
   seguir cayendo en su ventana (`ReproductoraEngordeCalculos.EsEdadSeguimientoValida`). Si alguno
   queda fuera → **400 con el detalle** (lote, fecha, edad) y no se escribe nada.
3. `fn_cruce_reproductora_a_engorde(loteId)` — re-fecha el cruce con la regla de §2.
4. `RetiroAvesEngordeAplicador.SincronizarCruceAsync` — las bajas del cruce al maestro de aves.
5. `SaldoAlimentoEngordeAplicador.RecalcularPorLoteAsync` — el saldo de alimento de toda la serie.

Al editar el lote **reproductora** (su propia fecha/hora): mismos pasos 3-5 sobre su lote engorde padre.

### 4.3 Front

* `lote-engorde-list`: los inputs de fecha/hora quedan `readonly` al **editar** sin el permiso, con el
  mismo aviso 🔒 que ya usa `lote.corregir_aves`, y una nota que dice qué recalcula el cambio.
* Mismo criterio en el formulario del lote reproductora.

---

## 5 · Casos de prueba

**Puros (xUnit):**

| hora | primera edad | esperado |
|---|---:|---:|
| null | 0 / 1 / 2 | 0 |
| 12:59 | 0 | 0 |
| 13:00 | 0 | 1 |
| 21:35 | 1 | **0** ← el caso del ticket |
| 21:35 | 2 | 0 |
| 13:00 | null (sin registros) | 1 |

**SQL / datos (transacción revertida sobre la copia de producción):**

* T1 — lote 255: el cruce pasa a 04→09/09; la primera fila queda el **4 de septiembre**.
* T2 — lotes 215/216: **0 filas cambiadas** (la reproductora capturó la edad 0).
* T3 — lotes 242, 254, 232 y los 32 sin hora: **0 filas cambiadas**.
* T4 — **GATE MULTIPAÍS** (CLAUDE.md): `EXCEPT` en los dos sentidos, todas las columnas, todos los
  lotes de TODAS las empresas, antes y después. Ecuador/Demo/Sanmarino tienen que dar **0**.
* T5 — cuadre: `backend/sql/verificar_cuadre_alimento_engorde.sql` antes/después; ningún galpón nuevo
  descuadrado.
* T6 — 239 remediado: serie contigua 28/08→08/09, sin hueco, sin violar el índice único.
* T7 — `Down()` devuelve la fn v-anterior byte a byte.

**API (smoke local):** `PUT` de fecha sin el permiso → **403**; con el permiso → 200 y el cruce
re-fechado; `PUT` que dejaría un registro de reproductora fuera de ventana → **400** con el detalle.

---

## 6 · Addendum 12-sep-2026 — medido contra la copia de produccion del dia

Segundo ticket del mismo defecto: **lote 239 «95 - 1»** (DONA MARIA A-1, encaset 27-ago 21:33). La
reproductora 146/147 capturo desde el **28-ago** (edad 1) y la tabla de pollo engorde arrancaba el
**29-ago** en «Edad 2». Es exactamente la fila del 239 de §3.

* **Radio: 5 lotes, no 4.** El **259** (encaset 09-sep 23:09, reproductora desde el 10-sep) nacio
  despues del diagnostico con la misma topologia. La remediacion selecciona por dato, asi que lo toma
  sola. El 239 tiene ahora **6** filas manuales (05→10-sep), tambien por dato.
* **Hueco que la migracion no cubria:** el cruce reinserta sus filas con ids nuevos y las
  `BAJA_SEGUIMIENTO` vivas del historico quedaban apuntando a seguimientos inexistentes. En la app lo
  repara `SincronizarCruceAsync` despues del trigger; en el 239 y el 255 la reproductora ya cerro la
  semana y nada lo volveria a disparar. Se replico en SQL (patron `20260828200000`), y la fila de bajas
  de cada registro manual corrido sigue a su registro (como `UpsertHistorico` al editar la fecha).
* **Medido (transaccion revertida, 2 pasadas):** huerfanas 6 → 0; maestro de aves y
  `fn_cuadre_aves_engorde` sin cambios; `inventario_gestion_movimiento` intacto; cuadre de alimento
  8 descuadrados / 15 con dias en rojo, igual antes y despues; `fn_seguimiento_diario_engorde` de todas
  las empresas: solo cambian los 5 lotes; segunda pasada 0 cambios.
* **Aviso para operacion:** el 257 (ciclo anterior de G0490) pasa a mostrar −227 kg el 07-sep: la
  reproductora consumio ese dia y el ingreso de alimento esta cargado el 08-sep. Es dato, no calculo.
