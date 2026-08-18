# V7.27 — El saldo de alimento y el cuadre de engorde ignoran `validado`

**Fecha:** 2026-08-17 · Retoma el último pendiente abierto del bloque V7
([`doble_validacion_bugs_por_empresa_plan.md`](doble_validacion_bugs_por_empresa_plan.md) §6).
**Gate obligatorio:** paridad multipaís (`backend/sql/verificar_paridad_saldo_engorde.sql`), corrido
ANTES y DESPUÉS.

---

## 1. Qué decía el pendiente y qué resultó ser

> V7.27 — *«El saldo de alimento y el cuadre de engorde se recalculan ignorando `validado`. Tocarlo
> exige el gate de paridad multipaís.»*

La mitad del **cuadre** ya se cerró en V7.37/V7.38: `CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas`
compara contra `stock − reservado` y quedó byte a byte igual con el flag apagado.

La mitad del **saldo** se auditó ahora y **la respuesta no es «filtrar la fn por `validado`»**. Que la
fn no mire `validado` es una decisión deliberada y correcta: el alimento se lo comieron las aves el
día que se cargó el seguimiento; la validación es la confirmación administrativa del movimiento de
inventario, no del consumo. Filtrar la fn cambiaría el número de **todas** las empresas (incluidas las
que tienen el flag apagado y arrastran filas con `validado=false` nacidas antes del fix H6) para
mostrar un saldo que le miente a la operación.

**Lo que sí está roto es otra cosa, y es concreta:** la doble validación escribe sus movimientos de
inventario con una **referencia que ningún lector de engorde reconoce**.

| Quién escribe | Referencia |
|---|---|
| `SeguimientoAvesEngordeService.Crud` (alta/edición/borrado) | `Seguimiento aves engorde #<id> <fecha>` |
| `SeguimientoLoteLevanteService.Crud` | `Seguimiento lote levante #<id> <fecha>` |
| `ProduccionService.Seguimiento` | `Seguimiento producción #<id> <fecha>` |
| `SeguimientoDiarioLoteReproductoraService` | `Seguimiento reproductora #<id> <fecha>` |
| **`ValidacionSeguimientoService.AplicarAlimentoAsync`** | **`Seguimiento <modulo.ToLower()> #<id> <fecha>`** |

Esa última línea sale de `$"Seguimiento {modulo.ToLowerInvariant()} #{seguimientoId} …"`, así que
produce `Seguimiento engorde #`, `Seguimiento levante #` y `Seguimiento produccion #` — **tres
literales que no existen en ninguna otra parte del sistema**. Sólo reproductora coincide por
casualidad.

## 2. Las dos consecuencias, medidas

### 🔴 (a) Desvalidar infla el saldo del galpón — REPRODUCIDO

`fn_seguimiento_diario_engorde` calcula
`saldo = apertura + Σ(ingresos/traslados del histórico) − Σ(consumo del seguimiento)`, y excluye a
propósito los `INV_INGRESO` que genera el propio seguimiento (`referencia LIKE 'Seguimiento aves
engorde #%'`) porque son **reversiones contables**, no alimento entrando al galpón.

* **Validar** emite un `Consumo` → `INV_CONSUMO`, que la fn ya ignora por tipo de evento. El saldo no
  se mueve, que es lo correcto: el consumo ya estaba restado desde que se guardó el registro. ✔
* **Desvalidar** emite un `Ingreso` → `INV_INGRESO` con referencia `Seguimiento engorde #…`, que **no
  matchea el filtro** ⇒ la fn lo cuenta como alimento nuevo. El seguimiento sigue existiendo y sigue
  restando su consumo, así que el galpón termina con **el doble**: se le devuelven los kilos y no se
  le vuelve a cobrar el consumo.

Medido en una transacción revertida (`fn_seguimiento_diario_engorde(168)`, ItalcolPanama, granja 106 /
G0490): una devolución de **500 kg** mueve el saldo **+500,000 kg** y el `ingreso_alimento_kg` del día
**+500,000 kg**. La misma fila con una referencia que la fn sí reconoce mueve **0** en ambas columnas.

Y arrastra al cuadre: `descuadre = saldo_tabla − (stock − reservado − mov_post)`. Al desvalidar, el
stock vuelve a subir y la reserva vuelve a `Activa` (o sea que `stock − reservado` queda igual que
antes de validar), pero `saldo_tabla` subió 500 ⇒ **descuadre de 500 kg inventado**, en un galpón que
estaba cuadrado.

### 🔴 (b) El consumo validado no se puede atribuir a su lote

`vw_validacion_alimento_engorde_por_lote` reconcilia el seguimiento contra el inventario y atribuye
cada movimiento a su lote con
`reference LIKE 'Seguimiento aves engorde #%'` + `substring(reference from '#([0-9]+)')`.
El `Consumo` que emite validar lleva `Seguimiento engorde #…` ⇒ **no entra en `inv_cons_attr`** y la
vista lo reporta como `consumo_no_posteado`: un falso positivo exactamente del tipo que esa vista
existe para cazar. Mismo problema en `backend/sql/revertir_anulacion_inv_consumo_seguimiento.sql`
(regex `^Seguimiento aves engorde #(\d+)`).

## 3. El arreglo

**Que la doble validación hable el vocabulario de cada módulo, en vez de inventar uno.** La referencia
pasa a construirse con el mismo literal que escribe el Crud del módulo — que es, byte a byte, el que
ya reconocen la fn del saldo, `fn_cuadre_alimento_engorde`, `fn_reporte_diario_costos_engorde`,
`vw_seguimiento_pollo_engorde`, las 7 consultas C# espejo y las vistas de conciliación.

**No se toca ninguna función SQL.** Con la referencia correcta, los **diez** lectores que ya existen
tratan bien el movimiento sin cambiar una línea. Alternativa descartada: ensanchar el filtro en la fn
+ el cuadre + el reporte diario + la vista de Power BI + 7 consultas EF — cinco veces más superficie,
el mismo resultado, y cada copia es una oportunidad de que una se quede atrás.

### Archivos

| Archivo | Cambio |
|---|---|
| `Application/Calculos/ReservaSeguimientoCalculos.cs` | **NUEVO** `ReferenciaInventario(modulo, seguimientoId, fecha, devolver)`: dueño único del literal, puro y testeable. Mismo patrón que `MigracionPosturaCalculos.ReferenciaConsumoLevante/Produccion`, que ya existe por esta misma razón |
| `Infrastructure/Services/ValidacionSeguimiento/Funciones/ValidacionSeguimientoService.Validar.cs` | `AplicarAlimentoAsync` deja de armar la cadena a mano y delega |
| `tests/ZooSanMarino.Application.Tests/ReservaSeguimientoCalculosTests.cs` | tests del literal por módulo, anclados contra los prefijos que escriben los Cruds |

### Regla de negocio que queda escrita

> Un movimiento de inventario que nace de un seguimiento diario **se referencia con el literal de su
> módulo**. Esa cadena no es decorativa: es la clave por la que el saldo, el cuadre y las
> conciliaciones distinguen «alimento que entró al galpón» de «reversión contable de un consumo».

## 4. Casos de prueba

| # | Caso | Esperado |
|---|---|---|
| T1 | `ReferenciaInventario(ENGORDE, 123, 2026-08-15, devolver:false)` | `Seguimiento aves engorde #123 2026-08-15 (validado)` |
| T2 | `ENGORDE_EC` | idéntico a `ENGORDE` (canónico: misma tabla, misma reserva) |
| T3 | `LEVANTE` | prefijo `Seguimiento lote levante #` |
| T4 | `PRODUCCION` | prefijo `Seguimiento producción #` (**con tilde**, como el Crud) |
| T5 | `REPRODUCTORA` | prefijo `Seguimiento reproductora #` (no cambia: ya coincidía) |
| T6 | `devolver:true` | sufijo `(devolución por quitar la validación)` |
| T7 | El prefijo de engorde matchea `LIKE 'Seguimiento aves engorde #%'` | true — es el filtro literal de la fn |
| T8 | Fecha con cultura no invariante | `yyyy-MM-dd` siempre |
| S1 | **Simulación SQL**: devolución con la referencia NUEVA sobre el lote 168 | saldo e ingreso **0,000** de diferencia |
| S2 | **Gate multipaís** antes/después | **0 en todas las columnas, en las 2 empresas con lotes** |
| S3 | **Runtime**: validar → desvalidar un seguimiento de engorde en ItalcolPanama con el flag ON | saldo, `ingreso_alimento_kg` y cuadre vuelven al valor de partida |

## 5. Fuera de alcance (dicho explícitamente)

* **No se filtra la fn por `validado`** — ver §1. Si algún día se quisiera diferir también el saldo,
  es un cambio de modelo que pide su propio plan y su propio gate.
* **Las referencias ya escritas no se migran.** En la BD local hay **cero** filas con los literales
  viejos (`Seguimiento engorde/levante/produccion #`), porque el flag sólo estuvo encendido durante
  los smokes de V7 y la base se restauró. En prod hay que verificarlo antes de mergear; si aparecen,
  van por migración data-only aparte.
* **Levante/producción/reproductora que devuelvan sobre un galpón donde vive un lote de engorde**
  quedan cubiertos sólo por su prefijo propio, no por el filtro de engorde. Es una asimetría que
  precede a la doble validación (le pasa igual al `devolución por eliminación` de esos módulos) y no
  entra acá.
