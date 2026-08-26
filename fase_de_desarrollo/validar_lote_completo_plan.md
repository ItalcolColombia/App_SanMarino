# «Validar todos los pendientes del lote» — el apoyo de UI que la regla exige

> Paso 1 de la secuencia de [`plazo_validacion_desde_creacion_plan.md`](plazo_validacion_desde_creacion_plan.md) §11.6.
> Tracker: bloque **EC7**.

---

## 1. Por qué existe

Hoy se valida **de a uno**: `POST /api/SeguimientoValidacion/{modulo}/{id}/validar`, y el front
dispara una request por fila. Eso alcanzaba mientras la captura era un día por vez. Ya no:

- **ItalcolPanama cargó 6 o más días en una sola sesión 41 veces en un mes, con un pico de 34 días.**
  Confirmar eso a mano son 34 clics y 34 requests.
- **El plazo desde `created_at`** (commit `94e1f9f`) hace que un lote de días viejos entre completo en
  una sola carga — y al día siguiente los N vencen **todos juntos** y bloquean hasta confirmarse.
  Cuantos más días entran de una, más pesa validar de a uno.
- **La regla de huecos** (§9 del plan hermano) lo vuelve obligatorio: llenar 4 huecos seguidos deja 4
  registros sin validar que hay que confirmar uno por uno antes de poder seguir.

En corto: el cambio de plazo y la regla de huecos **suben el tamaño del lote a confirmar**, y la UI
sigue siendo de a uno.

---

## 2. Qué se construye

Un **«validar todos los pendientes del lote»**: un endpoint que valida en bloque los registros sin
validar de un lote, y un botón en las listas que lo dispara con confirmación previa.

### Lo que NO se construye (y por qué)

- **«Guardar y validar» en el modal de alta.** Es la otra mitad de la propuesta original, pero suma
  una decisión al formulario de captura y no resuelve el caso grande (34 días ya cargados). Se puede
  agregar después sobre la misma base; hacerlo ahora mezcla dos cambios.
- **Desvalidar en bloque.** Deshacer descuentos en masa es peligroso y nadie lo pidió.

---

## 3. Lo que ya está verificado del código

| Qué | Dónde |
|---|---|
| Endpoint individual y su manejo de errores (403 por permiso, 400 por regla) | `SeguimientoValidacionController.cs` |
| `ValidarAsync` es **idempotente**: si ya está validado devuelve un resultado en cero | `ValidacionSeguimientoService.Validar.cs` |
| Chequea permiso (`PermisoValidar`) y **empresa** (`EsDeLaEmpresaActiva`), fail-closed | ídem |
| 🔴 **Transacción CONDICIONAL**: no abre la suya si ya hay una ambiente (patrón del push offline de la PWA, porque EF lanza al abrir una segunda sobre el mismo contexto) | ídem |
| `ObtenerPendientesAsync` ya devuelve los pendientes con fecha, estado y límite, acotado por empresa | `ValidacionSeguimientoService.Pendientes.cs` |
| El front consume `validar()` desde 3 listas: engorde, levante y producción | `shared/services/validacion-seguimiento.service.ts` |
| ⚠️ La ruta **no puede llevar `admin`** en el path — el WAF devuelve 403 | comentario del propio controller |

---

## 4. Las tres decisiones que definen el diseño

> Se resuelven con la especificación adversarial (4 diseños + 3 refutaciones). Van acá con su
> evidencia cuando cierre.

1. **¿Una transacción para todo, o una por registro?** La transacción condicional de `ValidarAsync`
   hace que envolver el bloque convierta la operación en **todo-o-nada**; no envolverlo permite
   **éxito parcial**. Con 34 registros y uno que falla por falta de stock, las dos opciones dan
   experiencias muy distintas.
2. **¿Importa el orden cronológico?** Validar descuenta inventario y aves. Si el resultado depende
   del orden, hay que forzar «del más viejo al más nuevo» y decirlo en un test.
3. **¿Cortar en la primera falla o seguir?** Cortar preserva la integridad cronológica y da un
   mensaje claro; seguir puede producir una cascada de errores derivados del primero.

---

## 5. Reglas que gobiernan este cambio

- **Flag apagado ⇒ nada visible ni distinto.** En una empresa sin doble validación el botón no existe.
- **Lógica pura a `Application/Calculos/` + xUnit**, con el caso «lista vacía» y «todos ya validados».
- **Angular 22**: el componente que agregue estado mutable lleva `changeDetection: Eager` explícito.
- **Confirmación con `ConfirmDialogService`**, notificación con `ToastService`. Nada de `confirm()`.
- **Sin migración** salvo que haga falta un permiso nuevo — y la primera opción es **reusar el
  permiso de validar que ya existe**, no inventar uno.
- Validar: `dotnet build` + `dotnet test` + `yarn build`, con el toolchain portable
  (`~/dotnet-portable`, `~/node-portable/node-v22.23.1-win-x64`), y capturando el exit code **sin
  pipe** (`cmd > log 2>&1; echo $?`).

---

## 6. Las decisiones, con su evidencia

Especificación adversarial: 4 diseños + 3 refutaciones. **Las refutaciones corrigieron dos cosas que
habrían causado corrupción silenciosa**, y las dos se verificaron a mano antes de escribir código.

| # | Decisión | Elección |
|---|---|---|
| D1 | ¿Una transacción o una por registro? | **Una por registro**, y el bloque **se niega a correr dentro de una transacción abierta** |
| D2 | ¿Importa el orden? | **Sí, cambia el resultado.** Cronológico, impuesto por el servidor |
| D3 | ¿Cortar en la primera falla? | **Sí.** El resto queda `NO_INTENTADO` |
| D4 | ¿Diferir la sincronización del cruce? | **No.** `ValidarAsync` queda intacto |
| D5 | Tope | **60** registros, tomando los más viejos |
| D6 | HTTP del corte | **200** con el detalle en el cuerpo; 403 sólo por permiso |
| D7 | ¿El cliente manda los ids? | **No.** El servidor resuelve el conjunto |

### 6.1 D1 — una por registro, y una guarda ruidosa

**El éxito parcial es el punto del feature.** Hoy 34 POST son 34 transacciones: si el 20 falla, los 19
quedan. Un botón que devuelva **cero** donde el de a uno devolvía 19 sería peor que no tenerlo. Y la
falla no es el caso excepcional: un backlog de 34 días es alguien que cargó 34 días de consumo, y que
a la mitad le falte el ingreso de alimento es lo esperable.

🔴 **Por eso el bloque se niega a correr dentro de una transacción abierta.** `ValidarAsync` abre la
suya **sólo si no hay una ambiente**. Con una envolvente, ninguno de los N commitearía y el bloque se
volvería todo-o-nada **en silencio**, sin que ningún test unitario lo note. Se prefiere el error
explícito a la sorpresa.

### 6.2 🔴 El hallazgo que evita corrupción silenciosa: `ChangeTracker.Clear()`

Al capturar una falla hay que limpiar el ChangeTracker, y el motivo es sutil:

> La transacción de `ValidarAsync` revierte **la base**, no el ChangeTracker. Las entidades ya
> guardadas quedan en memoria con el valor nuevo y marcadas `Unchanged`, así que el registro
> siguiente las reusa por identity map y descuenta desde un saldo que en la base **nunca existió**.

Y no hay forma de que se note solo, por una asimetría verificada en el código: la **guarda** de aves
lee con `AsNoTracking()` —ve el valor real revertido y **pasa**— mientras los **aplicadores** leen
rastreado y reciben la instancia envenenada. Sin el `Clear()`, el bloque descontaría de más y ninguna
validación lo detendría.

### 6.3 🔴 El orden **sí** cambia el resultado

Lo que **no** cambia (verificado, y vale decirlo para no sobre-diseñar): el kardex, la tabla diaria y
el cuadre son indiferentes — la fecha del movimiento sale de la **reserva**, no del momento de validar.

Lo que **sí**: la guarda de aves compara **totales** (`ReservaSeguimientoCalculos.MotivoAvesNoAplicable`)
mientras el descuento recorta **por bucket** (`RetiroAvesEngordeCalculos.AplicarPorBucket`, con
`Math.Min` por género). Con un lote de 100 hembras y 0 machos, un día que baja 50 machos y otro que
baja 60 hembras **validan los dos en un orden y cortan en el otro**. Mismo dato, distinto resultado.

Por eso el orden lo impone el **servidor** y no llega nunca del cliente, y su test es un test de
**corrección**, no de ergonomía.

### 6.4 D3 — cortar

Los días siguientes consumen del **mismo stock del mismo galpón** que acaba de rechazar al que falló:
seguir daría una cascada de mensajes derivados del primero. Y el caso *menos* probable es peor — si
alguno usa otro ítem con stock, se validaría y dejaría el que falló **pendiente rodeado de validados**,
que es exactamente lo que vuelve a bloquear el alta de días nuevos.

**Excepción al corte:** un registro **ya validado** (carrera con otra pestaña) no es falla — se cuenta
`YA_VALIDADO` y se sigue.

---

## 7. Lo que quedó construido

| Capa | Archivo |
|---|---|
| Cálculo puro | `Application/Calculos/ValidacionEnBloqueCalculos.cs` — orden, clasificación, resumen y el mensaje |
| DTOs | `ResultadoValidacionEnBloqueDto` + su ítem, y `YaEstabaValidado` en `ResultadoValidacionDto` |
| Service | `ValidacionSeguimiento/Funciones/ValidacionSeguimientoService.ValidarEnBloque.cs` (partial) |
| Endpoint | `POST /api/SeguimientoValidacion/{modulo}/lote/{loteId}/validar-pendientes` |
| Front | `validarPendientesDelLote()` + botón en las 3 listas, con confirmación y supresión del modal doble |
| Tests | `ValidacionEnBloqueCalculosTests.cs` — **+39 casos** |

### 7.1 El detalle de UI que no era obvio

Tras un corte **quedan vencidos por definición**, y la recarga del lote dispara el modal rojo de
pendientes. Sin `suprimirAlertaPendientes`, ese modal se apilaría **sobre** el del resultado: el
operario vería dos modales encimados y el que importa —el que dice qué día falló— quedaría abajo.

### 7.2 Gating

El botón reusa `puedeValidar`, que ya combina **el permiso** y **el flag de la empresa**
(`requiereValidacion`, fail-closed desde `/pendientes`). En una empresa sin doble validación el botón
no existe: no hay permiso nuevo ni migración.

---

## 8. Lo que NO se hizo

- **«Guardar y validar»** en el modal de alta: suma una decisión al formulario de captura y no
  resuelve el caso grande. Se puede agregar después sobre esta misma base.
- **Desvalidar en bloque:** deshacer descuentos en masa es peligroso y nadie lo pidió.
- **Diferir la sincronización del cruce** de reproductora: `ValidarAsync` queda byte a byte intacto.
  Donde el cruce dispara, diferirlo lo sacaría de la transacción que hoy lo protege.
