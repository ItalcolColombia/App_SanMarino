# Plan — Reapertura validada de Levante + Cierre/Reapertura de Lote de Producción

**Fecha:** 2026-07-26
**Módulos:** Seguimiento Diario de Levante · Seguimiento Diario de Producción

---

## 1. Objetivo

Dos cambios acoplados sobre el ciclo de vida **Levante → Producción**:

**A. Reapertura de levante con validación previa.** Hoy `POST /api/LotePosturaLevante/{id}/abrir`
reabre el levante y **borra en cascada y en silencio** el lote de producción con TODOS sus
seguimientos (`LotePosturaLevanteService.cs:505`). El usuario puede perder captura de producción sin
enterarse. Nuevo comportamiento:

1. Si el lote de producción tiene **seguimiento registrado por el usuario** → **se rechaza** la
   reapertura con un mensaje explícito que dice cuántos registros hay, de qué fechas, y que deben
   eliminarse desde *Seguimiento Diario de Producción* antes de reabrir.
2. Si **no** tiene seguimiento de usuario → se permite reabrir, y el lote de producción se
   **elimina (soft delete)** junto con sus filas de sistema. Al volver a cerrar el levante se crea
   de nuevo, actualizado.
3. Si el lote de producción está **cerrado** → se rechaza: primero hay que reabrirlo.

**B. Cierre y reapertura de lote de producción (feature nueva).** Hoy **no existe**:
`lote_postura_produccion.estado_cierre` nace en `"Abierta"` y **nadie lo cambia nunca**. Además el
servicio que atiende `/api/Produccion/seguimiento` — el que usa el módulo — **no valida
`estado_cierre` al crear, editar ni eliminar**. Se implementa el cierre/reapertura completo y se
alinean las tres operaciones detrás del guard.

---

## 2. Hallazgos del análisis que condicionan el diseño

### 2.1 El cierre de levante YA crea filas en `seguimiento_diario_produccion`

Contar "cualquier fila" bloquearía la reapertura **siempre**. Al cerrar se generan dos filas de
sistema (o una sola, porque el arrastre hace *merge* sobre la del traslado):

| Origen | Archivo | Marca |
|---|---|---|
| Arrastre de huevos del levante | `ArrastreHuevosLevanteService.cs:106` | `tipo_alimento = "N/A"`, `metadata.arrastreHuevosLevante`, obs. "Huevos arrastrados del levante" |
| Traslado de aves del cierre | `MovimientoAvesService.SeguimientoDiario.cs:343,549` | `tipo_alimento = "N/A"`, obs. "Entrada por movimiento…" / "Registro de descuento por traslado…" |

### 2.2 `tipo_alimento` distingue sistema de usuario (verificado)

- Filas de sistema: `"N/A"` **hardcodeado** en los 3 puntos de creación.
- Filas de usuario: el form manda `tipoAlimento: ['Standard', Validators.required]`
  (`modal-seguimiento-diario.component.ts:255`) — siempre un alimento real.
- Al hacer *merge* del usuario sobre una fila de sistema, `ProduccionService` **sobrescribe**
  `fila.TipoAlimento` con el real (`:545` alta con merge, `:705` update) ⇒ la fila pasa a contar
  como del usuario automáticamente. Es el comportamiento correcto: esa fila ya tiene captura.

### 2.3 Incoherencias preexistentes que se corrigen de paso

- El `<summary>` de `ILotePosturaLevanteService.AbrirLoteAsync` promete validar dependientes y el
  tooltip del front dice "si el lote de producción aún no tiene seguimiento" — **ninguno de los dos
  era cierto**. Este cambio hace que el código cumpla lo que ya documentaba.
- Ya existe `EnsureLoteProduccionAbiertoAsync` (`SeguimientoProduccionService.cs:283`, REQ-006) pero
  cuelga del **otro** servicio; `/api/Produccion/seguimiento` no lo usa. Se reutiliza el criterio.
- Género del estado: el LPP nace `"Abierta"` y el levante usa `"Abierto"`. El guard existente usa
  `StartsWith("Cerrad")`, tolerante a ambos. Se **conserva** el género de cada tabla (no se
  normaliza: cambiarlo tocaría datos históricos sin necesidad).

---

## 3. Enfoque arquitectónico

Toda la decisión es **lógica pura** en `Application/Calculos/` con tests xUnit (gate CI). Los
servicios solo leen de BD y delegan. El filtrado se resuelve **en la consulta**, no en memoria.

### 3.1 Nuevo cálculo puro — `CicloVidaPosturaCalculos.cs`

```
Application/Calculos/CicloVidaPosturaCalculos.cs   (static, sin EF)
```

**`EsRegistroDeUsuario(RegistroProduccionResumen fila) → bool`** — `true` si CUALQUIERA:

1. `tipo_alimento` (trim) ∉ { `""`, `"N/A"` } → el usuario eligió alimento;
2. `cons_kg_h + cons_kg_m > 0` → consumo capturado;
3. `mortalidad_h > 0` o `sel_m > 0` → captura manual (los descuentos de traslado escriben `SelH` y
   `MortalidadM` en **negativo**, por eso se miran las otras dos);
4. `huevo_tot >` el total declarado en `metadata.arrastreHuevosLevante.aplicado` → hay huevos que no
   vinieron del arrastre.

Red de seguridad: cualquier fila que no encaje en el patrón exacto de sistema se considera del
usuario ⇒ **fail-closed**: ante la duda, se bloquea la reapertura en vez de borrar datos.

**`EstaCerrado(string?)`** — mismo criterio que REQ-006 (`StartsWith("Cerrad")`, case-insensitive),
centralizado para que levante y producción no diverjan.

**`ConstruirMensajeBloqueoReapertura(int cantidad, DateTime? primera, DateTime? ultima, string loteNombre)`**
— mensaje único y explícito, p. ej.:

> No se puede reabrir el lote de levante: el lote de producción «P-L001» tiene 12 registros de
> seguimiento diario (del 01/03/2026 al 12/03/2026). Elimine esos registros desde Seguimiento Diario
> de Producción y vuelva a intentarlo. Al reabrir, el lote de producción se elimina y se vuelve a
> crear actualizado cuando cierre el levante nuevamente.

### 3.2 Backend — archivos

| Archivo | Cambio |
|---|---|
| `Application/Calculos/CicloVidaPosturaCalculos.cs` | **NUEVO** — lógica pura de arriba |
| `Application/DTOs/Lotes/CierreLoteProduccionDto.cs` | **NUEVO** — `CerrarLoteProduccionRequest(string Motivo, string ClosedByUserId)`, `AbrirLoteProduccionRequest(string Motivo, string OpenedByUserId)`, `CierreLoteProduccionResumenDto(...)` |
| `Application/DTOs/Lotes/CierreLoteLevanteDto.cs` | `CierreLoteLevanteResumenDto` += `PuedeReabrir`, `RegistrosProduccionUsuario`, `MotivoBloqueoReapertura` (aditivo, al final, con default) |
| `Application/Interfaces/ILotePosturaLevanteService.cs` | `<summary>` corregido de `AbrirLoteAsync` + nuevo `GetResumenReaperturaAsync(int id, ct)` |
| `Application/Interfaces/ILotePosturaProduccionService.cs` | += `CerrarLoteAsync`, `AbrirLoteAsync`, `GetResumenCierreAsync` |
| `Infrastructure/Services/LotePosturaLevanteService.cs` | `AbrirLoteAsync`: validación previa + **soft delete** del LPP; `EliminarDependientesLoteProduccionAsync` pasa a borrar solo filas de sistema |
| `Infrastructure/Services/LotePosturaProduccionService.cs` | + cierre/reapertura con auditoría |
| `Infrastructure/Services/ProduccionService.cs` | `EnsureLoteProduccionAbiertoAsync` en **Crear / Actualizar / Eliminar** |
| `API/Controllers/LotePosturaProduccionController.cs` | + `POST {id}/cerrar`, `POST {id}/abrir`, `GET {id}/resumen-cierre` |
| `API/Controllers/LotePosturaLevanteController.cs` | + `GET {id}/resumen-reapertura` |

### 3.3 Cambio de BD — auditoría del cierre de producción

Migración EF **idempotente** sobre `lote_postura_produccion` (sin cierre no hay a quién reclamarle):

```sql
ALTER TABLE lote_postura_produccion ADD COLUMN IF NOT EXISTS estado_cierre_motivo   text;
ALTER TABLE lote_postura_produccion ADD COLUMN IF NOT EXISTS estado_cierre_fecha    timestamptz;
ALTER TABLE lote_postura_produccion ADD COLUMN IF NOT EXISTS estado_cierre_user_id  integer;
```

Sin `NOT NULL` y sin backfill: los lotes existentes quedan en `NULL` (nunca se cerraron). No se toca
`estado_cierre`, que ya existe.

### 3.4 Soft delete del LPP al reabrir levante

`prod.DeletedAt = DateTime.UtcNow` en vez de `_ctx.LotePosturaProduccion.Remove(prod)`. Compatible
con lo que ya existe: el cierre valida `p.DeletedAt == null` (`:405`), así que el siguiente cierre
recrea el lote sin conflicto y queda el rastro de que existió.

**Dependientes que sí se borran** (son filas de sistema regeneradas en el próximo cierre):
`SeguimientoDiario` y `SeguimientoProduccion` del LPP (ya validadas como no-usuario),
`EspejoHuevoProduccion`, y se desvinculan los `TrasladoHuevos`.

### 3.5 Frontend

**Levante** (`seguimiento-lote-levante-list`): antes de abrir el modal de reapertura se pide
`GET {id}/resumen-reapertura`. Si `puedeReabrir === false`, el modal muestra el bloqueo con el
detalle y el botón de confirmar queda deshabilitado. Si `true`, se muestra un aviso explícito de que
el lote de producción se eliminará y se recreará al cerrar de nuevo. El backend revalida igual
(el front no es la autoridad).

**Producción** (`lote-produccion-list` + `tabs-principal`): botones **Cerrar lote** / **Abrir lote**
junto a la tarjeta "Estado", con modal de motivo (mín. 3 caracteres) espejando el de levante. Con el
lote cerrado se ocultan/deshabilitan **Nuevo registro**, **✎ Editar** y **🗑 Eliminar**.

Componentes nuevos con `changeDetection: ChangeDetectionStrategy.Eager` **explícito** (regla Angular
22 de `CLAUDE.md`), `ToastService` para los mensajes y `ConfirmDialogService` para las confirmaciones
— nunca `alert()`/`confirm()`.

---

## 4. Reglas de negocio (resumen normativo)

| # | Regla |
|---|---|
| R1 | Reabrir levante exige que el LPP asociado **no** tenga registros de seguimiento del usuario. |
| R2 | Reabrir levante exige que el LPP **no** esté cerrado (primero se reabre producción). |
| R3 | Al reabrir levante sin registros de usuario: LPP a soft delete + borrado de sus filas de sistema. |
| R4 | El LPP se recrea, actualizado, en el siguiente cierre de levante (comportamiento ya existente). |
| R5 | Con LPP **cerrado** no se puede crear, editar ni eliminar seguimiento diario de producción. |
| R6 | Cerrar producción exige estado abierto; reabrir exige estado cerrado y motivo ≥ 3 caracteres. |
| R7 | Cerrar/reabrir producción **no** borra ni modifica ningún seguimiento: solo cambia el estado. |
| R8 | Ante una fila de producción que no encaje en el patrón de sistema ⇒ se trata como del usuario. |

---

## 5. Casos de prueba

### 5.1 xUnit — `CicloVidaPosturaCalculosTests.cs` (gate CI)

| Caso | Esperado |
|---|---|
| Fila de arrastre pura (`N/A`, consumo 0, huevos = marca) | **sistema** |
| Fila de traslado pura (`N/A`, `SelH` negativo, todo 0) | **sistema** |
| Fila de arrastre + usuario registró el día (`tipo_alimento="Postura"`) | **usuario** |
| `N/A` pero `cons_kg_h > 0` | **usuario** |
| `N/A` pero `mortalidad_h > 0` | **usuario** |
| `N/A`, huevos > los declarados en la marca de arrastre | **usuario** |
| `N/A` sin marca de arrastre y con huevos | **usuario** (fail-closed) |
| `tipo_alimento` vacío/null, todo en cero, sin marca | **sistema** |
| `EstaCerrado`: `"Cerrado"`, `"Cerrada"`, `"cerrado "`, `"Abierta"`, `null`, `""` | `t,t,t,f,f,f` |
| Mensaje de bloqueo: 1 registro / N registros / rango de fechas | textos exactos |

### 5.2 Smoke API (local, JWT minteado)

1. Cerrar levante con huevos → LPP creado + fila de arrastre. **Reabrir → permitido** (la fila es de
   sistema); LPP con `deleted_at`, fila de arrastre borrada, levante `"Abierto"`.
2. Cerrar de nuevo → LPP **nuevo** creado con los huevos recalculados.
3. Registrar un seguimiento de producción → **reabrir devuelve 400** con el mensaje y el conteo
   correctos; el LPP y los registros **siguen intactos** (verificar en BD).
4. Eliminar ese seguimiento → reabrir vuelve a estar permitido.
5. Cerrar producción → crear / editar / eliminar seguimiento devuelven **400**; reabrir producción →
   las tres vuelven a funcionar.
6. Reabrir levante con producción **cerrada** → 400 pidiendo reabrir producción primero.
7. Motivo < 3 caracteres y cierre de un lote ya cerrado → 400.

### 5.3 Smoke UI (dev server)

- Modal de reapertura de levante en los dos estados (bloqueado con detalle / permitido con aviso).
- Cerrar producción: los 3 botones desaparecen; reabrir: vuelven.
- Modales abiertos y cerrados **dos veces** (checklist de change detection de `CLAUDE.md`).

---

## 6. Riesgos

| Riesgo | Mitigación |
|---|---|
| Clasificar mal una fila y borrar captura del usuario | Fail-closed (R8) + 10 tests puros + smoke que verifica en BD que nada se borró |
| La migración corre en prod al desplegar | 3 `ADD COLUMN IF NOT EXISTS`, nullable, sin backfill: reejecutable y sin bloqueo de tabla |
| Bloquear producción legítima con el guard nuevo | El guard solo dispara con `EstadoCierre` cerrado, y **hoy ningún lote lo está** ⇒ impacto cero sobre datos existentes |
| Soft delete deja el LPP visible en algún listado | Auditar que las consultas de LPP filtren `DeletedAt == null` antes de mergear |
