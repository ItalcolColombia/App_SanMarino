# Plan — Cantidad de alimento sin ítem: exigir el ítem en Seguimiento Diario de Levante y Producción

## Requerimiento (tal como lo dio el usuario, 18-sep-2026)

> En el módulo de seguimiento diario producción y levante, quiero que cuando coloque valor en el campo
> de alimento (la cantidad) se vuelva obligatorio seleccionar el ítem, ya que pueden seleccionar la
> cantidad pero no el alimento.

Ticket de operación que lo originó (cita):

> Al momento de don Diego realizar un consumo de alimento, puso la cantidad, pero no seleccionó el ítem
> de alimento, y dejó guardar el registro pero la salida no la hizo. En este caso, si la cantidad en
> consumo es diferente a 0, ¿debería obligar a que se seleccione un ítem de alimento?

Respuesta: **sí**. Alcance: **Levante** (`lote-levante/pages/modal-create-edit`) y **Producción**
(`lote-produccion/pages/modal-seguimiento-diario`), alta y edición. Solo frontend.

---

## Causa raíz (medida, no supuesta)

Reproducido leyendo el código y el registro real del ticket (seguimiento de producción **#676**, lote 152,
empresa 6, 16-sep-2026, copia local de prod):

1. La fila de alimento **fija** (`esFijo`) de Producción nace con `catalogItemId` **sin** `Validators.required`
   (`crearItemGroup`: `opcional = esFijo || permiteSeguimientoDiarioParcial`). En Levante las filas nuevas
   (`agregarItemHembras/Machos/General`) nacen **sin ningún validador** en tipo/ítem/unidad (Feature 13).
   Es decir: el formulario es **válido** con cantidad 1200 e ítem «— Seleccione —».
2. `onSave()` arma el request **descartando en silencio** toda fila sin ítem:
   - Producción: `.filter(x => x.tipoItem && (x.catalogItemId || x.itemInventarioEcuadorId))`
   - Levante: `if (!tipoH || !itemValue.catalogItemId) return;` (hembras, machos) y `&& itemValue.catalogItemId` (generales)
3. Sin filas, `useItems = false` y `consumoH` sale de un control legado en 0.
   **Resultado en BD (verificado): `cons_kg_h = 0`, `cons_kg_m = 0`, `metadata` sin `itemsHembras`, cero
   movimientos de inventario.** No se perdió solo la «salida»: **se perdió el consumo entero**, y el
   operario cree que lo registró.
4. El backend **no lo puede atajar**: `ItemConsumoCalculos.AcumularPorOrigen` ignora a propósito los ítems
   sin id («la fila vacía que el formulario deja abierta»), y el consumo escalar sin ítem es un camino
   legítimo (app móvil, carga masiva). Ver «Fuera de alcance».

Aplica a **todas** las empresas: con el flag `permite_seguimiento_diario_parcial` OFF la fila fija también
es opcional. Con el flag ON (Santa Reyes, la del ticket) se agrava porque «nada es obligatorio».

---

## Enfoque arquitectónico

- **Una sola regla, una sola función**: `cantidad > 0 ⇒ ítem elegido`. Vive como función PURA en
  `frontend/src/app/shared/utils/inventario/consumo-sin-item.funcion.ts` (junto a `stock-por-silo.funcion.ts`,
  que ya comparten los dos modales). Los dos componentes delegan; ninguno reimplementa la condición.
- **No es un flag de empresa** (CLAUDE.md §Features por EMPRESA no aplica): no hay comportamiento distinto
  por tenant. Una cantidad sin producto es inválida en todas partes, con el flag `parcial` ON u OFF. El flag
  relaja **qué se puede dejar vacío** (fila con cantidad 0 e ítem vacío sigue guardando), no permite
  **perder** una cantidad.
- **Tres capas de defensa en cada modal**, todas apoyadas en la misma función:
  1. **Aviso en línea** bajo el select «Ítem» de la fila + borde inválido (`is-invalid`, ya estilado en los dos SCSS).
  2. **Botón guardar deshabilitado** (`hayCantidadSinItem`, junto a `form.invalid || hasCantidadExcedida`) +
     aviso en el pie del modal, visible desde cualquier pestaña.
  3. **Guarda en `onSave()`** con `ToastService.error` (por si se llega por otro camino que el botón):
     nombra el bloque («Hembras», «Machos», «Ítems generales») y la fila cuando hay varias.
- **Excepción deliberada — fila «hidratada» sin tocar no bloquea.** Un registro cargado por migración masiva o
  por la app móvil guarda su consumo como escalar, sin ítem; al abrirlo, `populateForm` arma una fila con esa
  cantidad y sin ítem. Hoy se puede editar la mortalidad de ese registro y el consumo escalar se conserva
  (la fila se descarta y `consumoH` viaja del control legado). Exigirle un ítem **inventaría una salida de
  inventario nueva** solo por corregir otra cosa: prohibido (refactor ≠ cambio de comportamiento). Por eso la
  regla solo mira filas que el operario **tocó** (`FormGroup.dirty`; nadie llama `markAsDirty` a mano en los
  dos componentes, así que `dirty` = interacción real). Si toca esa fila, ya no es un escalar heredado y
  aplica la regla completa.
- **Bloques ocultos no bloquean**: con `consumoAlimentoSoloHembras` (Santa Reyes) el bloque Machos no se
  pinta; sus filas no cuentan (el operario no podría corregirlas).
- Sin cambio de contrato: el payload que sale del modal es idéntico cuando la regla se cumple.

---

## Archivos a crear / modificar

### Frontend (nuevos)
1. `frontend/src/app/shared/utils/inventario/consumo-sin-item.funcion.ts` — funciones puras:
   - `filaTieneCantidadSinItem(fila)` — `editadaPorElOperario && Number(cantidad) > 0 && !(Number(catalogItemId) > 0)`.
   - `filasConCantidadSinItem(bloques)` — ubicaciones `{ bloque, fila (1-based), enBloqueDeVarias }`.
   - `mensajeCantidadSinItem(ubicaciones)` — texto del toast (singular/plural, con fila si el bloque tiene varias).
   - `MENSAJE_ITEM_REQUERIDO_EN_FILA` — texto corto del aviso en línea (una sola fuente para las dos plantillas).
2. `frontend/src/app/shared/utils/inventario/consumo-sin-item.funcion.spec.ts` — Jasmine, ver §Casos de prueba.

### Frontend (modificados)
3. `lote-produccion/pages/modal-seguimiento-diario/modal-seguimiento-diario.component.ts` — `filaSinItemConCantidad(control)`,
   `get hayCantidadSinItem`, `textoItemRequerido`, guarda al inicio de `onSave()`. No se toca `crearItemGroup` ni
   los validadores del flag `parcial` (no se los endurece: una fila vacía sigue siendo válida).
4. `…/modal-seguimiento-diario.component.html` — en las filas Hembras y Machos: `[class.is-invalid]` en el select
   «Ítem», `<small class="ux-hint text-danger">` bajo él, y el botón `[disabled]` suma `|| hayCantidadSinItem`;
   aviso en el `footer`.
5. `lote-levante/pages/modal-create-edit/modal-create-edit.component.ts` — lo mismo, con tres bloques
   (Hembras, Machos, Ítems generales).
6. `…/modal-create-edit.component.html` — lo mismo en las filas Hembras, Machos y Generales + botón + pie.

### Backend / BD
Ninguno. Sin migración, sin SQL, sin flag, sin tocar `fn_seguimiento_diario_*` (no cambia ningún cálculo).

---

## Reglas de negocio

1. Fila con cantidad **> 0** y sin ítem elegido ⇒ **no se puede guardar**. Vale para alimento y para
   cualquier otro tipo de ítem (medicamento, insumo…): la pérdida silenciosa era la misma.
2. Cantidad 0 / vacía + ítem vacío ⇒ válido (fila sin usar). Sigue funcionando el flag `parcial`.
3. Cantidad > 0 + ítem elegido ⇒ igual que hoy (validaciones de stock/silo intactas).
4. El aviso es **el mismo con el flag `parcial` ON u OFF**, en las cuatro combinaciones país/empresa.
5. Registro editado con consumo escalar sin ítem y fila sin tocar ⇒ se guarda como hoy (ver excepción).
6. Editar un registro que tenía ítem y **dejar el select en «— Seleccione —» con cantidad** ⇒ bloquea. Hoy eso
   descartaba la fila y ponía el consumo en 0; en Colombia (modelo B nivel granja) el backend interpreta la
   diferencia `nuevo − viejo` como negativa y **devuelve el stock** sin que nadie lo pidiera.
7. Orden de validaciones en `onSave()`: primero esta (es la que evita pérdida de datos), después silo/huevos.

---

## Casos de prueba

### Unitarios (Jasmine, `consumo-sin-item.funcion.spec.ts`)
| # | Entrada | Esperado |
|---|---|---|
| 1 | cantidad 0 / null / '' / undefined, sin ítem, tocada | no marca |
| 2 | cantidad 1200, ítem 45, tocada | no marca |
| 3 | cantidad 1200, ítem null / 0 / '' / undefined, tocada | **marca** |
| 4 | cantidad '1200' (string) y ítem '45' (string) | resuelve como número (no marca / marca según ítem) |
| 5 | cantidad negativa o NaN, sin ítem | no marca |
| 6 | cantidad 1200, sin ítem, **no tocada** (hidratada) | no marca (excepción legado) |
| 7 | `filasConCantidadSinItem` con dos bloques | orden estable, fila 1-based, `enBloqueDeVarias` correcto |
| 8 | bloque vacío / sin filas / fila null | `[]` sin lanzar |
| 9 | `mensajeCantidadSinItem` 0 / 1 / varias ubicaciones | '' / texto singular con fila / texto plural sin repetidos |

### Build
`cd frontend && yarn build` — 0 errores (único warning aceptado: *bundle budget* preexistente).
`ng test --watch=false --browsers=ChromeHeadless --include=<spec>` — verde.

### Smoke en navegador (backend :5002 + front :4200 locales, BD local, sesión minteada) — lo que NO ve un test puro
Producción **y** Levante, alta **y** edición:
1. Teclear cantidad **sin** ítem ⇒ borde rojo + aviso bajo «Ítem» + aviso en el pie + botón deshabilitado.
2. Elegir el ítem ⇒ desaparece todo y el botón se habilita.
3. Dejar cantidad en 0 (o vaciar) ⇒ desaparece y se puede guardar sin ítem (flag `parcial`).
4. Abrir el registro de un día **con ítem**, vaciar el select ⇒ bloquea.
5. Abrir un registro con consumo escalar **sin ítem** (si hay uno en la BD local) y editar solo mortalidad ⇒ **guarda** (excepción).
6. Abrir/cerrar el modal dos veces ⇒ el estado no se arrastra (`resetForm` recrea las filas).
7. Empresa con silos (Santa Reyes): el aviso de ítem sale antes que el de silo.

### Backend
Sin cambios ⇒ `dotnet build`/`dotnet test` no se ven afectados; no se corren para este cambio.

---

## Fuera de alcance (a propósito) — y por qué

- **Guarda en el backend.** `ItemConsumoCalculos.AcumularPorOrigen` documenta que ignora ítems sin id porque
  «reventar ahí haría fallar el guardado de un día perfectamente válido» (fila vacía), y el consumo escalar
  sin ítem es el camino de la app móvil y de la carga masiva. Un 400 del servidor solo sería seguro para
  `cantidad > 0` con id ausente **dentro de un ítem**, y habría que medir primero qué clientes lo mandan.
  Se deja anotado como seguimiento, no se mezcla con este arreglo de UI.
- **App móvil (`zootecnicoapp`)**: `ItemsConsumo.armar` descarta las líneas incompletas «en silencio»
  (`lineas.where((l) => l.valida)`): mismo patrón. Es otro cliente y otro release.
- **Engorde y Reproductora** (`engorde-comun`, `modal-seguimiento-reproductora`): no se tocan (el pedido nombra
  Producción y Levante). Si comparten el patrón, se replica con la misma función compartida.
- **Registro #676**: quedó guardado con consumo 0 y sin salida de inventario (verificado: no hay movimiento
  «Seguimiento producción #676» en la BD local). Corregirlo = abrirlo, cargar cantidad + ítem y guardar de
  nuevo: en Colombia el backend compara contra el metadata guardado (sin ítems) y aplica la diferencia
  completa como consumo adicional, validando stock antes de persistir. No se toca la BD a mano.
