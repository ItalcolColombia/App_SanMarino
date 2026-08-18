# V8 — Descuadres de alimento de ItalcolPanama: diagnóstico y qué hacer con cada patrón

**Origen:** bloque «V8 · Descuadres de alimento de ItalcolPanama — ABIERTO, para otra sesión (16ago26)»
del tracker (checklist V8.1-V8.6), reabierto por el señalamiento **V16.6.1**: la pantalla nueva del
cuadre (entregada el 17ago26) dejó esos descuadres **a la vista de un humano** por primera vez.
**Fecha:** 2026-08-17 · **Naturaleza:** diagnóstico **SOLO LECTURA** + una mejora de visibilidad.

> ⛔ **Ninguna corrección de datos entra en este plan.** V8 lo dice y la guía también: anular/borrar
> filas para «cuadrar» ya casi manda 5 ciclos cerrados a saldo negativo. Toda corrección se simula en
> transacción, se revierte, pasa el gate de paridad multipaís y **necesita OK explícito del usuario**.

---

## 1. Línea base re-medida (17ago26, BD local tipo prod)

| | ItalcolPanama (5) | ItalcolEcuador (3) |
|---|---|---|
| Galpones con ciclo activo | 30 | 36 |
| **Descuadrados** (`abs(descuadre) > 1 kg`) | **5** · 54.795,4 kg | **0** |
| Galpones con días en negativo | 19 | **0** |
| Ajustes manuales de stock (`AjusteStock` + `EliminacionStock`) | 73 · 341.624 kg | **400** · 1.989.212 kg |

**V8.5 queda contestado por los datos:** la MISMA función da 0 y 0 en Ecuador ⇒ **el problema es de los
datos de Panamá, no del cálculo**. Y el contraejemplo es más fuerte todavía: Ecuador hace **5 veces
más** ajustes manuales de stock y aun así cuadra — o sea que el ajuste por sí solo no descuadra.

La tabla de 6 filas de V8 **ya no es la de hoy**: son 5, y una cambió de lote (G0483 pasó del lote 187
al 190 al arrancar el ciclo siguiente, conservando el mismo descuadre de 23.300 kg — el descuadre viaja
con el **galpón**, no con el lote).

---

## 2. La causa raíz del patrón A: el stock se corrige a mano y la tabla diaria no se entera

`fn_seguimiento_diario_engorde` lee del histórico **sólo** `INV_INGRESO`, `INV_TRASLADO_ENTRADA` e
`INV_TRASLADO_SALIDA` — en sus 5 lugares (`apert_mov`, `hist_full`, `hist_alimento`, `docs_por_fecha`,
`fechas_universo`). Los movimientos `AjusteStock` y `EliminacionStock` se espejan como **`INV_OTRO`**,
que **ninguno de esos 5 lugares mira**. Resultado: cuando la operación arregla el inventario editando o
borrando el **stock**, la tabla diaria sigue contando los kilos originales — y el galpón queda
descuadrado para siempre.

**Evidencia, galpón por galpón:**

| galpón / lote | descuadre | qué lo explica |
|---|---|---|
| G0477 / 182 | **+544,0** | un `AjusteStock` de **544,0 kg** (29-jul). Exacto |
| G0475 / 165 | **+18.650,4** | un `EliminacionStock` de **18.650,356 kg** (07-ago). Exacto |
| G0483 / 190 | **+23.300,0** | **12.500** (ingreso duplicado el 01-ago; ese mismo día borraron el registro de stock, pero el `INV_INGRESO` quedó) **+ 10.800** (ajuste manual del ítem 213: 24.000 → 1.200 kg, de los cuales 12.000 nunca estuvieron en el histórico) = **23.300**. Exacto |

**42.494,4 kg de los 54.795,4 (78 %) son correcciones manuales de inventario**, no alimento perdido.
Se descarta la hipótesis (a) de V8 («alimento que entró y nunca se registró»): en los tres casos el
inventario está internamente consistente y lo que falta es que la tabla diaria vea la corrección.

**Por qué Ecuador no lo sufre:** sus ajustes son viejos respecto de los ciclos vigentes, así que quedan
absorbidos por la **apertura** (que toma el stock físico al arrancar el ciclo). Los de Panamá caen
**dentro** de la ventana del ciclo abierto. La regla, entonces: *un ajuste manual antes del ciclo se
absorbe; dentro del ciclo, descuadra*.

---

## 3. Los otros dos descuadres NO son ajustes

| galpón / lote | descuadre | qué se midió |
|---|---|---|
| G0476 / 202 | **+2.496,0** | Inventario consistente (18.112 − 15.632 = 2.480 = stock). **Dos lotes conviven** en el galpón (185 del 26-jul→12-ago y 202 del 29-jul→13-ago) y el inventario registró **43.251 kg** de consumo contra **32.708 kg** de seguimiento ⇒ hay consumo en inventario **sin seguimiento detrás** |
| G0481 / 199 | **−9.805,0** · 7 días negativos | Inventario consistente (11.800 − 6.441 = 5.359 = stock). El ciclo arranca su seguimiento el 05-ago con la tabla **ya en negativo** ⇒ es patrón B, no patrón A |

---

## 4. Patrón B (V8.4): no son cantidades, son fechas — y ya está datado

Lote **161** (G0472, 28 días en negativo, `descuadre = 0`):

- El primer ingreso tiene `fecha_operacion` **22-jun** por 11.779,9 kg; el siguiente es del **08-jul**.
- El consumo del seguimiento hasta el 07-jul suma **32.977,3 kg** ⇒ faltan ~21.200 kg de entrada en esa
  ventana. Los negativos empiezan el **29-jun** y se hunden hasta que llegan los ingresos de julio.
- 🔑 **Todos esos ingresos se registraron el mismo día, el 28-jul** (`created_at`), con la fecha de
  operación puesta hacia atrás: es una **carga histórica** de un mes entero. El total cuadra ⇒ los
  kilos están; lo que está mal es **cuándo** dice el sistema que entraron.

Lote **142** (G0471, 17 días): mismo perfil.

**Consecuencia:** re-fechar exige las **remisiones físicas**. No se puede inferir del sistema cuál de
los ingresos del lote va antes: cualquier reparto que uno invente cuadra igual de bien. Es una decisión
de operación con el documento en la mano, no un fix de software.

---

## 5. Patrón C (V8.1): era un artefacto del corte por fecha, y ya se disolvió solo

Lote **168** (G0490): V8 lo midió con `descuadre = mov_post = 250,0`. Hoy da
`saldo_tabla 10.609,560 · stock 10.609,560 · mov_post 0 · descuadre 0,000`. Al cargarse un seguimiento
posterior al movimiento, éste dejó de ser «posterior» y el descuadre desapareció **sin tocar un dato**.
⇒ Confirmado: **no era un error de datos**, era el corte por fecha del cuadre. Baja el conteo de 6 a 5.

---

## 6. Lo único que se implementa: que el cuadre DIGA lo que encontró

No se toca ninguna función SQL ni se corrige un solo dato. Se agrega a
`GET /api/CuadreAlimentoEngorde` —el endpoint que estrenó pantalla ayer— el contexto que convierte «5
galpones en rojo» en «3 de ellos son correcciones manuales de inventario»:

| archivo | qué |
|---|---|
| `Application/Calculos/CuadreAlimentoEngordeCalculos.cs` | + `DescribirConAjustes(...)`: si hubo ajustes manuales dentro del ciclo, el detalle lo dice y nombra los kilos |
| `Application/DTOs/CuadreAlimentoEngordeDto.cs` | + `AjustesManualesKg` y `AjustesManualesCount` por fila |
| `Infrastructure/Services/CuadreAlimentoEngordeService.cs` | + agregación de `AjusteStock`/`EliminacionStock` por ubicación **dentro de la ventana del ciclo activo** |
| front (componente del tab «Cuadre alimento») | + columna «Ajustes manuales» y el detalle nuevo |

**El `descuadre_kg` NO se mueve.** Un ajuste manual no es un error de medición como sí lo era la reserva
de la doble validación (V7.37): es una corrección real que la tabla diaria no vio, y taparla escondería
justo lo que hay que decidir. Se **informa**, no se compensa.

**Casos de prueba (xUnit, puros):** sin ajustes ⇒ el detalle es **byte a byte** el de hoy (T1-T3);
con ajustes que explican todo el descuadre (T4), parte (T5) y con descuadre 0 pero ajustes presentes
(T6, el caso de Ecuador: no ensuciar una fila que cuadra).

---

## 7. Verificación

1. `dotnet build` + `dotnet test` (con los tests nuevos y los 2.788 que ya existen).
2. `yarn build`.
3. Smoke del endpoint contra las dos empresas: Panamá tiene que mostrar los 3 galpones con sus ajustes
   y Ecuador quedar **idéntico** (0 descuadrados, sin texto nuevo en ninguna fila).
4. `git diff backend/sql` vacío ⇒ no aplica el gate multipaís.

---

## 8. Fuera de alcance, dicho

- **No se corrige ningún dato de Panamá.** Ni los 42.494 kg de ajustes, ni las fechas de los lotes 161
  y 142, ni el consumo sin seguimiento de G0476. Cada uno necesita una decisión de operación con el
  documento físico, y V8.6 exige simular + revertir + gate antes de tocar nada.
- **No se toca `fn_seguimiento_diario_engorde` para que lea `INV_OTRO`.** Sería el cambio «correcto» de
  fondo, pero mueve el saldo de **todas** las empresas y exige el gate de paridad multipaís completo:
  va en su propio plan, con su propia compuerta.
- **No se bloquea el ajuste manual de stock.** Es la herramienta con la que la operación arregla sus
  errores; lo que faltaba era que dejara rastro visible en el cuadre.
