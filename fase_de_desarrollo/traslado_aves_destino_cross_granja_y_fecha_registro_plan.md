# Plan — Traslado de aves: destino cross-granja/galpón en Engorde + fecha de registro visible

**Fecha:** 2026-08-05
**Pedido del usuario:** al trasladar aves —tanto en **pollo engorde** como en **postura (levante o producción)**— debe
poder elegirse **otra granja / otro galpón** como destino, y deben existir **dos fechas distintas**: la **fecha del
traslado** (la que el usuario edita en la web, es el hecho del negocio) y la **fecha de creación del registro**
(`created_at`, cuándo se cargó en el sistema).

---

## 0. Auditoría del estado actual (el código manda)

Antes de escribir nada se leyó el código vivo. Resultado:

| Camino | Destino otra granja/galpón | Fecha del traslado editable | `created_at` en BD | `created_at` visible en UI |
|---|---|---|---|---|
| **Postura — Movimiento de Aves** (`movimientos-aves`) | ✅ cascada `app-filtro-select [paraDestino]` Granja→Núcleo→Galpón→Lote | ✅ `fechaMovimiento` | ✅ `CreatedAt = DateTime.UtcNow` (`MovimientoAvesService.Crud.cs:57`) | ❌ |
| **Postura — Traslado desde seguimiento** (`modal-traslado-aves-seguimiento`) | ✅ cascada propia con `paraDestino=true` en núcleos/galpones/lotes | ✅ `fechaEvento` (REQ-009a, obligatoria) | ✅ default de `AuditableEntity`; `fechaUtc = DateTime.UtcNow` | ❌ |
| **Engorde — Movimiento Pollo Engorde** (`movimientos-pollo-engorde`) | ❌ `<select>` plano `destinoOpciones` alimentado por `buildLotesOpciones()`, que filtra **por la granja/núcleo/galpón ya seleccionados en la pantalla** y además solo por lotes con ventas registradas (`getVentaLotesAveEngorde()`) | ✅ `fechaMovimiento` | ✅ `CreatedAt = DateTime.UtcNow` (`MovimientoPolloEngordeService.Crud.cs:82`) | ❌ |

**Conclusiones que definen el alcance:**

1. **La separación de fechas YA existe en el modelo de datos.** `MovimientoAves` y `MovimientoPolloEngorde` heredan
   de `AuditableEntity` (`CreatedAt`), guardan aparte `FechaMovimiento`, y **ambos DTOs ya exponen `CreatedAt`**
   (`MovimientoAvesDto.cs:39`, `MovimientoPolloEngordeDto.cs:33`). Lo que falta es **mostrarlo**: hoy ninguna
   pantalla lo pinta, por eso el usuario percibe que "solo hay una fecha". ⇒ **trabajo de front, sin migración.**
2. **El backend de engorde YA acepta destino cross-granja**: `CreateMovimientoPolloEngordeDto` tiene
   `GranjaDestinoId` / `NucleoDestinoId` / `GalponDestinoId` y `CreateAsync` los persiste
   (`Crud.cs:45-47`). El front **nunca los envía** (`mapear-movimiento-dto.funcion.ts` solo manda el lote).
   ⇒ **trabajo de front + un `paraDestino` en el catálogo de lotes, sin migración.**
3. **En engorde el traslado SÍ mueve aves**: `CompleteAsync` descuenta del lote origen y suma al destino para
   cualquier tipo (`Crud.cs:736-776`), y `RevertirEfectoCompletadoEnLotes` es su inverso exacto. No hace falta
   tocar aritmética de saldos: abrir el destino a otra granja no cambia ninguna fórmula.
4. ⚠️ **Hallazgo durante el smoke — en engorde NO existe hoy ninguna entrada de UI para crear un traslado.**
   `create()` de la lista fija `ventaPorGranjaMode = true` sin excepción (*«Siempre crear venta por granja
   (despacho)»*), así que el único alta posible es el despacho de venta multi-lote. La cascada de destino sería
   **inalcanzable** sin agregar el punto de entrada ⇒ entra en el alcance: botón **«Nuevo traslado»** +
   selección del lote ORIGEN dentro del modal.

**Decisiones tomadas con el usuario:**
- Alcance del destino en engorde: **todas las granjas de la empresa (patrón postura)**.
- `created_at` visible en: **columna de la tabla + detalle del movimiento + Excel exportado**.

---

## 1. Enfoque arquitectónico

**Refactor ≠ cambio de comportamiento.** Ningún número cambia: no se toca `AvesDisponiblesEngordeCalculos`,
ni `CompleteAsync`, ni las patas de traslado de postura. Se agrega **una vía de selección** que antes no existía
y se **expone un dato que ya se guardaba**.

**Patrón a copiar (canónico en el repo):** `modal-traslado-aves-seguimiento.component.ts` (postura). Su cascada de
destino resuelve granjas con `FarmService.getForTrasladoSeguimiento()` (todas las de la empresa activa + país) y
núcleos/galpones/lotes con `paraDestino=true` (omite el alcance granular núcleo/galpón del usuario, conserva la
restricción por granjas asignadas para no-admin). El engorde replica **exactamente** ese contrato.

**Regla de simetría en backend:** hoy existe `RellenarOrigenDesdeLoteOrigenSiFaltaAsync`, que deriva
granja/núcleo/galpón del **lote origen** cuando el DTO no los trae. Se agrega su gemelo para el **destino**, de modo
que el movimiento quede completo aunque un cliente viejo mande solo el lote destino (retrocompatible por
construcción: si el DTO ya trae la granja destino, el helper no hace nada).

---

## 2. Archivos a crear / modificar

### 2.1 Backend — `paraDestino` en el catálogo de lotes engorde

| Archivo | Cambio |
|---|---|
| `Application/Interfaces/ILoteAveEngordeService.cs` | `GetAllAsync()` → `GetAllAsync(bool paraDestino = false)` (default preserva la firma existente para todos los llamadores) |
| `Infrastructure/Services/LoteAveEngordeService.cs` | Propagar la bandera a `AplicarScopeUbicacionAsync(q, paraDestino)`, igual que `LotePosturaLevanteService:212`. **La restricción por granjas asignadas NO se toca** |
| `API/Controllers/LoteAveEngordeController.cs` | `GET /api/LoteAveEngorde?paraDestino=true` (query opcional, default `false`) |

### 2.2 Backend — simetría de destino en el movimiento de engorde

| Archivo | Cambio |
|---|---|
| `…/MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.Crud.cs` | Nuevo `RellenarDestinoDesdeLoteDestinoSiFaltaAsync(movimiento, dto)` invocado tras el de origen; deriva `GranjaDestinoId`/`NucleoDestinoId`/`GalponDestinoId` del lote destino cuando faltan. Sale temprano si `GranjaDestinoId` ya vino |

### 2.3 Frontend — cascada de destino en el modal de engorde

| Archivo | Cambio |
|---|---|
| `movimientos-pollo-engorde/models/venta-granja.model.ts` | Tipo `DestinoTrasladoEngorde` (granja/núcleo/galpón/lote elegidos) |
| `movimientos-pollo-engorde/components/modal-movimiento-pollo-engorde/…component.ts` | Inyecta `FarmService`/`NucleoService`/`GalponService`/`LoteEngordeService`; controles `granjaDestinoId`, `nucleoDestinoId`, `galponDestinoId`; handlers de cascada; carga perezosa (solo al elegir tipo `Traslado`, no penaliza el flujo de venta que es el 95 % del uso). `changeDetection: Eager` ya está |
| `…component.html` | Bloque «Destino del traslado» con los 4 selects, visible solo si `!isTipoVenta`. Sustituye al `<select>` plano |
| `movimientos-pollo-engorde/funciones/mapear-movimiento-dto.funcion.ts` | `buildCreateDto` envía `granjaDestinoId`/`nucleoDestinoId`/`galponDestinoId` |
| `movimientos-pollo-engorde/services/movimiento-pollo-engorde.service.ts` | Campos de destino en `CreateMovimientoPolloEngordeDto` |
| `lote-engorde/services/lote-engorde.service.ts` | `getAll(paraDestino = false)` |
| `movimientos-pollo-engorde/funciones/filtrar-lotes-destino.funcion.ts` | **Nuevo**: `filtrarLotesDestinoEngorde` / `construirOpcionesLoteDestino` (función pura, espejo del `filtrarLotesDestino()` del modal de postura) |

### 2.3b Frontend — punto de entrada del traslado (hallazgo del smoke)

| Archivo | Cambio |
|---|---|
| `…-list.component.ts` | `crearTraslado()` (abre el modal con `trasladoMode`, sin `ventaPorGranjaMode`); `lotesTrasladoOrigen` (lotes ABIERTOS de la granja, sin exigir ventas registradas); `canOpenTraslado`; reset de `trasladoMode` en `closeModal()` |
| `…-list.component.html` | Botón **«Nuevo traslado»** + bindings `[trasladoMode]` y `[lotesOrigenTraslado]` |
| `…modal…component.ts/.html` | `@Input() trasladoMode` / `lotesOrigenTraslado`; select de **lote origen** con `onLoteOrigenTrasladoChange()` que pide la disponibilidad real (`aves-disponibles-lotes`, mismo número que valida el backend); tipo fijado a `Traslado` y bloqueado; destino obligatorio antes de confirmar |

### 2.4 Frontend — fecha de registro visible (los 3 lugares pedidos)

| Archivo | Cambio |
|---|---|
| `movimientos-pollo-engorde/pages/…-list.component.html` | Columna **«Registrado»** junto a «Fecha» |
| `movimientos-pollo-engorde/models/movimiento-tabla.model.ts` + `funciones/agrupar-despachos.funcion.ts` | Propagar `createdAt` a la fila agrupada (un despacho multi-lote comparte creación) |
| `movimientos-pollo-engorde/components/…modal…component.html` | `<dt>Registrado el</dt>` en el detalle; nota bajo el input de fecha en el formulario aclarando que la fecha del traslado es el hecho real y la de registro la pone el sistema |
| `movimientos-pollo-engorde/funciones/exportar-ventas-excel.funcion.ts` | Cabecera **«Registrado»** + celda `fechaCorta(m.createdAt)` |
| `movimientos-aves/pages/movimientos-aves-list.component.html` | «Registrado» bajo la fecha en la celda «N° / Fecha» (tabla de 6 columnas, no se agrega una 7ª) |
| `movimientos-aves/components/modal-movimiento-aves/…component.html` | Nota de registro junto al campo Fecha |

> `movimientos-aves` **no tiene exportación a Excel** hoy; el punto «Excel» del pedido aplica a engorde, que sí la
> tiene. No se crea una exportación nueva para postura (fuera del alcance pedido).

### 2.5 Sin cambios de BD

Cero migraciones: `created_at` ya existe en `movimiento_aves` y `movimiento_pollo_engorde`, y las columnas
`granja_destino_id` / `nucleo_destino_id` / `galpon_destino_id` ya existen en `movimiento_pollo_engorde`.

---

## 3. Reglas de negocio

1. **Fecha del traslado** = `fecha_movimiento`, editable por el usuario, es el **hecho** (día real en que se movieron
   las aves). **Fecha de registro** = `created_at`, la escribe el sistema (`DateTime.UtcNow`), **nunca editable**.
   Que difieran es normal y esperado (carga tardía) — la UI lo muestra sin alarmar.
2. **`created_at` es de solo lectura de punta a punta**: no viaja en ningún `Create*`/`Update*` DTO; el front solo
   lo pinta.
3. **Destino en engorde**: granjas = todas las de la empresa activa + país (`/api/Farm/traslado-seguimiento-diario`);
   núcleos/galpones/lotes con `paraDestino=true`. El lote origen se excluye del listado de destino.
4. **El destino sigue siendo opcional** en engorde (había movimientos sin destino: venta, retiro, ajuste). Elegir
   granja destino sin lote destino no rompe: el backend guarda la granja y deja el lote nulo.
5. **Fail-closed**: si el catálogo de destino falla, la lista queda vacía y el traslado no se puede confirmar —
   nunca se cae al comportamiento viejo de "cualquier lote de la granja filtrada".
6. **Gate B8 intacto**: `ValidarLotesNoLiquidadosAsync` ya cubre origen y destino; un lote liquidado sigue sin poder
   recibir aves aunque ahora sea alcanzable desde otra granja.

---

## 4. Casos de prueba

### Backend (xUnit — `tests/ZooSanMarino.Application.Tests/`)
- `MovimientoPolloEngordeDestinoCalculosTests` — regla **campo por campo** (lo explícito manda, lo que falta se
  deriva del lote destino):
  - Ubicación explícita completa ⇒ el helper **no** pisa nada.
  - Granja explícita sin núcleo/galpón (caso real de la cascada) ⇒ completa núcleo y galpón desde el lote.
  - Sin granja explícita ⇒ deriva las tres del lote; núcleo/galpón vacíos (`null`/`""`/espacios) también se completan.
  - Galpón explícito sin núcleo ⇒ solo completa el núcleo.
  - DTO sin destino alguno (venta / retiro / ajuste) ⇒ queda tal cual llegó (comportamiento previo byte a byte).

### Manual / smoke
- **Engorde**: traslado de lote de granja A → lote de granja B (otro núcleo y otro galpón); confirmar; verificar en BD
  `granja_destino_id`/`nucleo_destino_id`/`galpon_destino_id` poblados y el saldo movido en ambos maestros.
- **Engorde (regresión)**: una **venta** normal sigue sin pedir destino y guarda idéntico.
- **Fechas**: crear un movimiento con fecha de traslado retroactiva (p. ej. 3 días atrás) ⇒ la columna «Fecha» muestra
  la retroactiva y «Registrado» muestra hoy; el Excel trae ambas.
- **Postura (regresión)**: el traslado desde seguimiento sigue funcionando igual y ahora muestra la fecha de registro.

### Validación de build
- `cd backend && dotnet build` (0 errores, sin advertencias nuevas) + `dotnet test`.
- `cd frontend && yarn build` (0 errores; único warning aceptado: bundle budget preexistente).
