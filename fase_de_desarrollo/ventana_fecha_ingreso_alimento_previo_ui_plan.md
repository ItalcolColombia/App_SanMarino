# La excepción D4 (alimento previo al encaset) tiene que ser alcanzable desde la UI

**Origen:** hallazgo **§2.3a** de la *«Auditoría de cierre — alimento previo al encaset»*
(`tracker_estado.md`), el de mayor severidad que quedaba abierto en ese bloque.
**Fecha:** 2026-08-17

## 1. El problema, en una frase

El backend **ya acepta** que un ingreso de alimento se feche en el mes anterior cuando esa fecha cae
dentro de la ventana de alimento previo a un encasetamiento REAL del galpón (excepción **D4**), pero
**el front no deja tipear esa fecha** y encima el hint dice *«Solo se admite el mes en curso»*. O sea:
la pantalla es más estricta que la regla, y le está diciendo a la operación que **falsee la fecha**.

**Impacto medido por la auditoría:** 39 de 110 encasets 2026 de Ecuador (**35 %**) y 10 de 60 de
Panamá caen a principio de mes ⇒ su alimento llegó el mes anterior. Ningún número sale mal: lo que se
pierde es **la fecha contable real**, que es exactamente lo que contabilidad pidió.

## 2. Estado verificado del código (17ago26)

| Pieza | Estado |
|---|---|
| `VentanaFechaMovimientoInventarioCalculos.EsFechaPermitidaConEncasetProximo` + `MensajeFueraDeVentanaConEncaset` | ✅ escrito |
| Tests xUnit (`VentanaFechaMovimientoInventarioEncasetProximoTests.cs`) | ✅ 184 líneas |
| `IInventarioGestionService.ResolverVentanaAlimentoPrevioEncasetAsync` / `…DeIngresoAsync` | ✅ implementados |
| `POST /ingreso` y `PUT /ingresos/{id}/fecha` usan `ValidarVentanaFechaIngresoAsync` | ✅ (controller :163 y :401) |
| **Endpoint que EXPONGA la ventana al front** | ❌ **no existe** |
| Front: `[min]` del datepicker y guarda previa | ❌ **corta en el día 1 del mes** |

Las otras tres puertas manuales (**traslado**, **fecha de traslado**, **stock**) conservan la regla
dura a propósito: D4 es del alimento que llega antes que los pollitos, no de un traslado.

## 3. Enfoque arquitectónico

**El controller sigue siendo la autoridad.** El front es UX (lo dice el encabezado de
`ventana-fecha-movimiento.funcion.ts`). De ahí las dos decisiones que ordenan todo el cambio:

- **D-1 · El front deja de pre-juzgar lo que no puede saber.** Una fecha fuera del mes en curso pero
  dentro de los 30 días de tope **no se bloquea en pantalla**: viaja y la resuelve el controller, que
  ya devuelve un 400 con el mensaje exacto (`MensajeFueraDeVentanaConEncaset` nombra el encaset y el
  rango admitido). Así no hay dos reglas que puedan divergir.
  ⚠️ Por qué NO se replica la regla completa en TS: el encaset que manda es *el más cercano con
  `fecha_encaset >= fecha del movimiento`*, así que **depende de la fecha que el usuario elija**.
  Un espejo en el front resolvería un encaset distinto al del backend y **rechazaría fechas que el
  backend acepta** — el mismo defecto que este trabajo viene a arreglar, del otro lado.
- **D-2 · El endpoint nuevo es informativo, no decisorio.** Sirve para que el hint diga el rango real
  del galpón («encasetamiento del 03/09: admite del 24/08 al 03/09») en vez de una promesa vaga.
  Si falla o no hay galpón, la pantalla cae al texto genérico y **no bloquea** (la puerta cerrada
  sigue siendo el controller).

## 4. Archivos a crear / modificar

### Backend
- **`ZooSanMarino.API/Controllers/InventarioGestionController.cs`** — dos GET nuevos, ambos
  delegando en los resolvers que YA existen (sin lógica propia):
  - `GET /api/inventario-gestion/ventana-fecha-ingreso?farmId&nucleoId&galponId&fecha` → alta.
  - `GET /api/inventario-gestion/ingresos/{movimientoId}/ventana-fecha?fecha` → edición de fecha.
- **`ZooSanMarino.Application/Calculos/VentanaFechaMovimientoInventarioCalculos.cs`** — una función
  **pura** nueva, `ExtremosVentanaIngreso(hoy, proximoEncaset, diasVentanaEmpresa)`, que devuelve el
  `(min, max)` del datepicker: el mínimo se corre hacia atrás sólo si el intervalo del encaset
  intersecta `[hoy−30, hoy]`. Es la ÚNICA aritmética nueva y va con tests.
- **`ZooSanMarino.Application/DTOs/InventarioGestionDtos.cs`** — el DTO de respuesta del GET
  (`InventarioGestionVentanaFechaIngresoDto`): extremos + encaset + días + texto de ayuda ya armado.

### Frontend
- **`gestion-inventario/funciones/ventana-fecha-movimiento.funcion.ts`** — se le agregan funciones
  **puras**: `ventanaFechaIngreso(...)` (extremos con la ventana del backend aplicada) y
  `hintFechaIngreso(...)` (el texto del hint). Las existentes **no se tocan**: las siguen usando las
  tres puertas con regla dura.
- **`gestion-inventario/services/…`** — dos métodos HTTP nuevos + la interfaz de respuesta.
- **`pages/gestion-inventario-page`** — trae la ventana cuando la ubicación está completa; usa los
  extremos en el `[min]` del modal de fecha del ingreso; el hint pasa a ser dinámico; la guarda
  `validarVentanaFecha` se reemplaza por la variante de ingreso en las **dos** llamadas del ingreso
  (`:1200` alta y `:1656` modal) — **traslado y stock quedan igual**.
- **`pages/inventario-historial-page`** — lo mismo en el modal «Nueva fecha» (`:332`), que edita un
  ingreso.

## 5. Reglas de negocio (las del backend, sin inventar ninguna)

1. La ventana base **no cambia**: del día 1 del mes en curso hasta hoy.
2. La excepción sólo aplica a las **dos puertas de ingreso**.
3. El **futuro nunca** se admite, por ninguna de las dos vías.
4. Tope duro: **30 días** hacia atrás desde hoy.
5. La fecha tiene que caer en `[encaset − dias_alimento_previo_encaset, encaset]` de un
   encasetamiento **real** de ESE galpón.
6. Sin galpón o sin encaset ⇒ no hay excepción: manda la regla del mes en curso.

## 6. Casos de prueba

### Backend — `ExtremosVentanaIngreso` (xUnit, cálculo puro)
- **T1** Sin encaset ⇒ `min` = día 1 del mes, `max` = hoy (idéntico a hoy).
- **T2** Encaset futuro con la ventana entrando al mes anterior ⇒ `min` = `encaset − dias`.
- **T3** `encaset − dias` **posterior** al día 1 del mes ⇒ `min` NO se mueve (nunca se achica).
- **T4** Encaset tan viejo que su intervalo cae fuera de `[hoy−30, hoy]` ⇒ `min` = día 1 del mes.
- **T5** `encaset − dias` anterior a `hoy−30` ⇒ `min` se topa en `hoy−30`, nunca antes.
- **T6** `dias` negativo ⇒ se normaliza a 0 (mismo criterio que el resto de la clase).
- **T7** `max` es **siempre** hoy, haya o no encaset (el futuro no se abre nunca).

### Integración / smoke
- **T8** `GET ventana-fecha-ingreso` de un galpón con encaset próximo ⇒ 200 con el encaset y el rango.
- **T9** El mismo GET sin galpón ⇒ 200 con `proximoEncaset = null` y los extremos clásicos.
- **T10** `POST /ingreso` con fecha del mes anterior **dentro** de la ventana ⇒ **200** (ya funciona;
  se verifica que el front ahora la deja tipear).
- **T11** `POST /ingreso` con fecha del mes anterior **fuera** de la ventana ⇒ **400** con el mensaje
  que nombra el encaset.

### Front
- **T12** `yarn build` sin errores nuevos.
- **T13** Las tres puertas con regla dura (traslado, fecha de traslado, stock) siguen cortando en el
  día 1 del mes — su `[min]` y su hint no cambian.

## 7. Fuera de alcance

- No se toca `fn_seguimiento_diario_engorde` ni ninguna función SQL ⇒ **no aplica el gate multipaís**.
- No se toca la marca `para_proximo_ciclo` (§2.3b/§2.3c): sigue congelada esperando el rediseño.
- No se cambia `dias_alimento_previo_encaset` de ninguna empresa.
