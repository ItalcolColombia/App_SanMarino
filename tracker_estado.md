# Tracker — Commit del trabajo en curso + Reducción de deuda técnica backend

> **Plan:** [`fase_de_desarrollo/commit_y_deuda_backend_plan.md`](fase_de_desarrollo/commit_y_deuda_backend_plan.md)
> **Misión:** hay ~65 archivos backend tocados sin commitear (Fase 3 convergencia `seguimiento_diario`, Feature-11/13, tests nuevos). Se commitean por alcance y, aparte, se reduce la deuda técnica: 5 archivos de servicio superan las 800 líneas (gate de arquitectura).
> **Fuente única de verdad** del estado del desarrollo (regla CLAUDE.md § Workflow obligatorio). Se marca `[x]` solo cuando el paso se ejecutó y se verificó realmente — no se maquillan resultados.

---

## Fase 1 — Commit seguro del trabajo en curso

- [x] **Plan y tracker de la misión** — `commit_y_deuda_backend_plan.md` creado (enfoque commits por alcance + refactor 5 archivos >800 líneas); este tracker reescrito con checklist granular.

- [ ] **Auditoría del diff y filtro de secretos**
  - [ ] Revisar `git status`/`git diff --cached` completo (~65 archivos: sql, controllers, DTOs, entidades, migraciones, servicios, tests).
  - [ ] Confirmar que `appsettings.Development.json` y `launchSettings.json` no exponen credenciales nuevas (deben seguir apuntando a `zoo_sanmarino_db` local, sin secretos hardcodeados).
  - [ ] Confirmar que las 8 migraciones nuevas (`Fase3*`, `RemoveDeadProduccionDiariaEntity`, `RemoveMenuInventarioViejo`) son idempotentes (`IF NOT EXISTS` / `WHERE NOT EXISTS`).
  - [ ] Confirmar que `ProduccionDiaria.cs` y `ProduccionSeguimiento.cs` (entidades eliminadas) tienen migración de baja correspondiente y no quedan referencias huérfanas en `Configurations/` o servicios.
  - [ ] Excluir `.devpilot/` del staging (metadata interna de la plataforma, no es código de la app).

- [ ] **Validación de build pre-commit**
  - [ ] `dotnet build --configuration Release` (desde `backend/`) → 0 errores, 0 warnings nuevos.
  - [ ] `dotnet test --configuration Release` → suite completa en verde, incluyendo los 10 archivos de test nuevos en `backend/tests/ZooSanMarino.Application.Tests/`.
  - [ ] `dotnet ef migrations list` → sin pendientes tras aplicar en local.
  - [ ] Registrar en este tracker el resultado real (nº de tests, errores si los hubo) — no marcar `[x]` sin la salida del comando.

- [ ] **Crear commits agrupados por alcance** (ver detalle en el plan § "Commits por alcance")
  - [ ] `chore(sql): ajuste funciones indicadores postura` — `fn_indicadores_levante_postura.sql`, `fn_indicadores_produccion_postura.sql`.
  - [ ] `feat(backend): Fase 3 — seguimiento_diario como tabla canónica` — 8 migraciones + Designers, `ZooSanMarinoContextModelSnapshot.cs`, `ZooSanMarinoContext.cs`, entidades de dominio, `Configurations/*`.
  - [ ] `feat(backend): Feature 11/13 — liquidación, indicadores Ecuador, convergencia seguimiento` — servicios, DTOs, `Calculos/*`, controllers, `DatabaseInitializer.cs`, config local.
  - [ ] `test(backend): suite de cálculos puros` — 10 archivos nuevos en `Application.Tests`.
  - [ ] Verificar que ningún commit mezcla alcances (revisar `git show --stat` de cada uno antes de continuar).

---

## Fase 2 — Reducción de deuda técnica backend (5 archivos >800 líneas)

> Confirmado por `wc -l` (no por el escaneo automático de la plataforma, que listaba otro conjunto de archivos): estos 5 son los de mayor deuda real. Refactor vía `partial class` en `Funciones/` (CLAUDE.md), **sin cambiar comportamiento**. Cada archivo se refactoriza en un commit propio, después de que Fase 1 esté mergeada.

| # | Archivo | Líneas actuales | Meta (partial más grande) |
|---|---------|------------------|----------------------------|
| 1 | `Services/ReporteTecnicoService.cs` | 3110 | ~620 |
| 2 | `Services/MovimientoAvesService.cs` | 2507 | ~500 |
| 3 | `Services/InventarioGestionService.cs` | 2296 | ~500 |
| 4 | `Services/ReporteTecnicoProduccionService.cs` | 1953 | ~650 |
| 5 | `Services/SeguimientoAvesEngordeService.cs` | 1884 | ~600 |

- [ ] **1. `ReporteTecnicoService.cs`** → particiones `Funciones/ReporteTecnicoService.{Levante,Produccion,Reproduccion,Consolidacion,Mapeo}.cs`
  - [ ] Archivo ancla conserva: campos, ctor, interfaz `IReporteTecnicoService`, helpers estáticos.
  - [ ] `dotnet build` → 0 errores; namespace `ZooSanMarino.Infrastructure.Services` sin cambios.
  - [ ] Tests de reportes técnicos (si existen) en verde; comportamiento idéntico.

- [ ] **2. `MovimientoAvesService.cs`** → particiones `Funciones/MovimientoAvesService.{Traslados,Ventas,Descuentos,Huevos}.cs`
  - [ ] Descuentos en `SeguimientoDiario` (`UpsertSeguimientoLevanteAsync()`) no regresionan.
  - [ ] Traslados no duplican registros.
  - [ ] `dotnet test` → verde.

- [ ] **3. `InventarioGestionService.cs`** → particiones `Funciones/InventarioGestionService.{Crud,Movimientos,Stock,Busqueda}.cs`
  - [ ] Endpoints `/api/farms/{farmId}/inventory/...` sin cambio de contrato.
  - [ ] Reconciliación de stock correcta.

- [ ] **4. `ReporteTecnicoProduccionService.cs`** → particiones `Funciones/ReporteTecnicoProduccionService.{Diario,Semanal,Consolidacion}.cs`
  - [ ] `dotnet build` + `dotnet test` → verde.

- [ ] **5. `SeguimientoAvesEngordeService.cs`** → particiones `Funciones/SeguimientoAvesEngordeService.{Diario,Validaciones,Calculos}.cs`
  - [ ] Indicadores de engorde correctos (sin cambio de aritmética).
  - [ ] `dotnet build` + `dotnet test` → verde.

**Checklist por partición (aplica a cada archivo de arriba):**
```markdown
- [ ] Archivo .cs creado en Funciones/ con namespace idéntico
- [ ] `partial class <NombreService>` (SIN `: IXxx`, la interfaz queda solo en el ancla)
- [ ] Métodos movidos sin modificar lógica ni aritmética
- [ ] Docstrings (`///`) preservados
- [ ] `dotnet build` → 0 errores, sin warnings nuevos
```

---

## Fase 3 — Validación final y cierre

- [ ] **Build + suite completa + verificación del gate de líneas**
  - [ ] `dotnet build --configuration Release` → 0 errores, 0 warnings.
  - [ ] `dotnet test --configuration Release` → 100% verde.
  - [ ] Confirmar que ningún archivo backend supera 800 líneas (`wc -l` sobre `Services/*.cs` y `Controllers/*.cs`).
  - [ ] Interfaces públicas (`IXxxService`) sin cambios de firma (backward-compat).

- [ ] **Cierre: tracker y base de conocimiento**
  - [ ] Marcar todos los checklists reales de esta misión.
  - [ ] Registrar en memoria/knowledge base: qué se commiteó, qué quedó pendiente, decisiones de partición tomadas.

---

## 📐 Decisiones registradas

| Decisión | Razón |
|----------|-------|
| **Conjunto de 5 archivos a refactorizar** confirmado por `wc -l` real, no por el listado automático de la plataforma (que apuntaba a otro grupo de archivos, ej. `IndicadorEcuadorService`, `MovimientoAvesController`) | Regla CLAUDE.md "el código actual es la fuente de verdad"; se prioriza el mayor impacto real de reducción de deuda |
| **Partial vs Extract method** | Partial mantiene DI, acceso a `_context`; sin riesgo de dependencias circulares |
| **Interfaz solo en el ancla** | Evita reimplementación; patrón CLAUDE.md |
| **Namespace plano (sin subcarpeta)** | Regla explícita CLAUDE.md; sin breaking changes en imports |
| **Commits por alcance en vez de 1 solo commit funcional** | El diff real mezcla SQL, migración de esquema (Fase 3), lógica funcional y tests — separarlos facilita revert selectivo y revisión |
| **Refactor de deuda técnica en commit(s) aparte, después de Fase 1** | Evita mezclar "cambio de comportamiento" con "reorganización sin cambio de comportamiento" (regla CLAUDE.md) |

---

## 🎬 Próximos pasos

1. Ejecutar Fase 1 (auditoría → build/test real → commits por alcance).
2. Tras mergear Fase 1, iniciar Fase 2 archivo por archivo (o en paralelo si no hay conflictos de merge).
3. Cerrar con Fase 3 y actualizar la base de conocimiento.
