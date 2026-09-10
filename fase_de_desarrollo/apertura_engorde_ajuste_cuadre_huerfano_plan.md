# La limpieza de un ciclo borrado se le cobra al ciclo siguiente

**Ticket de operación (10-sep-2026).** *«En el diario de alimento aparece una cantidad de alimento y
en el stock aparece otra. Doña María C-2.»*

Diagnóstico cerrado contra la copia `sanmarinoapplocal` (último histórico 10-sep 10:20, reproduce
las dos capturas del ticket). Relacionado: [[eliminar-ingreso-no-devolvia-el-stock]] (el mecanismo
que se agregó el 1-sep y que este plan corrige), [[descuadre-engorde-se-hereda-entre-ciclos]].

---

## 1. El hecho, con aritmética exacta

**Lote 257 «61 - 1»** — ItalcolPanama, granja **DOÑA MARIA** (106), núcleo **C** (791385), galpón
**2** (`G0490`), encasetado 06-sep-2026.

| | kg |
|---|---|
| Stock del galpón (`AV. POLLITO PREINICIADOR`) | **11.715,000** = 12.169 − 227 − 227 ✅ |
| Saldo de la tabla diaria (`fn_seguimiento_diario_engorde(257)`) | **7.718,44** |
| **Diferencia** | **3.996,56** |

```
fecha       consumo  ingreso   saldo       apertura   documento apertura
2026-09-07    227        0     -4.223,56   -3.996,56  Eliminación de stock
2026-09-08    227    12.169     7.718,44
```

El galpón abre el ciclo en **−3.996,56 kg**. **El stock tiene razón; la tabla diaria está corta por
exactamente los kilos de una eliminación de stock del ciclo anterior.**

> En la captura del ticket el ingreso de 12.169 estaba fechado el día 1 (saldo 7.945,44 = −3.996,56
> + 12.169 − 227); después lo movieron al 08-sep. El día 2 da 7.718,44 en las dos versiones: el
> hueco es el mismo y no depende de esa edición.

## 2. La causa, con horas

| hora (05-sep) | qué pasó |
|---|---|
| 09:56:35 / 09:56:38 | Borran (soft delete) los lotes **168 y 169**, los únicos vivos de `G0490` |
| 09:57:30 | Borran el stock sobrante de `GALPON` — 2.146,348 kg de `AV. SUPER POLLO ENGORDE` |
| 09:58:05 | Borran el stock sobrante de `G0490` — **3.996,560 kg** de `AV. SUPER POLLO ENGORDE` |
| 06-sep | Encasetan el lote **257** |

Cada baja de stock dispara `EliminarStockAsync`
([`InventarioGestionService.StockMutacion.cs:152`](../backend/src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.StockMutacion.cs)),
que escribe **dos** movimientos: el `EliminacionStock` (auditoría, `INV_OTRO`) y un
`AjusteCuadreTablaSalida` (`INV_AJUSTE_CUADRE_SALIDA`) para que la baja también llegue a la tabla
diaria. Ese segundo movimiento es el fix del 1-sep y **es correcto en general**.

El movimiento no lleva lote: se lo pone el trigger, vía
`fn_lote_ave_engorde_id_desde_ubicacion(farm, nucleo, galpon)`, que filtra `deleted_at IS NULL`.
Como los dos únicos candidatos se habían borrado **55 segundos antes**, devuelve **NULL**. Su
propio comentario dice qué pasa entonces:

> *«Si no queda ningún lote vivo en el galpón, devuelve NULL: `fn_seguimiento_diario_engorde`
> conserva las filas sin lote (no se pierde alimento) y **la apertura del ciclo siguiente las
> recoge**, que es el mismo camino del alimento previo al encaset.»*

**Ese fallback es correcto para un INGRESO y equivocado para una SALIDA de cuadre.** Un ingreso sin
lote es alimento que llegó antes que los pollitos: es del ciclo que entra. Una salida de cuadre sin
lote es la limpieza contable de un ciclo que **ya no existe**: no es de nadie, y cobrársela al
siguiente le descuenta alimento que nunca recibió — encima de otro tipo (`SUPER POLLO ENGORDE`
contra el `PREINICIADOR` del ciclo nuevo).

**Por qué las dos guardas existentes no alcanzan** (`apert_mov`, CTE 3 de la fn):

- **v11 `lotes_ajenos`** excluye lo etiquetado con un lote de otro ciclo. Su condición es
  `h.lote_ave_engorde_id IS NULL OR NOT EXISTS (…)` ⇒ con lote NULL **la guarda es tautológica**.
- **v12 `corte_apertura`** sube el piso de la ventana al día siguiente del último seguimiento del
  ciclo anterior, pero busca ese ciclo con `l2.deleted_at IS NULL`: los lotes recién borrados son
  invisibles, así que el piso cae de vuelta en `fecha_encaset − 10` = **27-ago**, y el 05-sep entra.

Aunque los lotes no se hubieran borrado, el corte por fecha tampoco lo habría atrapado: la
corrección se **registra** el día de la limpieza, no el día del hecho que corrige.

## 3. El radio: 45.183,08 kg y creciendo

11 filas `INV_AJUSTE_CUADRE_SALIDA`, todas entre el 05 y el 10-sep, todas en Panamá, **10 de ellas
sin lote**. Es una campaña de limpieza en curso: 5 lotes borrados el 05-sep (Doña María) y **7 más
el 10-sep** (Trofarello).

| galpón | granja | kg | estado |
|---|---|---|---|
| `G0490` | DOÑA MARIA | 3.996,560 | **ya disparó — es el ticket** |
| `GALPON` | DOÑA MARIA | 2.146,348 | quedó atribuido al lote 254 (vivo) y además lo neutralizó un reingreso del mismo monto; hoy cuadra 8.350 = 8.350 |
| `G0496` | TROFARELLO | 16.751,880 | pendiente |
| `G0492` | TROFARELLO | 16.606,799 | pendiente |
| `G0469` | DOÑA MARIA | 2.641,850 | pendiente |
| `G0495` | TROFARELLO | 1.332,314 | pendiente |
| `G0491` | TROFARELLO | 862,523 | pendiente |
| `G0470` | DOÑA MARIA | 807,810 | pendiente |
| `G0494` | TROFARELLO | 37,000 | pendiente |

**39.040,18 kg pendientes** en 7 galpones que hoy no tienen ciclo vivo. El próximo lote encasetado
en cualquiera de ellos dentro de los 10 días de ventana abre con esa cifra en negativo.

---

## 4. El arreglo

### 4.1 Enfoque arquitectónico

Dos capas, y la de la BD es la que cierra el caso:

**(A) `fn_seguimiento_diario_engorde` v19 — la apertura ignora los ajustes de cuadre HUÉRFANOS.**
En `apert_mov` (y su gemela `apertura_docs`), un `INV_AJUSTE_CUADRE_ENTRADA`/`_SALIDA` con
`lote_ave_engorde_id IS NULL` **no entra a la apertura**. Es el complemento exacto de la guarda v11:
donde v11 dice *«nada etiquetado con otro ciclo»*, v19 agrega *«y un ajuste de cuadre sin etiqueta
no es de nadie»*.

**Por qué exactamente esa condición y no una más amplia** — las tres alternativas que se
descartaron, cada una con el número que la mata:

1. ~~Excluir *todos* los `INV_AJUSTE_CUADRE_*` de la apertura.~~ Rompería el caso legítimo del
   ajuste cargado sobre un lote encasetado pero que aún no cargó su día 1.
2. ~~Aceptar solo los del propio lote (`= p_lote_id`).~~ **Rompe un galpón que hoy cuadra**: en
   `GALPON`, el ajuste del lote 254 está en la ventana de apertura del 256 y es correcto que entre
   (comparten bodega, v10). Excluirlo subiría la apertura del 256 de 9.847 a 11.993,35 y su saldo
   final a 10.496,35 contra un stock de 8.350.
3. ~~Cambiar el trato del `EliminacionStock` (`INV_OTRO`).~~ Es el naufragio de v15/v16, que el gate
   multipaís revirtió **dos veces**: mueve filas que ya existen.

La v19 no mueve ninguna fila que no sea un ajuste de cuadre huérfano, y esas **solo existen en
Panamá y solo desde el 05-sep** ⇒ el gate multipaís da 0 en el resto por construcción, no por suerte.

**(B) `EliminarStockAsync` — no escribir un ajuste que ninguna tabla va a leer.** Si el galpón no
tiene lote vivo, el `AjusteCuadreTablaSalida` nace huérfano y (con la v19) inerte: no se escribe.
El `EliminacionStock` se conserva intacto — sigue siendo el registro de que alguien eliminó el
stock. Es defensa en profundidad y, sobre todo, deja de generar filas que después hay que explicar.

**Qué NO se toca:** `CuadreAlimentoEngordeService.CuadrarGalponAsync` («Cuadrar galpón») parte de
una fila del cuadre que **siempre** trae `LoteAveEngordeId` ⇒ sus ajustes nunca son huérfanos y su
comportamiento queda idéntico.

### 4.2 Por qué esto resuelve los puntos 2 y 3 sin tocar un solo dato

La v19 es retroactiva por naturaleza: la fn se recalcula en cada lectura.

- **Punto 2 (reparar lo disparado):** la apertura del lote 257 pasa de −3.996,56 a 0 ⇒ saldo del
  08-sep = 12.169 − 454 = **11.715 = stock**. Sin `UPDATE` de datos.
- **Punto 3 (desactivar las minas):** los 39.040,18 kg huérfanos quedan fuera de la apertura de
  cualquier ciclo futuro. Sin `UPDATE` de datos.

> El día 1 del lote 257 queda en **−227** (consumo del 07-sep contra un ingreso fechado el 08-sep).
> Es la otra señal del cuadre —**día en rojo**, no kilos faltantes—: el total cierra y la causa es la
> fecha del ingreso, que es un dato de operación, no un defecto de cálculo. Ver CLAUDE.md § *El
> cuadre se mira, no se espera*. Se informa al usuario, no se corrige por código.

### 4.3 Archivos

**Backend — BD (el vehículo):**

| archivo | qué |
|---|---|
| `backend/src/ZooSanMarino.Infrastructure/Migrations/20260910120000_FnSeguimientoEngordeV19AperturaIgnoraAjusteHuerfano.cs` | migración: `Up` = v19, `Down` = v18 verbatim |
| `…_FnSeguimientoEngordeV19AperturaIgnoraAjusteHuerfano.Fn.cs` | partial con las dos constantes SQL (v19 y v18) |
| `…_FnSeguimientoEngordeV19AperturaIgnoraAjusteHuerfano.Designer.cs` | copia del `ModelSnapshot` (migración model-neutral) |
| `backend/sql/fn_seguimiento_diario_engorde.sql` | **espejo** actualizado a v19 |
| `backend/sql/verificar_ajuste_cuadre_huerfano_engorde.sql` | verificador de solo lectura: cuántos huérfanos hay, en qué galpones, y si el ciclo vivo cuadra contra su stock (exento del gate de migración por el prefijo `verificar_`) |

Sin DDL de tablas, sin cambio de firma (49 columnas OUT) ⇒ los 5 consumidores por
`CROSS JOIN LATERAL` no se tocan y el `ModelSnapshot` queda intacto.

**Backend — C#:**

| archivo | qué |
|---|---|
| `…/InventarioGestion/Funciones/InventarioGestionService.StockMutacion.cs` | `EliminarStockAsync` resuelve si hay lote vivo y omite el ajuste huérfano |
| `backend/src/ZooSanMarino.Application/Calculos/AjusteCuadreAlimentoCalculos.cs` | decisión pura `DebeEscribirAjusteDeTabla(hayLoteVivo)` |
| `backend/tests/ZooSanMarino.Application.Tests/AjusteCuadreAlimentoCalculosTests.cs` | tests de la decisión pura |

**Frontend:** ninguno. La grilla lee la fn.

### 4.4 Reglas de negocio

1. Un **ingreso** sin lote en la ventana previa al encaset **es** del ciclo que entra (sin cambio).
2. Un **ajuste de cuadre** sin lote **no es de ningún ciclo**: no entra a ninguna apertura.
3. Un ajuste de cuadre **con** lote sigue exactamente las reglas de hoy (v10 bodega compartida, v11
   lotes ajenos, v12 corte de ciclo).
4. Eliminar un registro de stock en un galpón **sin lote vivo** baja el stock y **no** escribe ajuste
   de tabla: no hay tabla diaria que corregir.
5. El `EliminacionStock` se escribe **siempre** (auditoría), haya o no lote vivo.

### 4.5 Casos de prueba

**Gate multipaís obligatorio** (CLAUDE.md § *Gate multipaís al tocar cálculo compartido*):
`EXCEPT` en los dos sentidos, todas las columnas, **todas** las empresas, antes y después.
Toda empresa que no sea Panamá debe salir con **0**.

| # | caso | esperado | medido |
|---|---|---|---|
| T1 | Lote 257 (el ticket) | apertura −3.996,56 → **0**; saldo 08-sep 7.718,44 → **11.715 = stock** | ✅ exacto |
| T2 | Lote 256 (`GALPON`, ajuste CON lote 254, bodega compartida) | **sin cambio**: apertura 9.847, saldo 8.350 = stock | ✅ |
| T3 | Lote 254 (ajuste en su propio ciclo, vía `hist_alimento`) | **sin cambio**: saldo 8.804 | ✅ |
| T4 | Censo de todos los lotes vivos de todas las empresas | 0 filas nuevas, 0 que desaparecen, **0 distintas fuera del 257** | ✅ 7.051 filas / 174 lotes; único cambio: lote 257 (2 filas) |
| T5 | Ecuador (132 lotes con filas) | 0 | ✅ 0 |
| T6 | `verificar_ajuste_cuadre_huerfano_engorde.sql` antes/después | `dif_kg` deja de contener los kilos huérfanos | ✅ G0490 `dif_kg` **3.996,560 → 0,000** |
| T7 | Ajuste de cuadre CON lote dentro de la ventana de apertura de ese mismo lote | sigue contando (no se rompe «Cuadrar galpón») | ✅ = T2 |
| T8 | `EliminarStockAsync` en galpón **con** lote vivo | escribe los 2 movimientos (igual que hoy) | ✅ `G0490`→257, `GALPON`→256 |
| T9 | `EliminarStockAsync` en galpón **sin** lote vivo | escribe **solo** el `EliminacionStock` | ✅ los 7 galpónes con huérfanos dan NULL |
| T10 | `Down` de la migración | la fn vuelve a v18 byte a byte | ✅ 0 y 0 sobre 7.051 filas |

> **Cobertura real del gate:** el engorde vive solo en dos empresas — ItalcolEcuador (132 lotes con
> filas) e ItalcolPanamá (42). Sanmarino, Demo y Santa Reyes **no tienen un solo lote de engorde**,
> así que su 0 es trivial y hay que decirlo, no presentarlo como cobertura.

**Validación:** `dotnet build` (0 err / 0 warn) · `dotnet test` · `yarn build` ·
`dotnet ef database update` en local sin error · `node backend/scripts/verificar-sql-llega-por-migracion.js`.

### 4.6 Fuera de alcance (se informa, no se corrige acá)

- El **día 1 en rojo** del lote 257 (ingreso fechado un día después del primer consumo): dato de
  operación.
- El consumo del galpón se escribe con la referencia `Seguimiento reproductora #…` porque los días
  1-7 de engorde nacen del cruce de reproductora. No afecta este cálculo (la fn ignora el consumo
  por tipo de evento), pero conviene auditarlo aparte contra
  [[referencia-movimiento-es-clave-de-lectura]].

---

## 5. Resultado de la validacion (10-sep-2026)

| paso | resultado |
|---|---|
| `dotnet build` (Domain + Application + Infrastructure) | **0 errores / 0 warnings** |
| `dotnet test` | **4.087 + 1 pasan / 0 fallan** (base 4.081 + 1; los 6 casos nuevos cierran la cuenta) |
| **Prueba negativa** del gate de tests | anulando la regla (`=> true`) fallan **exactamente los 3** casos que la cubren, ninguno mas; restaurado y verde |
| Gate multipais (`EXCEPT` en los dos sentidos, todas las columnas) | 7.051 filas / 174 lotes / las 2 empresas con engorde — **el unico lote que cambia es el 257** (2 filas). Ecuador **0** |
| T10 `Down` | v18 vuelve **byte a byte**: 0 y 0 sobre las 7.051 filas |
| T1 (el ticket) | `G0490` saldo **7.718,44 → 11.715,000 = stock**, `dif_kg` **3.996,560 → 0,000** |
| T2 / T3 | lotes 256 (8.350 = stock) y 254 (8.804) **intactos** |
| `verificar-sql-llega-por-migracion.js` | OK |
| Orden con la migracion `20260910010000` de la otra sesion | sin colision: recrea `fn_aplicar_correccion_despachos_sin_peso` y `fn_auditoria_liquidacion_engorde`, no esta |

**Lo que NO se hizo, y por que:** no se corrio un smoke runtime de `DELETE /stock/{id}`. Ese camino
**escribe datos reales** y la BD local es compartida entre sesiones y worktrees (ver
[[smokes-y-testigos]] y [[entorno-local-toolchain]]). La decision esta cubierta por los tests de la
funcion pura y su **entrada** se verifico contra la misma `fn_lote_ave_engorde_id_desde_ubicacion`
que usa el trigger, sobre las 9 ubicaciones reales que hoy tienen ajustes de cuadre.

**Tampoco se aplico la fn a la BD local compartida**: toda la medicion se hizo en transacciones
revertidas, para no cambiarle el comportamiento de la fn a la otra sesion que esta trabajando en el
mismo repo. La migracion la aplica el deploy (`Database__RunMigrations=true`).
