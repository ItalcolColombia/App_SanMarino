# Doble validación de los seguimientos diarios (levante, producción, pollo engorde, reproductora)

> Estado: **plan aprobado, en implementación** · Fecha: 14ago26 · Solicitante: moiesbbuga@gmail.com
> Tracker: [tracker_estado.md](../tracker_estado.md) — bloque `V1`

## 1. Problema

Hoy los cuatro módulos de seguimiento diario **descuentan alimento y aves en el mismo instante en que
se guarda el registro**. Como el registro se puede editar y borrar libremente después, la operación
corrige a mano por debajo (devoluciones, ajustes de inventario, recálculos de saldo) cada vez que se
equivoca en la captura. Además:

- **No hay obligación de cargar alimento.** Se guardan días con consumo 0 y sin tipo de alimento, que
  después rompen el cuadre y los indicadores (consumo/ave, conversión).
- **El motivo del rechazo no siempre se ve.** Levante y producción muestran el error en un toast que se
  va solo; en varios flujos el 400 del backend queda invisible y el usuario cree que guardó.
- **Fecha duplicada:** levante y producción sí la validan; **pollo engorde y reproductora no** — se
  pueden cargar dos registros del mismo día en el mismo lote.
- **El alimento de un galpón lo comparten dos lotes.** Como el descuento es inmediato y no hay reserva,
  dos lotes que consumen del mismo galpón se pisan el disponible entre ellos.

## 2. Decisiones tomadas con el usuario (14ago26)

| # | Decisión | Elegido |
|---|---|---|
| 1 | Alcance del descuento diferido | **Flag por empresa**, OFF por defecto (`requiere_validacion_seguimiento_diario`). Con el flag apagado el comportamiento actual queda **idéntico**. |
| 2 | Seguimiento reproductora (ya tiene `confirmado`) | **Unificar** al modelo nuevo: separar al guardar, descontar y cruzar al validar. |
| 3 | Registros vencidos sin validar | **Bloquean días nuevos** en ese lote, además del aviso visual (fila roja + estado *En retraso* + alarma + modal al entrar). |
| 4 | Registros históricos al desplegar | **Backfill `validado = true`** en todo lo existente (nada se revierte, ningún saldo cambia). |

## 3. Estado actual — auditoría por módulo

| Módulo | Tabla | Servicio | Duplicado fecha | Alimento obligatorio | Descuento alimento | Descuento aves | Validación |
|---|---|---|---|---|---|---|---|
| Levante | `seguimiento_diario_levante` | `SeguimientoLoteLevanteService` + `SeguimientoDiarioService` | ✅ (`SeguimientoDiarioService:288`) | ❌ | al crear | al crear (`SeguimientoDiarioService`) | ❌ |
| Producción | `seguimiento_diario_produccion` | `SeguimientoProduccionService` / `ProduccionService.Seguimiento` | ✅ (`:252`, `:456`) | ❌ | al crear | al crear | ❌ |
| Pollo engorde | `seguimiento_diario_aves_engorde` (+ `_ecuador`) | `SeguimientoAvesEngordeService` / `...EcuadorService` | ❌ | ❌ | al crear | al crear (`SincronizarBajasAvesAsync`) | ❌ |
| Reproductora | `seguimiento_diario_lote_reproductora_aves_engorde` | `SeguimientoDiarioLoteReproductoraService` | ❌ | ❌ | al crear | vía cruce | ⚠️ `confirmado` (solo gatea el cruce) |

Notas de la auditoría que condicionan el diseño:

- El descuento de aves de levante/producción está **centralizado** en `SeguimientoDiarioService`
  (Create/Update/Delete) — ver la nota `A7` en `SeguimientoLoteLevanteService.Crud.cs:210`. No hay que
  duplicarlo: se gatea ahí.
- El consumo de alimento tiene **tres caminos** según el país: Colombia (modelo B nivel granja, atómico
  con transacción), Ecuador/Panamá (modelo B tolerante) y «ninguno». Todos pasan por
  `MetadataEngordeCalculos.ParseMetadataItemsToKg(PorOrigen)` → ese es el punto único donde engancha la
  separación.
- El cruce reproductora → pollo engorde lo hace un **trigger de BD**
  (`trg_cruce_reproductora_engorde` → `fn_cruce_reproductora_a_engorde`), gateado por `confirmado = true`.
  Unificar reproductora **no** debe tocar esa función: `validado` se mapea sobre el `confirmado` que ya
  existe.
- Engorde tiene dos servicios (`...AvesEngordeService` y `...AvesEngordeEcuadorService`) con Crud
  paralelo: todo cambio va en los dos o queda cojo en Ecuador.

## 4. Arquitectura

### 4.1 Flag por empresa

`companies.requiere_validacion_seguimiento_diario boolean NOT NULL DEFAULT false`, nombrada por el
**comportamiento** (no por el tenant). Viaja en `CompanyDto` y hay que agregarla en **todas** las
proyecciones: `CompanyService.ToDto`, `CompanyService.Crud`, `CompanyResolver`, `CompanyPaisService`,
`CreateCompanyDto`, `UpdateCompanyDto`. El front la lee con
`core/services/company-config/active-company-config.service.ts` (caché 5 min, **fail-closed**).

La decisión es **lógica pura** en `Application/Calculos/ValidacionSeguimientoCalculos.cs`; los servicios
solo resuelven el flag y delegan.

### 4.2 Estado de validación por registro

Columnas nuevas en las 4 tablas que no lo tienen (reproductora **reutiliza** `confirmado`):

```
validado            boolean NOT NULL DEFAULT false
validado_at         timestamptz NULL
validado_por        varchar(64) NULL
```

Estados derivados (no se persisten — se calculan):

| Estado | Regla |
|---|---|
| `VALIDADO` | `validado = true` |
| `PENDIENTE` | `!validado` y `hoy <= fecha_seguimiento + 1 día` |
| `EN_RETRASO` | `!validado` y `hoy > fecha_seguimiento + 1 día` |

### 4.3 Separación (reserva) de alimento — tabla nueva

`seguimiento_reserva_alimento`

| Columna | Tipo | Nota |
|---|---|---|
| `id` | bigserial | |
| `company_id`, `pais_id`, `farm_id` | int | scoping |
| `nucleo_id`, `galpon_id` | varchar NULL | ubicación (igual que el stock) |
| `silo_id` | int NULL | empresas con `maneja_inventario_por_silo` |
| `item_inventario_ecuador_id` | int | |
| `origen_modulo` | varchar(24) | `LEVANTE` \| `PRODUCCION` \| `ENGORDE` \| `ENGORDE_EC` \| `REPRODUCTORA` |
| `origen_seguimiento_id` | bigint | |
| `lote_ref` | varchar(64) | trazabilidad legible |
| `fecha_seguimiento` | date | |
| `cantidad_kg` | numeric(18,3) | |
| `estado` | varchar(12) | `ACTIVA` \| `APLICADA` \| `LIBERADA` |
| `created_at`, `created_by_user_id`, `aplicada_at`, `liberada_at` | | |

Índice único parcial (idempotencia, mismo patrón que el stock):
`UNIQUE (origen_modulo, origen_seguimiento_id, item_inventario_ecuador_id, COALESCE(silo_id,0)) WHERE estado = 'ACTIVA'`.

**Disponible = `stock.quantity − Σ reservas ACTIVAS`** de esa ubicación/ítem. Ese es el número que ve el
formulario y contra el que se valida el tope — así el mismo galpón compartido por dos lotes ya no
muestra el disponible completo a los dos.

### 4.4 Separación (reserva) de aves — tabla nueva

`seguimiento_reserva_aves`

| Columna | Tipo | Nota |
|---|---|---|
| `id` | bigserial | |
| `company_id` | int | |
| `origen_modulo`, `origen_seguimiento_id` | varchar(24) / bigint | igual que arriba |
| `lote_ref_int` | int | clave numérica del lote de ese módulo |
| `fecha_seguimiento` | date | |
| `hembras`, `machos`, `mixtas` | int NOT NULL DEFAULT 0 | bajas separadas (mort + sel + err. sexaje) |
| `estado` | varchar(12) | `ACTIVA` \| `APLICADA` \| `LIBERADA` |
| auditoría | | |

**Aves disponibles = saldo actual − Σ reservas ACTIVAS.** Un traslado, despacho o venta ve el saldo ya
separado, así que no puede disponer de aves que un seguimiento sin validar ya dio de baja.

### 4.5 Flujo con el flag encendido

| Acción | Efecto |
|---|---|
| **Guardar** (crear) | Valida (alimento obligatorio, fecha duplicada, pendientes vencidos). NO descuenta. Crea reservas `ACTIVA` de alimento y aves. Registro queda `PENDIENTE`. |
| **Editar** (pendiente) | Reescribe las reservas `ACTIVA` de ese seguimiento (borra y vuelve a separar). **Sin cálculo de retorno**: nunca se descontó. |
| **Eliminar** (pendiente) | Libera las reservas (`LIBERADA`). Nada que devolver. |
| **Validar** | En **una transacción**: aplica el consumo real al inventario, descuenta las aves del maestro, marca reservas `APLICADA` y el registro `validado = true`. |
| **Editar/Eliminar validado** | Bloqueado (mismo criterio que reproductora hoy: para corregir hay que des-validar con permiso, o eliminar). |

Con el flag **apagado** todo el bloque se saltea y el camino es el actual, byte a byte.

### 4.6 Alimento obligatorio

Cálculo puro `AlimentoObligatorioCalculos.Validar(modulo, esLoteMixto, itemsH, itemsM, itemsGenerales)`:

| Módulo | Regla |
|---|---|
| Pollo engorde (lote mixto / Panamá) | Al menos un ítem **en el bloque Mixto** con tipo de alimento y cantidad > 0. |
| Pollo engorde (lote con sexos) | Al menos un ítem con cantidad > 0 en hembras o machos. |
| Levante y producción | Al menos un ítem con cantidad > 0 en **hembras, machos o ambos**. |
| Reproductora | Al menos un ítem con cantidad > 0 (bloque del lote). |

Se valida en el **front** (bloquea el submit y explica) y en el **backend** (defensa en profundidad; la
carga masiva y la PWA entran por el mismo servicio). El mensaje dice qué falta: *«Falta el alimento: el
registro del 12/08 no tiene tipo de alimento ni cantidad en el bloque Mixto»*.

### 4.7 Modal de motivo

Servicio compartido `shared/services/aviso-validacion.service.ts` (mismo patrón que
`ConfirmDialogService`: monta el modal dinámicamente). Reemplaza al toast en los cuatro módulos para los
casos que el usuario **tiene que leer**:

- fecha duplicada — *«Ya existe un registro para el 12/08/2026 en el lote A374A. Editá ese registro o elegí otra fecha.»*
- alimento faltante / campos obligatorios vacíos — lista con cada campo.
- pendientes vencidos que bloquean el alta.
- cualquier 400 del backend (se muestra el mensaje del servidor, no un genérico).

El toast se conserva para el éxito y para los avisos que no requieren acción.

### 4.8 Retraso y alarma

- Fila roja + badge **En retraso** + ícono de alarma en la tabla diaria de los 4 módulos.
- Al entrar al lote, si hay ≥ 1 registro vencido sin validar: **modal rojo** con el conteo y las fechas.
- **Bloqueo (decisión 3):** con pendientes vencidos el lote **no acepta un seguimiento nuevo**; el modal
  explica cuáles hay que validar. El bloqueo vive en el backend (`ValidacionSeguimientoCalculos`) y el
  front lo anticipa para no dejar llenar el formulario en vano.

### 4.9 Permisos

Patrón `modulo.accion`, enforce en backend (`_current.Permissions.Contains`) y `*appHasPermission` en el
front:

- `seguimiento_levante.validar`
- `seguimiento_produccion.validar`
- `seguimiento_engorde.validar`
- `seguimiento_reproductora_engorde.confirmar` — **ya existe**, se reutiliza.

Seed idempotente que los otorga a los roles que ya tienen el menú correspondiente (mismo criterio que
`20260722020045_SeedPermisosConfirmarEliminarSeguimientoReproductora`).

## 5. Cambios de base de datos

Migraciones EF **idempotentes** (`ADD COLUMN IF NOT EXISTS`, `CREATE TABLE IF NOT EXISTS`,
`CREATE INDEX IF NOT EXISTS`):

1. `AddFlagRequiereValidacionSeguimientoDiario` — columna en `companies`.
2. `AddValidacionSeguimientosDiarios` — `validado/validado_at/validado_por` en las 4 tablas + backfill
   `validado = true` de todo lo existente (decisión 4).
3. `AddSeguimientoReservaAlimentoYAves` — las 2 tablas nuevas + índices.
4. `SeedPermisosValidarSeguimientos` — los 3 permisos nuevos a los roles que ya tienen el menú.
5. `SeedTicketDobleValidacionSeguimientos` — data-only: el caso, la historia y las tareas (§8).

## 6. Backend — archivos

**Cálculo puro (`Application/Calculos/`) — con tests xUnit obligatorios:**
- `ValidacionSeguimientoCalculos.cs` — estado derivado (VALIDADO/PENDIENTE/EN_RETRASO), fecha límite,
  bloqueo por vencidos, gate del flag.
- `AlimentoObligatorioCalculos.cs` — §4.6.
- `ReservaSeguimientoCalculos.cs` — armado del set de reservas desde la metadata y del diff en edición.

**Infraestructura:**
- `Services/ValidacionSeguimiento/ValidacionSeguimientoService.cs` (+ `Funciones/`) — validar,
  des-validar, listar pendientes por lote.
- `Services/ValidacionSeguimiento/Funciones/...Reservas.cs` — crear/reescribir/liberar/aplicar reservas.
- Enganche en los 5 Crud existentes (levante, producción, engorde Colombia, engorde Ecuador,
  reproductora): el descuento actual queda **detrás del gate del flag**.
- `InventarioGestionService` — `GetDisponible` descuenta reservas activas.

**API:**
- `POST /api/SeguimientoValidacion/{modulo}/{id}/validar`
- `POST /api/SeguimientoValidacion/{modulo}/{id}/desvalidar` (permiso aparte)
- `GET  /api/SeguimientoValidacion/{modulo}/pendientes?loteId=`

## 7. Frontend — archivos

- `shared/services/aviso-validacion.service.ts` + `shared/components/aviso-validacion-modal/` (§4.7).
- `shared/funciones/estado-validacion-seguimiento.funcion.ts` — estado y color de fila (una sola copia
  para los 4 módulos).
- Por módulo (levante, producción, engorde, reproductora): columna **Estado**, botón ✓ Validar gateado
  por permiso, fila roja + alarma, modal de pendientes al entrar al lote, y el submit bloqueado sin
  alimento.
- Los componentes/modales nuevos llevan `changeDetection: ChangeDetectionStrategy.Eager` explícito
  (regla del repo — Angular 22 tiene OnPush por defecto).

## 8. Ticket (ItalJira)

Migración data-only idempotente que crea:

- **Caso** para moiesbbuga@gmail.com (lo cierra él después de validar).
- **Historia** «Doble validación de los seguimientos diarios» con las tareas de §9, cada una con
  `horas_estimadas`, responsable = el usuario, y `ticket_tiempos` (worklog) al finalizar.

## 9. Fases y estimación

| # | Tarea | Est. |
|---|---|---|
| V1.1 | Flag de empresa + proyecciones + servicio de config en el front | 3 h |
| V1.2 | Columnas `validado` en las 4 tablas + backfill | 2 h |
| V1.3 | Tablas de reserva (alimento y aves) + índices | 3 h |
| V1.4 | Cálculos puros + tests xUnit (3 clases) | 5 h |
| V1.5 | Servicio de validación + reservas + endpoints | 6 h |
| V1.6 | Enganche en los 5 Crud (descuento diferido tras el flag) | 8 h |
| V1.7 | Disponible = stock − reservas (inventario y formularios) | 4 h |
| V1.8 | Alimento obligatorio (back + front, 4 módulos) | 5 h |
| V1.9 | Modal de motivo compartido + reemplazo del toast | 4 h |
| V1.10 | Estado/fila roja/alarma + modal de pendientes (4 módulos) | 6 h |
| V1.11 | Bloqueo de días nuevos con vencidos | 2 h |
| V1.12 | Permisos + seed idempotente | 2 h |
| V1.13 | Ticket ItalJira (historia + tareas) | 1 h |
| V1.14 | Validación: `dotnet build`/`test`, `yarn build`, smoke doble (flag OFF y ON) | 5 h |
| | **Total** | **56 h** |

## 10. Casos de prueba

**Flag OFF (regresión — lo más importante):**
1. Levante/producción/engorde/reproductora: crear, editar y borrar un registro descuenta y devuelve
   exactamente igual que hoy (comparar movimientos de inventario y saldo de aves fila a fila).
2. `GET /api/CuadreAlimentoEngorde` sigue en **0 descuadrados**.

**Flag ON:**
3. Guardar sin alimento → rechazo con modal que nombra el bloque faltante (mixto / H / M).
4. Guardar con fecha repetida → modal que nombra la fecha y el lote.
5. Guardar OK → registro `PENDIENTE`, inventario **sin movimiento**, reserva `ACTIVA` creada, disponible
   del galpón bajado por la reserva.
6. Dos lotes del mismo galpón: el segundo ve el disponible ya descontado por la reserva del primero.
7. Editar el pendiente → reserva reescrita, sigue sin haber movimientos de inventario.
8. Eliminar el pendiente → reserva `LIBERADA`, disponible restituido, cero movimientos.
9. Validar → movimientos de consumo creados, aves descontadas del maestro, reserva `APLICADA`,
   registro de solo lectura.
10. Registro de ayer sin validar → fila roja, estado *En retraso*, modal al entrar al lote y **rechazo**
    del alta de un día nuevo.
11. Reproductora: validar dispara el cruce a pollo engorde igual que hoy (el trigger sigue leyendo
    `confirmado`).
12. Sin el permiso `*.validar` el botón no aparece y el endpoint devuelve 403.

## 11. Riesgos

- **Gate multipaís (CLAUDE.md):** V1.6 y V1.7 tocan el saldo de alimento de engorde ⇒ hay que correr
  `backend/sql/verificar_paridad_saldo_engorde.sql` **antes y después** y exigir 0 en todas las empresas
  que no sean la de prueba.
- **El histórico unificado se ANULA, nunca se abandona:** las reservas NO son movimientos y no deben
  llegar a `lote_registro_historico_unificado`. Al aplicar la reserva, el movimiento real sí entra por
  el camino de siempre (trigger AFTER INSERT).
- **Una sola fórmula por número:** el disponible pasa a ser `stock − reservas`; hay que cambiarlo en el
  único lugar que lo calcula, no en cada formulario.
- **Reproductora:** su `confirmado` lo lee una función SQL. Mapear `validado` sobre esa misma columna
  (no crear una segunda) evita que el cruce se rompa.
