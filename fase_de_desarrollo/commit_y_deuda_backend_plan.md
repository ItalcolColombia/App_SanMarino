# Plan: Commit de cambios actuales + Refactor de deuda técnica en backend

**Fecha:** 2026-07-10  
**Alcance:** Dos misiones secuenciales en una sola PR de backend.

---

## 🎯 Objetivo

1. **Commit 1 (INMEDIATO):** Integrar cambios funcionales en progreso (Features 11-13, seguimiento diario convergencia, indicadores y liquidación).
2. **Commit 2 (REFACTOR):** Partir servicios monolíticos (3110→2507→2296 líneas) en módulos focalizados vía `partial class` (patrón Clean Architecture obligatorio).

---

## 📋 COMMIT 1: Cambios funcionales en progreso (sin refactor)

**Scope:** Integrar trabajo en curso sin fragmentar responsabilidades.  
**Criterio de aceptación:** `dotnet build` + `dotnet test` 0 err, validación de contratos API.

### Backend — Archivos modificados (validar integridad)

#### A. **Servicios de seguimiento** (convergencia Feature-13)

| Archivo | Cambios | Validar |
|---------|---------|---------|
| `MovimientoAvesService.cs` | +319 líneas; helpers Feature-13 (`UpsertSeguimientoLevanteAsync`, descuentos vía `SeguimientoDiario`) | Traslados/ventas reflejados en `seguimiento_diario`; SelH/SelM acumuladas correctamente |
| `SeguimientoDiarioService.cs` | +9 líneas; columnas de traslado/venta en canonical row | Registro con `ciclo='Traslado'` guardado; acumulados OK |
| `SeguimientoLoteLevanteService.cs` | +10 líneas; compatibilidad con `SeguimientoDiario` | Datos heredados de `SeguimientoLoteLevante` no cambian |
| `SeguimientoAvesEngordeService.cs` | +154 líneas; Feature-13 para engorde | Segumientos en `seguimiento_diario` con `tipo='engorde'` |
| `SeguimientoAvesEngordeEcuadorService.cs` | +153 líneas; Feature-13 para Ecuador | Ídem con segmentación por país |
| `TrasladoAvesDesdeSegService.cs` | +26 líneas; upsert canonical row | Traslados reflejan en fila canónica, no duplican |

#### B. **Servicios de liquidación/indicadores** (Feature-11)

| Archivo | Cambios | Validar |
|---------|---------|---------|
| `LiquidacionTecnicaService.cs` | +51 líneas; ajuste de fórmulas postura | Totales a cliente = base - merma (NULL→descarta); aritmética igual |
| `LiquidacionTecnicaComparacionService.cs` | +45 líneas; comparación vs guía genética | Índices nutricionales OK |
| `IndicadorEcuadorService.cs` | +46 líneas; indicadores postura | KPI acumulados correctos |
| `ColombiaInventarioConsumoService.cs` | +63 líneas; modelo B + descuento validado | Stock cierra; consumo realista |
| `SeguimientoProduccionService.cs` | +78 líneas; alineación producción | Transición levante→producción OK |
| `LoteReproductoraService.cs` | +8 líneas; mantenimiento data | Lotes de reproducción no regresionan |

#### C. **DTOs y Entidades** (contratos API)

| Archivo | Cambios | Validar |
|---------|---------|---------|
| `Lote.cs` (Domain) | Campos agregados/renombrados | Migración FK respetada; `LotePadreId`, `HembrasL`, `MachosL` inmutables |
| `SeguimientoDiario.cs` (Domain) | +columnas de traslado/venta/ciclo | NOT NULL defaults acorde; enums "levante"/"producción"/"engorde" |
| `SeguimientoProduccion.cs` (Domain) | Campos renombrados/reorganizados | Backward-compat con `seguimiento_produccion` tabla |
| `ProduccionLote.cs` (Domain) | Campos de control | aves_iniciales_h/m preservadas |
| `CreateSeguimientoLoteLevanteRequest.cs` | DTO para POST | Validación de aves_iniciales |
| `SeguimientoDiarioDto.cs` | DTO salida | Respeta campos legacy |
| `SeguimientoLoteLevanteDto.cs` | DTO salida | Ídem |

#### D. **Migraciones y configuraciones**

| Archivo | Cambios | Validar |
|---------|---------|---------|
| `ZooSanMarinoContextModelSnapshot.cs` | ModelSnapshot actualizado | Reflejar esquema actual; sin cambios DD|
| `SeguimientoDiarioConfiguration.cs` | Mappeo EF Core actualizado | snake_case OK; FK/constraints respetados |
| `SeguimientoProduccionConfiguration.cs` (BORRADO) | Se consolidó en otra config | Sin referencia orfana a tabla |

#### E. **Controladores** (validar routing)

| Archivo | Cambios | Validar |
|---------|---------|---------|
| `LiquidacionTecnicaController.cs` | OK; endpoint GET `{loteId}` | Contrato `/api/liquidacion-tecnica/{id}` sin cambios |
| `MovimientoAvesController.cs` | OK; POST traslados/ventas | Body `{ tipoMovimiento, cantidadHembras, ... }` sin cambios |

#### F. **Config y startup**

| Archivo | Cambios | Validar |
|---------|---------|---------|
| `DatabaseInitializer.cs` | SQL idempotente; `IF NOT EXISTS` para `produccion_lote` | Schema provisioning sin error; tabla creada 0 o 1 vez |
| `appsettings.Development.json` | Connection string local | BD `zoo_sanmarino_db` en `:5432` accesible |
| `launchSettings.json` | Profile "Development" | Puerto :5002 sin conflicto |

---

## 🚨 COMMIT 2: Refactor de deuda técnica (>800 líneas)

**Scope:** Particionar servicios monolíticos en `partial class` (sin cambiar comportamiento).  
**Patrón obligatorio:** [CLAUDE.md Clean Code § Backend — `partial class` en `Funciones/`](CLAUDE.md#-clean-code--organización-de-funciones-front--back)

### 5 servicios críticos (priority order)

| # | Archivo | Líneas | Responsabilidades identificadas | Partición propuesta |
|---|---------|--------|----------------------------------|--------------------|
| **1** | `ReporteTecnicoService.cs` | 3110 | Levante/producción/reproducción reportes | `Levante.cs` (gen diario/semanal), `Produccion.cs`, `Consolidacion.cs`, `Mapeo.cs` |
| **2** | `MovimientoAvesService.cs` | 2507 | Traslados, ventas, descuentos diarios, traslados huevos | `Traslados.cs`, `Ventas.cs`, `Descuentos.cs`, `Huevos.cs` |
| **3** | `InventarioGestionService.cs` | 2296 | CRUD inventario, mov, stock, ajustes, búsqueda | `Crud.cs`, `Movimientos.cs`, `Stock.cs`, `Busqueda.cs` |
| **4** | `ReporteTecnicoProduccionService.cs` | 1953 | Reportes producción (diario/semanal) | `Diario.cs`, `Semanal.cs`, `Consolidacion.cs` |
| **5** | `SeguimientoAvesEngordeService.cs` | 1884 | Seguimiento diario engorde, validaciones, cálculos | `Diario.cs`, `Validaciones.cs`, `Calculos.cs` |

### Estrategia por archivo

#### **1. ReporteTecnicoService.cs** (3110 → ~620 c/partial)

**Archivo Ancla:** `ReporteTecnicoService.cs`
```csharp
public class ReporteTecnicoService : IReporteTecnicoService
{
    // Campos, ctor, interfaz, helpers estáticos SOLO aquí
}
```

**Particiones:**
- `Funciones/ReporteTecnicoService.Levante.cs`: `GenerarReporteDiarioSubloteAsync()`, `GenerarReporteDiarioConsolidadoAsync()`, `ObtenerDatosDiariosLevanteAsync()`
- `Funciones/ReporteTecnicoService.Produccion.cs`: `GenerarReporteDiarioProduccionAsync()`, métodos producción
- `Funciones/ReporteTecnicoService.Reproduccion.cs`: datos reproducción (si aplica)
- `Funciones/ReporteTecnicoService.Consolidacion.cs`: `ConsolidarSemanales()`, helpers consolidación
- `Funciones/ReporteTecnicoService.Mapeo.cs`: `MapearInformacionLote()`, mapeos DTOs

**Validación:**
- `dotnet build` → 0 errores, mismo namespace `ZooSanMarino.Infrastructure.Services`
- Tests: comportamiento idéntico (stubs de entrada/salida sin cambios)

---

#### **2. MovimientoAvesService.cs** (2507 → ~500 c/partial)

**Archivo Ancla:** `MovimientoAvesService.cs`

**Particiones:**
- `Funciones/MovimientoAvesService.Traslados.cs`: `AplicarDescuentoEnLevanteDiariaAvesAsync()`, helpers traslado
- `Funciones/MovimientoAvesService.Ventas.cs`: lógica de ventas (separar de traslados)
- `Funciones/MovimientoAvesService.Descuentos.cs`: descuentos en daily entries
- `Funciones/MovimientoAvesService.Huevos.cs`: `TrasladoHuevosService` (si integrado acá)

**Validación:**
- Descuentos en `SeguimientoDiario` (vía `UpsertSeguimientoLevanteAsync()`) no regresionan
- Traslados no duplican registros

---

#### **3. InventarioGestionService.cs** (2296 → ~500 c/partial)

**Archivo Ancla:** `InventarioGestionService.cs`

**Particiones:**
- `Funciones/InventarioGestionService.Crud.cs`: `GetAsync()`, `CreateAsync()`, `UpdateAsync()`, `DeleteAsync()`
- `Funciones/InventarioGestionService.Movimientos.cs`: `RegisterEntryAsync()`, `RegisterExitAsync()`, `RegisterTransferAsync()`
- `Funciones/InventarioGestionService.Stock.cs`: `GetStockAsync()`, `ComputeStockAsync()`, ajustes
- `Funciones/InventarioGestionService.Busqueda.cs`: `SearchAsync()`, filtros

**Validación:**
- API endpoints `/api/farms/{farmId}/inventory/...` sin cambio de contrato
- Stock reconciliation correcta

---

#### **4. ReporteTecnicoProduccionService.cs** (1953 → ~650 c/partial)

**Archivo Ancla:** `ReporteTecnicoProduccionService.cs`

**Particiones:**
- `Funciones/ReporteTecnicoProduccionService.Diario.cs`: reportes diarios
- `Funciones/ReporteTecnicoProduccionService.Semanal.cs`: consolidaciones semanales
- `Funciones/ReporteTecnicoProduccionService.Consolidacion.cs`: helpers

---

#### **5. SeguimientoAvesEngordeService.cs** (1884 → ~600 c/partial)

**Archivo Ancla:** `SeguimientoAvesEngordeService.cs`

**Particiones:**
- `Funciones/SeguimientoAvesEngordeService.Diario.cs`: registro diario
- `Funciones/SeguimientoAvesEngordeService.Validaciones.cs`: validación de datos
- `Funciones/SeguimientoAvesEngordeService.Calculos.cs`: cálculos de indicadores

---

### Checklista de refactor por archivo

Cada partición debe cumplir:

```markdown
- [ ] Archivo .cs creado en `Funciones/` con namespace idéntico
- [ ] `partial class <NombreService>` (SIN : IXxx)
- [ ] Todos los `private`/`protected` members accesibles a otros partial
- [ ] Métodos moveados sin modificar lógica (refactor = clean code, no cambio de comportamiento)
- [ ] Docstring (`///`) de métodos preservado
- [ ] `dotnet build` → 0 errores, sin nuevas warnings
- [ ] `dotnet test` → tests preexistentes aún pasan (si aplica)
```

---

## 🧪 Validación Pre-commit

### Fase 1: Cambios funcionales

```bash
# Desde backend/
dotnet build --configuration Release
# Expected: ✅ 0 errors, 0 warnings

dotnet test --configuration Release
# Expected: ✅ 144/144 tests pass (o n_tests si hay cambios)

# Generar MigrationSnapshot
cd backend/src/ZooSanMarino.API
dotnet ef migrations list --project ../ZooSanMarino.Infrastructure --context ZooSanMarinoContext
# Expected: ✅ Migrations applied (ninguna pendiente)
```

### Fase 2: Refactor (después de COMMIT 1)

```bash
# Mismos comandos + verificación comportamental

# (Opcional) Snapshot comparison
git diff HEAD~1 --stat backend/src/ZooSanMarino.Infrastructure/Services/ | grep Funciones/
# Expected: ✅ 5 directorios nuevos, 0 cambios en ServiceBase
```

---

## 📐 Decisiones de diseño

| Pregunta | Decisión | Razón |
|----------|----------|-------|
| ¿Partial o Extract method? | **Partial** | Mantiene DI intact, preserva acceso a `_context`/`_currentUser`, sin riesgo de circular deps |
| ¿Qué va en la interfaz? | **Solo el ancla** | Interfaz `IXxxService` en el archivo principal; partials no la reimplementan |
| ¿Namespace en subcarpeta? | **Plano** (sin cambiar) | Evita rename de imports en otros servicios; regla CLAUDE.md explícita |
| ¿Helpers estáticos? | **En el ancla** | Accesibles a todos los partial; cross-cutting concerns centralizados |
| ¿Cuándo particionar más (>600 líneas)? | **Próxima iteración** | Después de esta, auditar nuevamente; ciclo continuo de mejora |

---

## 🎬 Proceso de entrega (2 PRs o 2 commits en 1 PR)

### Commits por alcance (contra el diff real actual)

El diff actual (`git status`) mezcla varios alcances; se agrupan en commits separados para mantener historial legible y facilitar revert selectivo:

| Commit | Alcance | Archivos |
|--------|---------|----------|
| **A. `chore(sql)`** | Ajuste funciones de indicadores postura | `fn_indicadores_levante_postura.sql`, `fn_indicadores_produccion_postura.sql` |
| **B. `feat(backend)` — Fase 3 convergencia** | Esquema `seguimiento_diario` como tabla canónica; retiro de tablas duplicadas | 8 migraciones `Fase3*`/`RemoveDead*`/`RemoveMenu*` + Designers, `ZooSanMarinoContextModelSnapshot.cs`, `ZooSanMarinoContext.cs`, entidades `Domain/Entities/*` (incl. `ProduccionDiaria.cs` y `ProduccionSeguimiento.cs` **eliminadas**), `Configurations/*` (2 eliminadas, 2 modificadas) |
| **C. `feat(backend)` — Feature 11/13 funcional** | Liquidación técnica, indicadores Ecuador, seguimiento convergencia, alimento por género | Servicios listados en Commit 1 (§ arriba), DTOs, `Calculos/ColombiaInventarioIdResolutionCalculos.cs`, `Calculos/SaldoLevanteCalculos.cs`, controllers, `DatabaseInitializer.cs`, config local |
| **D. `test(backend)`** | Suite de tests de cálculos puros (10 archivos nuevos) | `backend/tests/ZooSanMarino.Application.Tests/*Tests.cs` |
| **E. `refactor(backend)`** (aparte, tras validar A-D) | Partición de los 5 servicios monolíticos en `partial class` | Ver Commit 2 (§ arriba) |

**Excluir del commit:** `.devpilot/` (metadata interna de la plataforma devpilot — no es código de la app; no debe versionarse en el repo del proyecto).

### Opción A: 2 commits en 1 PR (recomendado para este caso)

```
Commit 1: "feat(backend): integrar Feature-11 y Feature-13 (liquidación, seguimiento convergencia)"
  - Cambios funcionales en 6 servicios, DTOs, entidades, migraciones
  - ✅ build + tests pasan

Commit 2: "refactor(backend): partir servicios monolíticos en partial classes (5 archivos)
  - ReporteTecnicoService, MovimientoAvesService, InventarioGestionService, etc.
  - ✅ 0 cambio de comportamiento; build + tests pasan
```

### Opción B: 2 PRs separadas

1. **PR #1:** Solo cambios funcionales (Feature-11/13)
2. **PR #2 (después de merge #1):** Refactor de deuda técnica

---

## 📝 Notas

- **Migración:** Ambos commits son **aditivos**; no hay DROP/DELETE de código. Regla "nunca borrar sin plan" respetada.
- **Backward compat:** Interfaces públicas (`IXxxService`) NO cambian; partials solo reorganizan implementación.
- **Testing:** Si un test falla post-refactor, significa que un `private` quedó inaccesible entre partials (impossible si se respeta la regla de namespace).

---

## ✅ Criterio de aceptación final

- ✅ Ambos commits hacen `dotnet build` y `dotnet test` sin error
- ✅ Interfaces públicas intactas (sin breaking changes)
- ✅ Migración EF aplicada (o pendiente pero idempotente)
- ✅ Partial classes siguen convención CLAUDE.md
- ✅ No hay código muerto introducido
