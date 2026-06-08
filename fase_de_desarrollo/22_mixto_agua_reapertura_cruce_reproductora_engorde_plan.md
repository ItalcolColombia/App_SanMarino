# Plan: Mixto + Consumo de Agua + Reapertura con Novedad — Cruce Reproductora → Pollo Engorde

**Fecha:** 2026-06-05
**Módulos afectados:**
- Origen: Seguimiento Diario Lote Reproductora (`frontend/.../seguimiento-diario-lote-reproductora`, API `SeguimientoDiarioLoteReproductora`, tabla `seguimiento_diario_lote_reproductora_aves_engorde`).
- Destino del cruce: Seguimiento Diario Pollo Engorde (tabla canónica `seguimiento_diario_aves_engorde`).
- Maestro: Lote Reproductora Aves de Engorde (`lote_reproductora_ave_engorde` / módulo `lote-reproductora-ave-engorde`).
**Plan previo relacionado:** [cruce_reproductora_a_pollo_engorde_plan.md](cruce_reproductora_a_pollo_engorde_plan.md)
**Trigger existente:** [backend/sql/fn_cruce_reproductora_a_engorde.sql](../backend/sql/fn_cruce_reproductora_a_engorde.sql)

---

## 0. Decisiones confirmadas con el usuario (2026-06-05)

| # | Decisión | Respuesta |
|---|----------|-----------|
| 1 | Destino "campo mixto" de las aves devueltas | **Total mixto solo en el DTO** (`AvesDisponiblesDto.MixtasDisponibles = HembrasDisponibles + MachosDisponibles`). **NO** tocar el maestro `lote_ave_engorde`. |
| 2 | Consumo de agua al cruce | Copiar **los 4 campos** (`consumo_agua_diario`, `_ph`, `_orp`, `_temperatura`) **del primer lote reproductora** (por `repro_id` ascendente), **sin** sumar ni promediar. |
| 3 | Reapertura de lote cerrado | **Reabrir con novedad → habilita eliminar → recierra solo.** Se persiste `reabierto` + `novedad_apertura` + auditoría (quién/cuándo). Tras eliminar, el estado se recalcula (vuelve a Cerrado si aplica) y `reabierto` se resetea. |
| 4 | Llave del cruce | **Mantener EDAD** (`fecha − fecha_encasetamiento`). Sin cambios a la llave. |

> Nota: el saldo de aves del pollo engorde (`fn_seguimiento_diario_engorde`) **ya es un único número** (`hembras_l + machos_l + mixtas`). El "mixto" del requerimiento #1 se resuelve a nivel de **lectura/UI** (DTO), no en el trigger ni en el saldo.

---

## 1. Estado actual (validación del trigger y el módulo)

### 1.1 Módulo origen
- Componente: [seguimiento-diario-lote-reproductora-list.component.ts](../frontend/src/app/features/seguimiento-diario-lote-reproductora/pages/seguimiento-diario-lote-reproductora-list/seguimiento-diario-lote-reproductora-list.component.ts).
- Servicio front: [seguimiento-diario-lote-reproductora.service.ts](../frontend/src/app/features/seguimiento-diario-lote-reproductora/services/seguimiento-diario-lote-reproductora.service.ts) → `api/SeguimientoDiarioLoteReproductora`.
- Servicio back: [SeguimientoDiarioLoteReproductoraService.cs](../backend/src/ZooSanMarino.Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs). `DeleteAsync` **no** valida cierre (el bloqueo es 100% frontend).
- ⚠️ El doc [FLUJO_SEGUIMIENTO_REPRODUCTORA.md](../frontend/src/app/features/seguimiento-diario-lote-reproductora/FLUJO_SEGUIMIENTO_REPRODUCTORA.md) está **desactualizado** (dice `lote_seguimientos`); la tabla real es `seguimiento_diario_lote_reproductora_aves_engorde`. Se corregirá.

### 1.2 Trigger / función del cruce
`fn_cruce_reproductora_a_engorde(p_lote_ave_engorde_id)` + `trg_cruce_reproductora_engorde` (AFTER INSERT/UPDATE/DELETE en la tabla origen):
- Cruza por EDAD, días 1..7. Suma mortalidad/sel/error/consumo M/H; peso ponderado por aves vivas.
- UPSERT en `seguimiento_diario_aves_engorde` con `origen_cruce=true`.
- En DELETE/desalineación ya borra el registro de engorde de esa edad (satisface el cascade del req #3).
- ❌ **NO** copia `consumo_agua_*` (aunque la tabla destino sí tiene esas columnas).

### 1.3 "Devolución de aves por género" (req #1)
Vive en `GetAvesDisponiblesAsync` ([LoteReproductoraAveEngordeService.cs:372-477](../backend/src/ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeService.cs:372)). Al completar los 7 días de todos los reproductora calcula `HembrasDisponibles`/`MachosDisponibles` por separado (`AvesDisponiblesDto`).

### 1.4 Estado Cerrado (req #3)
`CalcularEstado(...)` ([LoteReproductoraAveEngordeService.cs:124-130](../backend/src/ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeService.cs:124)): **calculado**, no persistido. Cierra cuando `avesActuales<=0` **o** `numRegistros>=7`. Por eso hoy no hay forma de "reabrir": hace falta un override persistido.

---

## 2. Requerimiento #1 — Mixto en el DTO (sin tocar maestro)

**Objetivo:** que las aves devueltas tras el cierre de los reproductora se muestren como **un solo total mixto**, no separadas por género.

### Cambios
1. **DTO** [AvesDisponiblesDto.cs](../backend/src/ZooSanMarino.Application/DTOs/AvesDisponiblesDto.cs): agregar
   ```csharp
   public int MixtasDisponibles { get; set; }   // = HembrasDisponibles + MachosDisponibles
   public bool AvesDevueltas { get; set; }       // = sieteDiasCompletos (true = ya se devolvieron / mostrar como mixto)
   ```
2. **Servicio** `GetAvesDisponiblesAsync`: setear `MixtasDisponibles = hembrasDisponibles + machosDisponibles` y `AvesDevueltas = sieteDiasCompletos`. **No** cambiar la fórmula por género (se mantiene para trazabilidad); solo se agrega el total.
3. **Frontend** (donde se muestren aves disponibles del lote reproductora / pollo engorde): cuando `avesDevueltas === true`, mostrar **"Aves actuales (mixto): N"** en lugar del desglose H/M. Localizar el/los consumidores de `aves-disponibles` y del detalle del lote reproductora (`lote-reproductora-ave-engorde-list`, header del módulo de seguimiento).

**Constraint:** el maestro `lote_ave_engorde` (`hembras_l`, `machos_l`, `mixtas`) **no se modifica**. El saldo de la `fn_seguimiento_diario_engorde` permanece igual.

---

## 3. Requerimiento #2 — Consumo de agua al cruce

**Objetivo:** que el consumo de agua capturado en el seguimiento reproductora llegue al seguimiento diario pollo engorde.

### Cambios (solo SQL — función del trigger)
Editar [backend/sql/fn_cruce_reproductora_a_engorde.sql](../backend/sql/fn_cruce_reproductora_a_engorde.sql):

1. En el subselect interno `dia`, agregar las 4 columnas de agua desde `s.`:
   `s.consumo_agua_diario, s.consumo_agua_ph, s.consumo_agua_orp, s.consumo_agua_temperatura`.
2. En el agregado externo, tomar el valor **del primer lote** (no sumar/promediar). Patrón:
   ```sql
   (array_agg(dia.consumo_agua_diario ORDER BY dia.repro_id)
        FILTER (WHERE dia.consumo_agua_diario IS NOT NULL))[1]  AS agua_diario,
   -- idem ph / orp / temperatura
   ```
3. Incluir las 4 columnas en el `INSERT INTO seguimiento_diario_aves_engorde (...) VALUES (...)`:
   `consumo_agua_diario, consumo_agua_ph, consumo_agua_orp, consumo_agua_temperatura`.

**Migración:** crear migración EF `UpdateFnCruceReproductoraEngordeAgua` que haga `CREATE OR REPLACE FUNCTION fn_cruce_reproductora_a_engorde` con el cuerpo actualizado (idempotente). Mantener el `.sql` como fuente de verdad y reusar su contenido. No se toca el trigger ni el índice.

**Constraint:** la tabla destino ya tiene las columnas → no requiere cambio de schema.

---

## 4. Requerimiento #3 — Eliminar en lote cerrado vía reapertura con novedad

**Objetivo:** poder eliminar registros de seguimiento de un lote reproductora ya cerrado, reabriéndolo con una novedad (motivo). La eliminación elimina el registro espejo en el seguimiento diario pollo engorde (vía trigger existente).

### 4.1 Schema (migración EF idempotente)
`AddReaperturaToLoteReproductoraAveEngorde` sobre `lote_reproductora_ave_engorde`:
```sql
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS reabierto boolean NOT NULL DEFAULT false;
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS novedad_apertura text NULL;
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS reabierto_por integer NULL;
ALTER TABLE lote_reproductora_ave_engorde ADD COLUMN IF NOT EXISTS reabierto_at timestamptz NULL;
```

### 4.2 Backend
1. **Entidad** [LoteReproductoraAveEngorde.cs](../backend/src/ZooSanMarino.Domain/Entities/LoteReproductoraAveEngorde.cs): `Reabierto`, `NovedadApertura`, `ReabiertoPor`, `ReabiertoAt`.
2. **Config** [LoteReproductoraAveEngordeConfiguration.cs](../backend/src/ZooSanMarino.Infrastructure/Persistence/Configurations/LoteReproductoraAveEngordeConfiguration.cs): mapear las 4 columnas (snake_case).
3. **DTO** `LoteReproductoraAveEngordeDto`: agregar `bool Reabierto`, `string? NovedadApertura`. Setear en `Map(...)`.
4. **DTO request** nuevo: `ReabrirLoteReproductoraDto(string Novedad)`.
5. **Interfaz + servicio** `ILoteReproductoraAveEngordeService` / `LoteReproductoraAveEngordeService`:
   - `Task<LoteReproductoraAveEngordeDto?> ReabrirAsync(int id, string novedad)`: valida compañía, exige `novedad` no vacía, set `Reabierto=true`, `NovedadApertura`, `ReabiertoPor=_current.UserId`, `ReabiertoAt=now`. Devuelve DTO.
6. **Controller** [LoteReproductoraAveEngordeController.cs](../backend/src/ZooSanMarino.API/Controllers/LoteReproductoraAveEngordeController.cs): `POST api/LoteReproductoraAveEngorde/{id:int}/reabrir` con body `{ novedad }`.
7. **Guard + auto-recierre** en `SeguimientoDiarioLoteReproductoraService.DeleteAsync`:
   - Antes de borrar: cargar el lote padre; calcular si está Cerrado (reusar la regla `numRegistros>=7 || avesActuales<=0`). Si Cerrado y `Reabierto==false` → `throw InvalidOperationException("Debe reabrir el lote con una novedad para eliminar registros.")`.
   - Después de borrar: si el padre estaba `Reabierto==true`, set `Reabierto=false` (y opcionalmente conservar `NovedadApertura` como histórico) → **recierra solo**.
   - El trigger de BD ya elimina el espejo en `seguimiento_diario_aves_engorde`.

### 4.3 Frontend
1. **Servicio** [lote-reproductora-ave-engorde.service.ts](../frontend/src/app/features/lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service.ts): `reabrir(id, novedad)` → `POST {base}/{id}/reabrir`. Exponer `reabierto` y `novedadApertura` en el DTO TS.
2. **Componente** `seguimiento-diario-lote-reproductora-list.component.ts`:
   - `get isLoteReproductoraCerrado` se mantiene; nuevo `get puedeEliminar = !isLoteReproductoraCerrado || selectedReproductoraDetail?.reabierto === true`.
   - En el modal informativo de "lote cerrado", agregar botón **"Reabrir lote"** → abre un mini-modal que pide la **novedad** (texto obligatorio).
   - Al confirmar: `loteReproductoraSvc.reabrir(id, novedad)` → refrescar `selectedReproductoraDetail` (`reabierto=true`) → habilita los botones de eliminar.
   - `delete(id)` / `onConfirmDelete`: permitir cuando `puedeEliminar`. Tras eliminar, refrescar detalle (el back resetea `reabierto`, vuelve a bloquear).
3. **Mini-modal de novedad**: el `ConfirmationModalComponent` no captura texto. Opciones: (a) extender `ConfirmationModalData` con un campo `inputRequired/inputValue`, o (b) crear `NovedadModalComponent` simple. Preferir (a) si es de bajo impacto; si no, (b).
4. **HTML del listado** ([...-list.component.html](../frontend/src/app/features/seguimiento-diario-lote-reproductora/pages/seguimiento-diario-lote-reproductora-list/seguimiento-diario-lote-reproductora-list.component.html)): mostrar badge "Reabierto" + tooltip con la novedad cuando aplique; condicionar botones de eliminar a `puedeEliminar`.

**Constraint clave:** la eliminación dispara el trigger AFTER DELETE → recalcula edades 1..7 del lote pollo engorde → al romperse la alineación (falta ese día) se elimina el registro espejo de engorde. **No** hay que tocar el trigger para el cascade.

---

## 5. Archivos a crear / modificar (resumen)

### Backend
| Archivo | Cambio |
|---------|--------|
| `backend/sql/fn_cruce_reproductora_a_engorde.sql` | Agua: 4 campos del 1er lote en agregado + INSERT. |
| `Migrations/<ts>_UpdateFnCruceReproductoraEngordeAgua.cs` | `CREATE OR REPLACE` de la función. |
| `Migrations/<ts>_AddReaperturaToLoteReproductoraAveEngorde.cs` | 4 columnas idempotentes. |
| `Domain/Entities/LoteReproductoraAveEngorde.cs` | 4 props de reapertura. |
| `Infrastructure/.../LoteReproductoraAveEngordeConfiguration.cs` | mapeo de 4 columnas. |
| `Application/DTOs/AvesDisponiblesDto.cs` | `MixtasDisponibles`, `AvesDevueltas`. |
| `Application/DTOs/LoteReproductoraAveEngordeDto.cs` | `Reabierto`, `NovedadApertura` + `ReabrirLoteReproductoraDto`. |
| `Application/Interfaces/ILoteReproductoraAveEngordeService.cs` | `ReabrirAsync`. |
| `Infrastructure/Services/LoteReproductoraAveEngordeService.cs` | `MixtasDisponibles`, `ReabrirAsync`, set reapertura en `Map`. |
| `Infrastructure/Services/SeguimientoDiarioLoteReproductoraService.cs` | guard de cierre + auto-reset `reabierto` en `DeleteAsync`. |
| `API/Controllers/LoteReproductoraAveEngordeController.cs` | endpoint `POST {id}/reabrir`. |

### Frontend
| Archivo | Cambio |
|---------|--------|
| `lote-reproductora-ave-engorde/services/lote-reproductora-ave-engorde.service.ts` | `reabrir()`, campos `reabierto/novedadApertura`. |
| `seguimiento-diario-lote-reproductora-list.component.ts` + `.html` | botón Reabrir, mini-modal novedad, `puedeEliminar`, badge. |
| (modal novedad) | extender `ConfirmationModalComponent` o nuevo componente. |
| consumidores de `aves-disponibles` / detalle reproductora | mostrar mixto cuando `avesDevueltas`. |
| `FLUJO_SEGUIMIENTO_REPRODUCTORA.md` | corregir tabla canónica. |

---

## 6. Casos de prueba

- **T1 (agua):** cargar seguimiento reproductora con `consumo_agua_diario/ph/orp/temp` → el registro espejo de engorde de esa edad trae esos 4 valores del 1er lote.
- **T2 (agua multi-lote):** 2 reproductora con agua distinta → el espejo toma los del `repro_id` menor; no se suman.
- **T3 (mixto):** lote con 7 días completos en todos sus reproductora → `AvesDisponiblesDto.MixtasDisponibles == HembrasDisponibles + MachosDisponibles` y `AvesDevueltas==true`; la UI muestra mixto.
- **T4 (reapertura):** lote Cerrado → eliminar sin reabrir = error. Reabrir con novedad → eliminar OK; novedad/auditoría persistida; `reabierto` queda en false tras eliminar.
- **T5 (cascade):** al eliminar el día N de un reproductora (lote reabierto), desaparece el registro espejo del día N en `seguimiento_diario_aves_engorde`.
- **T6 (recierre):** tras eliminar, si el lote sigue con ≥7 registros / aves agotadas vuelve a Cerrado; si bajó de 7, queda Vigente (correcto).
- **T7 (no regresión):** el saldo `fn_seguimiento_diario_engorde` y los días 1..7 siguen iguales (sin doble descuento).

---

## 7. Riesgos / notas

- **Validación local obligatoria** antes de mergear: `make up`, `dotnet ef database update`, probar el trigger con datos. Detener servicios (`make down`) al terminar.
- **Idempotencia**: columnas con `IF NOT EXISTS`; función con `CREATE OR REPLACE`.
- **Auditoría reapertura**: `reabierto_por` usa `_current.UserId` (int). Confirmar el tipo del user id en `ICurrentUser`.
- **`reabierto` se resetea al eliminar** para cumplir "recierra solo"; `novedad_apertura`/`reabierto_at` se conservan como histórico del último motivo.
