# Permiso de fecha retroactiva + ventana base de 15 días

**Fecha:** 2026-08-20 · **Pedido del usuario:** un permiso que habilite el campo de fecha de los
registros para poder cargar fechas anteriores, porque hoy la regla es «solo el mes en curso»; y
además sumar **15 días hacia atrás** a la regla base, porque el día 1 de cada mes nadie puede
registrar lo que llegó el día anterior.

---

## 1. Estado real medido (antes de tocar nada)

**La regla del mes en curso existe en UN solo módulo.** Medido con `grep` sobre `backend/src` y
`frontend/src`:

| Dónde | Qué hay hoy |
|---|---|
| `VentanaFechaMovimientoInventarioCalculos` (Application/Calculos) | La única implementación de la ventana: `[1 del mes, hoy]`, día operativo UTC−5, + excepción **D4** (alimento previo al encaset) |
| `InventarioGestionController` | Las **5 puertas** que la aplican: `POST /ingreso`, `POST /traslado`, `PUT /ingresos/{id}/fecha`, `PUT /traslados/{gid}/fecha`, `PUT /stock/{id}` |
| `frontend/.../gestion-inventario/funciones/ventana-fecha-movimiento.funcion.ts` | Espejo de UX (acota el datepicker) |
| **Todo el resto de la app** | **Libre**: ningún otro campo de fecha valida nada (ni pasado ni futuro) |

⇒ Extender la ventana a otros módulos **no es «agregar el permiso»: es agregar una restricción
nueva**. El alcance se decidió con el usuario (2026-08-20) y quedó así:

**DENTRO** (elegido): movimientos y traslados (9 pantallas) + gestión de inventario y gastos.
**FUERA** (elegido): seguimientos diarios (5 pantallas) y fechas fundacionales de lote
(`fechaEncaset`, `fechaAlistamiento`, `fechaInicio` de producción — Ecuador **programa
encasetamientos futuros** y la carga de históricos usa meses atrás).
**FUERA por instrucción explícita:** tickets / ItalJira, Implementación y Vacunación — ahí las fechas
son libres y no se toca nada.
**FUERA por naturaleza del dato:** `catalogo-alimentos → fecha_vencimiento` (es una fecha **futura**;
la ventana la rompería) y todos los filtros de reportes (`fechaDesde`/`fechaHasta`, `calcsDesde`,
`filtro*`): un filtro no es un registro.

---

## 2. Reglas de negocio

**R1 — Ventana base (aplica a todos, sin permiso):**
`min = MIN(día 1 del mes en curso, hoy − 15 días)` · `max = hoy` (día operativo **UTC−5**).
Es una **ampliación estricta** de la regla vigente: del día 16 en adelante manda el 1 del mes (más
ancho); del 1 al 15 manda `hoy − 15`, que es justo el caso que el usuario reportó.

**R2 — El permiso `registros.fecha_retroactiva`** abre **todo el pasado, sin tope**. Decisión del
usuario: el **futuro sigue cerrado para todos**, con o sin permiso — una fecha posterior a hoy es un
error de tipeo, no un caso de negocio.

**R3 — La guarda va en el CONTROLLER, nunca en el service.** Invariante ya aprendido a los golpes
(`ventana-fecha-inventario-va-en-el-controller`): los services los comparten la carga masiva, las
devoluciones de alimento al editar/borrar un seguimiento y las anulaciones de gastos, que fechan
histórico **a propósito**. El controller es la única frontera «esto lo tipeó una persona».

**R4 — Una sola fórmula por número.** La ventana base vive en UN cálculo puro nuevo
(`VentanaFechaRegistroCalculos`) y `VentanaFechaMovimientoInventarioCalculos` **delega** en él,
conservando D4 encima. No se duplica la aritmética.

**R5 — Puertas de creación Y de edición.** Sin las de edición la regla se esquiva cargando con fecha
de hoy y cambiándola después (lección de las 5 puertas de inventario).

**R6 — Fail-closed en el permiso, fail-open en la UX.** El backend decide; si el front no puede
resolver el permiso, ofrece la ventana **restringida** (nunca bloquea de más de lo que el back
rechaza: el 400 del controller es el que manda).

---

## 3. Backend — archivos

### 3.1 Nuevo cálculo puro
`backend/src/ZooSanMarino.Application/Calculos/VentanaFechaRegistroCalculos.cs`
- `const string PermisoFechaRetroactiva = "registros.fecha_retroactiva"`
- `const int DiasRetroactividadBase = 15`
- `DiaOperativo(DateTimeOffset)` — UTC−5 (mismo criterio y motivo que hoy)
- `PrimerDiaAdmitido(DateTime hoy)` → `MIN(1 del mes, hoy − 15)`
- `EsFechaPermitida(DateTime? fecha, DateTime hoy, bool puedeRetroactivar)`
- `MensajeFueraDeVentana(DateTime hoy, bool puedeRetroactivar)` — con permiso, el rechazo solo puede
  ser por fecha futura y el texto lo dice
- `ExtremosVentana(DateTime hoy, bool puedeRetroactivar)` → `(DateTime? Min, DateTime Max)`;
  `Min = null` con permiso (sin piso)
- `TienePermisoRetroactivo(IEnumerable<string>? permisos)` — comparación `OrdinalIgnoreCase`, igual que
  el resto de los chequeos de permiso del repo

### 3.2 Guarda compartida de la capa API
`backend/src/ZooSanMarino.API/Infrastructure/VentanaFechaRegistroGuard.cs` — extensión de
`ControllerBase` que lee el permiso del claim `permission` (igual que `HttpCurrentUser`), resuelve el
día operativo y devuelve el `BadRequest` ya armado o `null`:

```csharp
if (this.ValidarVentanaFechaRegistro(dto.FechaMovimiento) is { } fuera) return fuera;
```

No toca constructores (usa `ControllerBase.User`), así que ningún controller cambia su DI.

### 3.3 `VentanaFechaMovimientoInventarioCalculos` (existente)
- `PrimerDiaAdmitido` / `EsFechaPermitida` / `MensajeFueraDeVentana` pasan a **delegar** en el cálculo
  nuevo; se les agrega `bool puedeRetroactivar = false` como parámetro final **opcional** (los tests
  existentes siguen compilando).
- D4 queda intacto **encima** de la base ampliada. `ExtremosVentanaIngreso` devuelve `DateTime? Min`.

### 3.4 Puertas a guardar (17 endpoints)

| Controller | Endpoint | Campo |
|---|---|---|
| `InventarioGestion` | `POST /ingreso` (D4) | `FechaIngreso` |
| `InventarioGestion` | `POST /traslado` | `FechaMovimiento` |
| `InventarioGestion` | `PUT /ingresos/{id}/fecha` (D4) | `FechaMovimiento` |
| `InventarioGestion` | `PUT /traslados/{gid}/fecha` | `FechaMovimiento` |
| `InventarioGestion` | `PUT /stock/{id}` | `FechaIngreso` |
| `InventarioGastos` | `POST /` | `Fecha` |
| `FarmInventoryMovements` | `POST in` / `out` / `transfer` / `adjust` | `FechaMovimiento` (`DateTimeOffset?`) |
| `MovimientoAves` | `POST /` · `PUT /{id}` | `FechaMovimiento` |
| `Traslados` | `POST /aves` | `FechaTraslado` |
| `Traslados` | `POST /huevos` · `PUT /huevos/{id}` | `FechaTraslado` |
| `MovimientoPolloEngorde` | `POST /` · `PUT /{id}` · `POST /venta-granja-despacho` | `FechaMovimiento` |
| `MovimientoPolloEngordePanama` | `POST /venta-despacho` | `FechaMovimiento` |

**No se tocan, con motivo:**
- `POST /InventarioGestion/consumo` — el front nunca lo llama (entra por seguimiento diario y carga
  masiva). Igual que en la entrega de agosto.
- `POST /MovimientoAves/ejecutar-venta`, `ejecutar-traslado`, `ejecutar-traslado-cierre-levante`
  (tienen `Fecha`) — los dispara el **seguimiento diario de levante**, que quedó fuera de alcance.
- `POST /Traslados/aves-desde-seguimiento` (`FechaSeguimiento`) — la fecha **no es libre**: el modal la
  inicializa con `origen.fechaSeguimiento` y tiene que coincidir con un seguimiento existente.
  Guardarla sería aplicarle la ventana a los seguimientos por la puerta de atrás. Se documenta.
- `POST /transito/recepcion`, `POST /transito/rechazo`, `POST /{id}/procesar`, `traslado-rapido`,
  `completar`, `registrar-peso` — verificado: **no llevan fecha tipeada**.

### 3.5 Migración (seed del permiso)
`SeedPermisoFechaRetroactivaRegistros` — data-only, idempotente, Designer clonado (sin tocar
`ModelSnapshot`):
1. `INSERT INTO permissions (key, description) ... WHERE NOT EXISTS` — key
   `registros.fecha_retroactiva`.
2. `INSERT INTO company_permissions (company_id, permission_id, is_enabled) SELECT c.id, p.id, true
   FROM companies c CROSS JOIN permissions p WHERE p.key = '...' AND NOT EXISTS (...)` — **sin esto el
   permiso no es asignable ni sobrevive al login**: `company_permissions` es fail-closed
   (`permisos-por-empresa-company-permissions`).
3. `INSERT INTO role_permissions (role_id, permission_id) SELECT 1, p.id ...` — al rol Admin, mismo
   patrón que `AddSincronizacionPanamaModule`.
4. `Down()` revierte los tres en orden inverso.

No lleva `.sql` espejo: el gate `verificar-sql-llega-por-migracion.js` cubre `fn_*` / `vw_*`, y un seed
de catálogo va por migración (CLAUDE.md §🗄️).

---

## 4. Frontend — archivos

### 4.1 Función compartida (nueva, canónica)
`frontend/src/app/shared/utils/fecha/ventana-fecha-registro.funcion.ts` — espejo del cálculo nuevo:
`aYmd`, `ventanaFechaRegistro(hoy, puedeRetroactivar)`, `esFechaRegistroPermitida(...)`,
`mensajeFechaRegistroFueraDeVentana(...)`, `hintVentanaFechaRegistro(...)`.
El permiso se lee con `UserPermissionService.has('registros.fecha_retroactiva')` (ya existe, síncrono
y reactivo). `ventana-fecha-movimiento.funcion.ts` de gestión de inventario **delega** en ésta.

### 4.2 Pantallas (11 formularios, 9 grupos)
`[attr.min]` + `[max]` + validación con mensaje, en:
1. `inventario/components/movimiento-alimento-form` — `fechaMovimiento`
2. `movimientos-aves/components/modal-movimiento-aves` — `fechaMovimiento`
3. `movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde` — `fechaMovimiento`
4. `movimientos-pollo-engorde/components/modal-venta-panama` — `fechaMovimiento`
5. `traslados-aves/pages/inventario-dashboard` — `fechaMovimiento` + `fechaTraslado`
6. `traslados-aves/pages/traslado-aves-huevos` — `fechaTraslado`
7. `traslados-huevos/components/modal-traslado-huevos` — `fechaTraslado`
8. `traslados-huevos/pages/traslado-huevos-form` — `fechaTraslado`
9. `gastos-inventario/pages/gastos-inventario-page` — `formFecha`
10. `gestion-inventario` (2 pantallas) — ya tienen la ventana: se les suma el permiso y el piso nuevo

`[attr.min]` y no `[min]`: con permiso el mínimo es `null` y el atributo tiene que **desaparecer**.
`max` es siempre hoy, con permiso o sin él.

### 4.3 Contrato del GET de ventana (D4)
`InventarioGestionVentanaFechaIngresoDto.Min` pasa a `DateOnly?` y la interfaz TS a `min: string |
null`; el backend arma el `ayuda` sabiendo si el usuario tiene el permiso. Es aditivo salvo la
nulabilidad de `min`, que el front ya tiene que manejar para el caso con permiso.

---

## 5. Tests

**Nuevos** — `backend/tests/ZooSanMarino.Application.Tests/VentanaFechaRegistroCalculosTests.cs`:
- Día 20: el piso es el 1 del mes (el mes es más ancho que 15 días).
- Día 1: el piso es `hoy − 15` ⇒ **el día anterior se acepta** (el caso reportado).
- Día 16: los dos coinciden en el 1 del mes.
- Futuro rechazado **con y sin** permiso.
- Con permiso: hace 2 años se acepta.
- `null` sigue siendo válido (significa «sin fecha explícita»; el service pone la hora actual).
- Día operativo UTC−5: 03:00 UTC del 1 de septiembre sigue siendo 31 de agosto.
- Permiso resuelto `OrdinalIgnoreCase` y con lista nula/vacía.

**Actualizados** — los 3 archivos de `VentanaFechaMovimientoInventario*Tests`: las expectativas que
asumían el piso «1 del mes» duro cambian al piso ampliado. Se agrega un caso de equivalencia:
**con el flag de permiso apagado y a mitad de mes, el resultado es idéntico al de hoy** (byte a byte,
mensajes incluidos).

**Gate multipaís:** no aplica — no se toca `fn_seguimiento_diario_engorde`, `fn_cuadre_alimento_engorde`
ni ningún `*SaldoAlimento*`. La ventana solo **valida** fechas de entrada; ninguna aritmética de saldo
cambia.

---

## 6. Validación

1. `cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test`.
2. `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget preexistente).
3. Smoke por HTTP con backend local en `:5002` (arrancado al final, apagado al terminar, puerto libre
   verificado — §🔌 de CLAUDE.md):
   - sin el permiso: fecha del mes anterior a más de 15 días ⇒ **400** con el mensaje nuevo;
   - sin el permiso: `hoy − 15` ⇒ **OK** (el caso del día 1);
   - con el permiso: fecha de hace 6 meses ⇒ **OK**;
   - con el permiso: `hoy + 1` ⇒ **400** (el futuro no lo abre nadie);
   - las 5 puertas de gestión de inventario siguen respondiendo igual con el permiso apagado.
4. `node backend/scripts/verificar-sql-llega-por-migracion.js` (los 4 gates del CI en verde).

## 7. Riesgos

- **Restricción nueva donde antes no había ninguna** (movimientos y traslados). Mitigación: la ventana
  es más ancha que la única que ya existía, y el permiso es la válvula. Aun así, es el cambio de
  comportamiento visible de esta entrega y hay que anunciarlo a operación.
- **`company_permissions` es fail-closed**: si el seed del paso 2 no corre, el permiso no se puede ni
  asignar. Va en la misma migración y es idempotente.
- **El menú no interviene**: el permiso no habilita pantallas, solo destraba el campo. No se toca
  `company_menus` ni `role_menus`.
