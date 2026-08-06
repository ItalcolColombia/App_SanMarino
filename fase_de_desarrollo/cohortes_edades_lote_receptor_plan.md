# Plan — Trazabilidad de cohortes: cuántas aves, de dónde y con qué edad en el lote receptor

**Fecha:** 2026-08-06
**Pedido del usuario:** validar (y cerrar) que en **las dos líneas** —postura y pollo engorde— el traslado
mantenga la auditoría de **cuántas aves** entraron al lote receptor, **de qué lote y de qué ubicación** vienen y
**con qué edad**, dado que un mismo lote puede tener aves de varios orígenes y ubicaciones distintas.

---

## 0. Auditoría previa (hecha sobre código + dump de producción restaurado)

El mecanismo correcto **ya existe en postura**: `lote_aves_cohortes` guarda por cada grupo recibido el lote de
origen, la fecha de ingreso y la **fecha de encasetamiento del lote ORIGEN** — la edad se cuenta siempre desde ahí.
Hay UI (`app-edades-lote`, tabs de Levante y Producción) y endpoint (`GET /api/Traslados/cohortes/{loteId}`).

**El problema es la cobertura:**

| Camino | ¿Registra cohorte? | Evidencia |
|---|---|---|
| Postura — traslado desde seguimiento (`TSD-*`) | ✅ | `TrasladoAvesDesdeSegService.Cohortes.cs:53` |
| Postura — carga masiva (hoja Movimientos Aves, Ingreso) | ✅ | `MigracionService.MovimientosAves.cs:614` |
| **Postura — módulo «Movimientos de Aves» (`MOV-*`)** | ❌ | `ProcesarMovimientoAsync` acredita el destino y escribe la fila diaria, pero el único uso de `LoteAvesCohortes` en ese service es el **borrado** (`Crud.cs:112`) |
| **Engorde — traslado entre lotes** | ❌ | **No existe la tabla ni el concepto**: la edad sale de `lote_ave_engorde.fecha_encaset`, una sola fecha por lote |

En el dump de producción restaurado: **3 traslados `MOV-*` con lote destino y 0 cohortes en toda la base**.

**Hallazgos secundarios (verificados):**

1. `CrearRegistroEntradaEnLoteDestinoAsync` (camino `MOV-*`) calcula la semana con
   `SemanaDesdeEncaset(fecha, loteDestino.FechaEncaset)` ⇒ asume que las aves entrantes **adoptan la edad del
   receptor**. Es justo lo contrario de lo que hace la cohorte.
2. El traslado desde seguimiento deja **`movimiento_aves.lote_destino_id` NULL** (solo guarda la granja destino).
   ⚠️ Además de perder el dato, esto **abre un hueco de duplicación**: la idempotencia de la carga masiva
   (`MigracionService.MovimientosAves.cs:315-336`) busca movimientos ya aplicados con
   `LoteOrigenId == loteId || LoteDestinoId == loteId` y clasifica como **Ingreso** los que matchean por destino.
   Con el campo en NULL, un traslado hecho por pantalla es **invisible** al reimportar el lote receptor y la carga
   masiva lo volvería a aplicar. Poblarlo cierra ese hueco.
3. **Engorde — baseline de la auditoría de ventas**: `MaxVendiblePorSexo` parte del registro `Inicio` de
   `historial_lote_pollo_engorde` (`Auditoria.cs:60-71`), y ese registro **solo se escribe al crear el lote**
   (únicos escritores: `LoteAveEngordeService:342` y `LoteReproductoraAveEngordeService:226`). Un lote que recibe
   aves por traslado **no sube su techo** ⇒ vender esas aves aparecería como **sobreventa** aunque existan en el
   maestro (`CompleteAsync` sí incrementa `hembras_l/machos_l`).
4. La fila «Aves propias del lote» de la UI de edades devuelve `hembras: null, machos: null`
   (`construir-filas-edades-lote.funcion.ts:23-25`) ⇒ no se puede cuadrar *cohortes + propias = saldo del lote*.

**Límite honesto del modelo (no se resuelve acá):** la mortalidad y la selección se registran por lote, no por
cohorte. Lo auditable es **cuántas entraron, de dónde y con qué edad**; «cuántas aves de cada edad quedan HOY»
exigiría una política de imputación de bajas (proporcional / FIFO / manual) que es decisión de negocio.

**Decisiones del usuario:** cerrar los tres frentes, y **congelar granja/núcleo/galpón de origen en la cohorte**
(sobrevive a que el lote origen se reubique o se elimine).

---

## 1. Enfoque

Una sola idea, aplicada igual en las dos líneas: **cada ingreso de aves por traslado deja una fila de cohorte**
con la ubicación y la edad congeladas en el momento del traslado, y esa fila es la **única fuente** de «qué aves
ajenas tiene este lote».

Consecuencia de diseño para el punto 3 de la auditoría: en vez de escribir un registro nuevo en
`historial_lote_pollo_engorde` (tabla **sin soft-delete**, que obligaría a compensar con filas negativas al
revertir), el baseline de la auditoría de ventas **lee las cohortes**. Así hay un solo dato, y la reversión de un
traslado (que ya da de baja la cohorte) corrige el techo sin lógica extra. Es la regla «una sola fórmula por
número» de `CLAUDE.md`.

---

## 2. Archivos

### 2.1 Backend — modelo

| Archivo | Cambio |
|---|---|
| `Domain/Entities/LoteAvesCohorte.cs` | `+ GranjaOrigenId (int?)`, `NucleoOrigenId (string?)`, `GalponOrigenId (string?)` |
| `Domain/Entities/LoteEngordeAvesCohorte.cs` | **Nueva**: cohorte del lote de engorde receptor (mismos campos + `CantidadMixtas`) |
| `Persistence/Configurations/LoteAvesCohorteConfiguration.cs` | Columnas nuevas |
| `Persistence/Configurations/LoteEngordeAvesCohorteConfiguration.cs` | **Nueva** → tabla `lote_engorde_aves_cohortes` |
| `Persistence/ZooSanMarinoContext.cs` | `DbSet<LoteEngordeAvesCohorte>` |
| `Migrations/…AddCohortesEngordeYUbicacionOrigen.cs` | **Idempotente**: `CREATE TABLE IF NOT EXISTS` + `ADD COLUMN IF NOT EXISTS` |

### 2.2 Backend — escritura de cohortes

| Archivo | Cambio |
|---|---|
| `MovimientoPolloEngordeService.Cohortes.cs` (**nuevo partial**) | Registrar la cohorte al COMPLETAR un movimiento con lote destino; darla de baja al cancelar/eliminar |
| `MovimientoPolloEngordeService.Crud.cs` | Llamadas desde `CompleteAsync` / `CancelAsync` / `EliminarAsync` |
| `MovimientoAvesService.Cohortes.cs` (**nuevo partial**) | Registrar la cohorte en el camino `MOV-*` al procesar un traslado con destino |
| `MovimientoAvesService.Procesamiento.cs` | Llamada tras acreditar el destino |
| `TrasladoAvesDesdeSegService.Traslado.cs` | `LoteDestinoId = destino.LoteBaseId` en la auditoría |
| `TrasladoAvesDesdeSegService.Cohortes.cs` | Congelar granja/núcleo/galpón de origen |
| `MigracionService.MovimientosAves.cs` | Ídem en la cohorte de la carga masiva |

### 2.3 Backend — lectura

| Archivo | Cambio |
|---|---|
| `Application/Calculos/LoteCohortesCalculos.cs` | `BaselineConCohortes(...)`: techo de venta = inicio + recibidas (puro) |
| `MovimientoPolloEngordeService.Auditoria.cs` | Sumar las cohortes al baseline vía el cálculo puro |
| `DTOs/Traslados/LoteCohortesDto.cs` | `+ UbicacionOrigen` en la cohorte; `+ HembrasPropias/MachosPropias` en el lote |
| `TrasladoAvesDesdeSegService.Cohortes.cs` | Devolver ubicación de origen y las cantidades propias (saldo − recibidas, con clamp) |
| `MovimientoPolloEngordeService.Cohortes.cs` | `GetCohortesLoteEngordeAsync` + endpoint en `MovimientoPolloEngordeController` |

### 2.4 Frontend

| Archivo | Cambio |
|---|---|
| `traslados-aves/models/cohorte-lote.model.ts` + `funciones/construir-filas-edades-lote.funcion.ts` | Columna de ubicación de origen; fila «propias» con cantidades |
| `traslados-aves/components/edades-lote/*` | Render de lo anterior (el componente se reutiliza tal cual en engorde) |
| `movimientos-pollo-engorde/…-list` | Panel «Edades en el lote» del lote seleccionado |

---

## 3. Reglas

1. **La cohorte se escribe cuando las aves ENTRAN de verdad**: en engorde al *completar* (que es cuando
   `CompleteAsync` acredita el maestro), en postura al *procesar*. Nunca al crear un movimiento «Pendiente».
2. **La edad se hereda del lote ORIGEN** (`fecha_encaset_cohorte`); si el origen no tiene encasetamiento, **no se
   crea la cohorte y el traslado continúa** (regla ya vigente en postura: la edad heredada es informativa y jamás
   debe tumbar un traslado).
3. **La ubicación de origen se congela** (granja/núcleo/galpón del lote origen al momento del traslado).
4. **Revertir un traslado da de baja la cohorte** (soft-delete), nunca la borra: es el mismo invariante de
   `CLAUDE.md` («el histórico se anula, nunca se abandona»).
5. **El techo de venta de engorde sube con las cohortes recibidas** y baja solo cuando la cohorte se anula.
6. Sin lote destino (venta / retiro / ajuste) **no se crea cohorte** y todo queda byte a byte como antes.

---

## 4. Casos de prueba

### xUnit (`tests/ZooSanMarino.Application.Tests/`)
- `LoteCohortesCalculosTests` (ampliar): `BaselineConCohortes` suma las recibidas; sin cohortes devuelve el inicio
  **idéntico** (retrocompatible); cohortes anuladas no suman; clamp a 0.
- Edad de la cohorte: se cuenta desde el encaset del ORIGEN, no del receptor ni de la fecha de ingreso.

### Smoke
- **Engorde**: trasladar de un lote con encaset A a un lote con encaset B ⇒ el receptor muestra dos edades; vender
  las aves recibidas **no** debe marcar sobreventa en la auditoría; eliminar el traslado devuelve techo y cohorte.
- **Postura `MOV-*`**: traslado desde «Movimientos de Aves» ⇒ ahora aparece la cohorte con la edad del origen.
- **Postura TSD**: `lote_destino_id` poblado y la carga masiva detecta el Ingreso como ya aplicado.
- **Regresión**: una venta normal (sin destino) no crea cohorte y la auditoría no cambia.

### Build
`dotnet build` 0/0 · `dotnet test` verde · `yarn build` sin errores nuevos.

---

## 5. Hallazgo durante el smoke: el camino `MOV-*` está roto de antes

Al validar la cohorte del módulo «Movimientos de Aves» apareció un bug **preexistente y ajeno a este
cambio** que impide que ese camino haga absolutamente nada:

```
Npgsql.PostgresException 42883: operator does not exist: character varying = integer
  SELECT ... FROM inventario_aves AS i
  WHERE i.lote_id = @loteId AND i.granja_id = @granjaId ...
```

`inventario_aves.lote_id` es **`character varying`** en la base (dump de producción) mientras que
`InventarioAves.LoteId` es **`int`**.

**Por qué es grave:** `ProcesarMovimientoAsync` ejecuta `ActualizarInventarioPorMovimientoAsync` como
primer paso después de marcar el movimiento. La secuencia real es:

1. `movimiento.Procesar()` ⇒ `Estado = "Completado"`
2. `SaveChangesAsync()` ⇒ **el estado ya quedó persistido**
3. `ActualizarInventarioPorMovimientoAsync` ⇒ 💥 42883
4. `CreateAsync` atrapa la excepción y sólo hace `LogError`

Resultado: **el movimiento se muestra «Completado» sin haber movido una sola ave.** Verificado en el
smoke (movimiento MOV-20260806-000019, lote 116 → 115): maestros `lote_postura_levante` intactos, sin
fila en `seguimiento_diario_levante`, sin fila en el histórico unificado, sin tocar `inventario_aves`.
Es consistente con que los únicos 3 traslados `MOV-*` del dump de producción sigan en estado
`Pendiente` y con 0 cohortes.

**Qué falta para cerrarlo:** por la regla «el código manda» de `CLAUDE.md`, la corrección es alinear la
columna a `integer` (migración idempotente con `USING lote_id::integer`, previa verificación de que
todos los valores sean numéricos). Es **DDL sobre una tabla de producción** ⇒ requiere OK explícito
antes de ejecutarse. La cohorte del camino `MOV-*` ya está implementada y compilada: empezará a
registrarse en cuanto la vía se desbloquee.
