# Código ERP de engorde a nivel GRANJA con avance automático al cerrar ciclo — Panamá

**Fecha:** 2026-07-22 · **Alcance:** solo Panamá (gobernado por dato en la granja, no por país hardcodeado)

## Requerimiento (del usuario)

Hoy el código ERP del lote de pollo engorde se digita a mano al crear cada lote. Se pide:

1. El código ERP se define **al crear/editar la granja** (campo nuevo en granja, sección Panamá).
2. Todos los lotes de engorde que se creen en esa granja **capturan automáticamente** el código ERP vigente de la granja (el form ya lo trae por defecto, no editable).
3. Cuando se **cierran/liquidan TODOS los lotes** de un lote base en esa granja (todas las corridas de todos los galpones, ej. base 17 → "17 - 1" y "17 - 2" en galpón 1 + "17 - 1" en galpón 3), el código de la granja **avanza +1** automáticamente:
   - `4001017` (base 17 activa) → cierra todo el 17 → `4001018` (empieza el 18)
   - Patrón continúa: … `4001099` → `4001100` (base 100 toma 3 dígitos) → `4001101` …
   - Aritméticamente el avance es **+1 numérico sobre el código completo** (conservando ceros a la izquierda si los hubiera).

## Enfoque arquitectónico

- **La granja es la fuente del código.** Nuevo campo `farms.codigo_erp_engorde` (varchar 20, NULL). Si está configurado ⇒ comportamiento Panamá activado para esa granja; si es NULL ⇒ comportamiento actual (otros países no cambian). No se detecta país en backend (mismo criterio que `AutoNombrePorCorrida`).
- **Captura al crear lote** (`LoteAveEngordeService.CreateAsync`): si la granja tiene código ⇒ `LoteErp = código de la granja` (backend autoritativo, ignora lo que venga en el DTO). Si no ⇒ `LoteErp = dto.LoteErp` (hoy).
- **Avance al cerrar** (`LoteAveEngordeService.CerrarLoteAsync`, único punto de cierre — POST `/api/LoteAveEngorde/{id}/cerrar`): tras marcar `EstadoOperativoLote="Cerrado"`, si:
  1. el lote tiene `LoteBaseEngordeId`,
  2. la granja tiene `CodigoErpEngorde`,
  3. el `LoteErp` del lote **coincide** con el código vigente de la granja (guarda de ciclo: evita doble avance al re-cerrar lotes reabiertos de ciclos viejos),
  4. no queda ningún otro lote **abierto** (no eliminado) de esa misma (granja + lote base),
  ⇒ `farm.CodigoErpEngorde = siguiente(código)` en el **mismo SaveChanges** (atómico con el cierre).
- **Cálculo puro** en `Application/Calculos/GestionLotesEngordeCalculos.cs` (mismo archivo de la corrida):
  - `SiguienteCodigoErpGranja(string?)`: trim; solo dígitos (si no ⇒ `null` = no avanzar); parse long +1; pad-left a la longitud original si encogió. `4001017→4001018`, `4001099→4001100`, `4001100→4001101`, `"0099"→"0100"`.
  - `EsCodigoErpGranjaValido(string?)`: vacío/null válido (opcional) o solo dígitos, máx 20.
- **Reapertura (`AbrirLoteAsync`): NO decrementa.** Si se reabre por error tras el avance, el código de la granja se corrige manualmente editando la granja (campo editable). Re-cerrar ese lote no vuelve a avanzar gracias a la guarda 3.
- **Update de lote** (`UpdateAsync`): si la granja tiene código y el lote ya tiene `LoteErp` ⇒ se **conserva** el almacenado (histórico del ciclo, no se re-estampa ni se deja pisar). Si el lote no tiene (lotes viejos) ⇒ acepta el del DTO (backfill manual). Granjas sin código ⇒ comportamiento actual.

## Archivos a modificar

### Backend
| Archivo | Cambio |
|---|---|
| `Domain/Entities/Farm.cs` | + `string? CodigoErpEngorde` |
| `Infrastructure/Persistence/Configurations/FarmConfiguration.cs` | + mapeo `codigo_erp_engorde`, maxlen 20 |
| `Infrastructure/Migrations/<ts>_AddCodigoErpEngordeToFarm.cs` | `ALTER TABLE farms ADD COLUMN IF NOT EXISTS codigo_erp_engorde varchar(20)` (idempotente) + snapshot regenerado con `dotnet ef` |
| `Application/DTOs/Farms/FarmDto.cs`, `CreateFarmDto.cs`, `UpdateFarmDto.cs`, `FarmDetailDto.cs` | + `string? CodigoErpEngorde = null` al final (records posicionales, no rompe llamadas existentes) |
| `Infrastructure/Services/FarmService.cs` | Create/Update: normalizar (trim, ''→null) + validar solo dígitos; incluir el campo en TODAS las proyecciones `new FarmDto(...)` (`ToFarmDtoListAsync` ×2, `GetByIdAsync` ×2, retornos Create/Update, `GetByZonaUsuarioAsync`) y `ProjectToDetail` (FarmDetailDto) |
| `Application/Calculos/GestionLotesEngordeCalculos.cs` | + `SiguienteCodigoErpGranja`, `EsCodigoErpGranjaValido` (puros) |
| `Infrastructure/Services/LoteAveEngordeService.cs` | `CreateAsync`: estampar `LoteErp` desde granja si tiene código. `UpdateAsync`: conservar `LoteErp` almacenado si granja tiene código. `CerrarLoteAsync`: llamar `AvanzarCodigoErpGranjaSiCicloCerradoAsync(ent)` antes del SaveChanges (método privado nuevo con las guardas 1-4) |
| `tests/ZooSanMarino.Application.Tests/GestionLotesEngordeCalculosTests.cs` (o nuevo) | Theory: 4001017→4001018, 4001099→4001100, 4001100→4001101, "0099"→"0100", inválidos (letras, vacío, null) → null; validador |

### Frontend
| Archivo | Cambio |
|---|---|
| `features/farm/services/farm.service.ts` | + `codigoErpEngorde?: string \| null` en `FarmDto` y `CreateFarmDto` |
| `features/farm/components/farm-form/farm-form.component.ts/.html` | Control `codigoErpEngorde` (pattern `^\d*$`, maxlength 20) en sección Panamá (`*appShowIfCountry="'PANAMA'"`), help text explicando captura + avance automático; payload con trim/''→null |
| `features/lote-engorde/components/lote-engorde-list/lote-engorde-list.component.ts/.html` | Campo `loteErpBloqueado` + método `aplicarErpGranjaPanama()`: creación Panamá ⇒ al elegir granja, `loteErp` se llena desde `farmById[granjaId].codigoErpEngorde` y el input queda `[readonly]`; edición Panamá con ERP ya guardado ⇒ readonly; granja sin código u otros países ⇒ editable como hoy. Hint bajo el input |

## Reglas de negocio / decisiones

- El código es **numérico** (solo dígitos). El avance es +1 con padding preservado.
- El sistema **no** crea ni activa lotes base automáticamente (el "18" lo crean/asignan como hoy); solo el código ERP avanza.
- Sin decremento automático en reapertura ni al eliminar lotes; corrección manual en la granja (campo siempre editable en el form de granja).
- Lotes existentes NO se tocan (sin backfill): el código capturado es histórico por lote.
- Cierre + avance atómicos (un solo `SaveChangesAsync`).

## Casos de prueba

1. **Puro (xUnit):** los del cuadro de tests (patrón 99→100 incluido).
2. **Smoke manual (local, Panamá):** granja con `4001017` → crear lote (base 17, galpón X) ⇒ `lote_erp=4001017` aunque el DTO mande otro valor; crear "17 - 2" ⇒ igual. Cerrar "17 - 1" (queda "17 - 2" abierto) ⇒ granja sigue `4001017`. Cerrar "17 - 2" ⇒ granja pasa a `4001018`. Reabrir "17 - 2" y re-cerrar ⇒ granja sigue `4001018` (no doble avance). Granja sin código ⇒ todo como hoy.
3. **Front:** form granja Panamá muestra/persiste el campo; form lote Panamá lo trae readonly; Ecuador/Colombia sin cambios visibles.

## BD

- Migración EF idempotente (una columna nullable). Se aplica sola en deploy (`Database__RunMigrations=true`); local: `dotnet ef database update` contra `sanmarinoapplocal` (:5433).
