# Plan — Reporte Diario Costos (Pollo Engorde) + Lote Base global

**Fecha:** 2026-07-20 · **Solicitado por:** Moisés
**Fuente de datos:** seguimiento diario engorde existente (`seguimiento_diario_aves_engorde` vía `fn_seguimiento_diario_engorde`), sin duplicar lógica.

---

## 1. Qué se pide (mockup)

Reporte a nivel **granja** que unifica por **fecha** todos los lotes de pollo engorde en alcance:

- **Filtros:** Granja (obligatorio) · **Lote base** (opcional; sin lote base = TODOS los lotes de la granja) · Fecha inicio · Fecha fin · botón GENERAR.
- **Por fecha (fila):**
  - **Resumen global de alimento del día (kg):** desglose por tipo de alimento → `stock alimento` + `cantidad consumo`, y TOTAL del día (ej. 34kg + 5kg = 39kg).
  - **Mortalidad + Selección por galpón** (columnas dinámicas: GALPÓN 1..N).
  - **Aves vivas por galpón** (columnas dinámicas) + total.
- **Footer:** SUMA TOTAL (alimento) y SUMA TOTAL por galpón (mort+sel).
- **Cabecera:** lotes involucrados (ej. galpón 1 con lotes `84A` y `84B` → "lote nombre", "lote nombre 2") y **aves vivas actuales**.
- **Regla del segundo lote:** si no se da fecha inicio, el reporte arranca en la **fecha de encaset del lote más reciente** del alcance ("si llega un segundo lote se toma la fecha del segundo lote como inicio del reporte").

### 1b. Lote Base global (nuevo concepto)

- En postura el "lote base" es un prefijo de nombre (letras A–F por galpón). Para engorde se pide un **catálogo explícito**: entidad `lote_base_engorde` a nivel **empresa**, que se **amarra opcionalmente** a los lotes de engorde (`lote_ave_engorde.lote_base_engorde_id`).
- **No obligatorio** al crear/editar el lote engorde (el flujo actual no cambia si no se usa).
- Pedir "lote base 95 de la granja 1" → trae todos los lotes de esa granja con lote base 95 (todos los galpones) y su acumulado completo.

---

## 2. Enfoque arquitectónico

**Principio:** el reporte NO reimplementa aritmética. Reutiliza `fn_seguimiento_diario_engorde(p_lote_id)` (v8: apertura, cierre efectivo, ventas, mort caja, saldo Lindley) vía `LATERAL` por cada lote del alcance y **agrega en la BD** (regla CLAUDE.md: la BD filtra/agrupa; el backend orquesta). Así los números cuadran 1:1 con la pantalla de seguimiento por lote.

```
Angular (página nueva) ── POST /api/ReporteDiarioCostosEngorde/generar
        │                          │
        │                          ▼
        │        ReporteDiarioCostosEngordeService (delgado)
        │                          │  SqlQueryRaw
        │                          ▼
        │        fn_reporte_diario_costos_engorde(company, granja, lote_base, f_ini, f_fin)
        │                          │  por lote: LATERAL fn_seguimiento_diario_engorde(id)
        │                          ▼
        │        1 fila por FECHA + jsonb (alimentos, galpones)
        └── columnas dinámicas por galpón + export Excel (helpers shared)
```

- **Alimentos por día:** desde `historico_consumo_alimento` jsonb (`[{nombre_alimento, saldo_inicial, consumo, saldo_final, unidad_medida}]`). `consumo` = SUM por alimento sobre todos los lotes; `stock` = SUM por galpón del **último** `saldo_final` del alimento en esa fecha (último snapshot por galpón+ítem, evita doble conteo entre lotes que comparten bodega). Fallback filas viejas sin histórico: `tipo_alimento` + `consumo_dia_kg`, stock NULL.
- **Mort+Sel por galpón:** SUM de `mortalidad_h+m + sel_h+m` (err. sexaje se expone aparte en el jsonb para detalle).
- **Aves vivas por galpón/fecha:** carry-forward del último `saldo_aves` de cada lote con fecha ≤ d (lateral), sumado por galpón. "Aves vivas actuales" = fila de la última fecha.
- **Totales footer:** cálculo **puro** en `Application/Calculos/ReporteDiarioCostosEngordeCalculos.cs` (+ xUnit), no en SQL ni en el componente.

## 3. Archivos a crear / modificar

### Backend
| Acción | Archivo |
|---|---|
| ➕ | `Domain/Entities/LoteBaseEngorde.cs` (AuditableEntity: company, nombre, descripción) |
| ✏️ | `Domain/Entities/LoteAveEngorde.cs` → `LoteBaseEngordeId int?` + nav |
| ➕ | `Infrastructure/Persistence/Configurations/LoteBaseEngordeConfiguration.cs` |
| ✏️ | `Infrastructure/Persistence/Configurations/LoteAveEngordeConfiguration.cs` (columna+FK+índice) |
| ✏️ | `ZooSanMarinoContext` → `DbSet<LoteBaseEngorde>` |
| ➕ | `Application/DTOs/LoteBaseEngorde/LoteBaseEngordeDtos.cs` (list/create/update) |
| ✏️ | `Application/DTOs/CreateLoteAveEngordeDto.cs`, `UpdateLoteAveEngordeDto.cs`, `LoteAveEngorde/LoteAveEngordeDetailDto.cs` (campo opcional al final) |
| ➕ | `Application/Interfaces/ILoteBaseEngordeService.cs` · `Infrastructure/Services/LoteBaseEngordeService.cs` · `API/Controllers/LoteBaseEngordeController.cs` |
| ✏️ | `Infrastructure/Services/LoteAveEngordeService.cs` (create/update/detail: mapear lote base + validar empresa) |
| ➕ | `Application/DTOs/ReporteDiarioCostosEngordeDtos.cs` |
| ➕ | `Application/Calculos/ReporteDiarioCostosEngordeCalculos.cs` (totales puros) |
| ➕ | `Application/Interfaces/IReporteDiarioCostosEngordeService.cs` · `Infrastructure/Services/ReporteDiarioCostosEngordeService.cs` · `API/Controllers/ReporteDiarioCostosEngordeController.cs` |
| ✏️ | `API/Program.cs` → DI de los 2 servicios nuevos |
| ➕ | `backend/sql/fn_reporte_diario_costos_engorde.sql` |
| ➕ | Migraciones EF: `AddLoteBaseEngorde` (tabla+columna, idempotente) · `AddFnReporteDiarioCostosEngorde` (CREATE OR REPLACE) · `AddMenuReporteDiarioCostosEngorde` (menús/roles/empresas, hereda de `/informe-semanal-engorde`) |
| ➕ | `tests/ZooSanMarino.Application.Tests/ReporteDiarioCostosEngordeCalculosTests.cs` |

### Frontend
| Acción | Archivo |
|---|---|
| ➕ | `features/engorde-comun/services/lote-base-engorde.api.ts` (CRUD lote base, compartido) |
| ✏️ | `features/lote-engorde/components/lote-engorde-list/*` → select "Lote base (opcional)" + crear rápido |
| ✏️ | `features/lote-engorde/services/lote-engorde.service.ts` → `loteBaseEngordeId`/`loteBaseNombre` en DTOs |
| ➕ | `features/reporte-diario-costos-engorde/models/reporte-diario-costos.model.ts` |
| ➕ | `features/reporte-diario-costos-engorde/services/reporte-diario-costos-engorde.service.ts` |
| ➕ | `features/reporte-diario-costos-engorde/funciones/exportar-reporte-costos-excel.funcion.ts` (pura, AOA multi-header) |
| ➕ | `features/reporte-diario-costos-engorde/pages/reporte-diario-costos-engorde-main/*` (ts/html/scss, OnPush, referencias estables) |
| ✏️ | `app.config.ts` → ruta `/reporte-diario-costos-engorde` con `authGuard` |

## 4. Cambios de BD (idempotentes, se aplican solos en deploy)

```sql
-- 1) Catálogo lote base
CREATE TABLE IF NOT EXISTS lote_base_engorde (
  id            serial PRIMARY KEY,
  company_id    int NOT NULL,
  nombre        varchar(80)  NOT NULL,
  descripcion   varchar(300) NULL,
  created_by_user_id int NULL, created_at timestamptz NOT NULL DEFAULT timezone('utc', now()),
  updated_by_user_id int NULL, updated_at timestamptz NULL, deleted_at timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_lote_base_engorde_company_nombre
  ON lote_base_engorde (company_id, LOWER(nombre)) WHERE deleted_at IS NULL;

-- 2) Amarre opcional en lote engorde
ALTER TABLE lote_ave_engorde ADD COLUMN IF NOT EXISTS lote_base_engorde_id int NULL;
CREATE INDEX IF NOT EXISTS ix_lote_ave_engorde_lote_base ON lote_ave_engorde (lote_base_engorde_id);
-- FK vía DO $$ ... $$ (constraint sin IF NOT EXISTS nativo)

-- 3) fn_reporte_diario_costos_engorde(p_company_id, p_granja_id, p_lote_base_id, p_fecha_inicio, p_fecha_fin)
--    RETURNS TABLE(fecha, consumo_total_kg, mort_sel_total, aves_vivas_total, alimentos TEXT(json), galpones TEXT(json))
```

## 5. Reglas de negocio

1. **Alcance de lotes:** empresa efectiva + granja (validada contra granjas asignadas al usuario) + lote base opcional; `deleted_at IS NULL`; incluye lotes cerrados (histórico).
2. **Sin lote base seleccionado → todos** los lotes de la granja (tengan o no lote base).
3. **Fecha inicio por defecto** = `MAX(fecha_encaset)` del alcance (regla del segundo lote). Fecha fin por defecto = hoy. Explícitas ganan.
4. **Unifica por fecha:** universo de fechas = unión de las fechas devueltas por `fn_seguimiento_diario_engorde` de cada lote dentro del rango.
5. **Aves vivas** por galpón/fecha = carry-forward del último `saldo_aves` por lote (≤ fecha), sumado por galpón; lote sin filas aún → 0. "Actuales" = última fecha del reporte.
6. **Alimento:** consumo = suma de `consumo` del histórico jsonb por `nombre_alimento`; stock = suma por galpón del último `saldo_final` de esa fecha; fallback filas sin histórico → `tipo_alimento` + consumo del día (stock NULL, sin inventar datos).
7. **Lote base:** nombre único por empresa (case-insensitive, vivos); borrar = soft-delete **bloqueado** si tiene lotes activos amarrados; asignación opcional en create/edit del lote engorde, validando que pertenezca a la empresa efectiva.
8. **Refactor ≠ cambio de comportamiento:** no se toca `fn_seguimiento_diario_engorde` ni el CRUD existente más allá del campo nuevo opcional.

## 6. Casos de prueba

**xUnit (`ReporteDiarioCostosEngordeCalculosTests`):**
- Totales: suma consumo global, suma mort+sel por galpón, suma por alimento; redondeos estables; lista vacía → totales en 0 sin excepción.
- Agregación de alimentos con nombres repetidos entre galpones (merge) y fallback `tipo_alimento`.
- Aves vivas actuales = última fecha; galpones sin datos en una fecha → 0.

**Integración manual (local, BD :5433):**
- Granja con 2+ lotes en galpones distintos y lote base asignado → generar con/sin lote base; comparar consumo/saldo_aves contra pantalla de seguimiento por lote (deben cuadrar exacto).
- Sin fechas → arranca en encaset del lote más reciente.
- CRUD lote base: crear duplicado (409/400), borrar con lotes amarrados (bloqueado), crear desde el form de lote engorde.
- `dotnet ef database update` local sin error (migraciones idempotentes re-ejecutables).

**Validación:** `cd backend && dotnet build && dotnet test` · `cd frontend && yarn build` (0 errores; solo warning de bundle budget preexistente).

---

## 7. EXTENSIÓN (2026-07-21) — Gestión de lotes base + permisos

**Pedido:** botón "Lotes base" en la página de Lote Aves de Engorde que abre un **modal de gestión** (lista + crear/editar/eliminar) con campos nuevos **Código ERP** y **Línea genética**; todo gateado por permisos (convive con Ecuador, que no lo usa → sin permiso no se ve nada).

**Permisos (convención `modulo.accion`, como `movimientos_pollo_engorde.vender_lotes_cerrados`; seed en migración, asignación a roles desde el módulo Roles y Permisos):**
- `lote_base_pollo_engorde.ver` → muestra el botón "Lotes base" (modal en modo lectura) **y** el campo "Lote base" en el form de crear/editar lote engorde.
- `lote_base_pollo_engorde.crear` → form de creación en el modal + crear rápido inline en el form del lote.
- `lote_base_pollo_engorde.editar` → acción editar en la lista del modal.
- `lote_base_pollo_engorde.eliminar` → acción eliminar (ConfirmDialogService; el backend ya bloquea si tiene lotes amarrados).
- Gate 100% en frontend (`*appHasPermission`), igual que el precedente de venta lotes cerrados; backend queda `[Authorize]`.

**Cambios:**
| Capa | Cambio |
|---|---|
| Domain/Config | `LoteBaseEngorde.CodigoErp` (80) + `LineaGenetica` (120) → columnas `codigo_erp`, `linea_genetica` |
| DTOs/Service | list/create/update con los 2 campos nuevos |
| Migración | `AddLoteBaseEngordeCamposYPermisos`: ALTER TABLE ADD COLUMN IF NOT EXISTS ×2 + seed idempotente de los 4 permisos |
| Front api | `lote-base-engorde.api.ts`: DTO + payloads con `codigoErp`/`lineaGenetica` |
| Front página | Botón "Lotes base" (header, gate ver) + modal `le-modal` con tabla (Nombre/ERP/Línea genética/Lotes asignados/acciones) y form inline crear/editar; eliminar con `ConfirmDialogService.ask` |
| Front form lote | Campo "Lote base (opcional)" envuelto en gate ver; crear rápido inline en gate crear |

**Casos de prueba:** sin permisos → ni botón ni campo (Ecuador intacto); ver solo → lista sin acciones; crear/editar/eliminar según permiso; eliminar con lotes amarrados → toast de error del backend; duplicado → 400 con mensaje.

---

## 8. EXTENSIÓN (2026-07-21 b) — Panamá: lote base obligatorio + tab + vigencia por año

**Pedido:**
1. **Panamá:** al crear un lote de engorde, el campo **"Nombre lote" pasa a ser un select de los lotes base vigentes** → el nombre del lote = nombre del lote base elegido y queda amarrado (`loteBaseEngordeId`). Obligatorio (debe existir un lote base para poder crear el lote). **Ecuador conserva la vista actual** (input libre + campo opcional gateado por permiso).
2. **Panamá:** la gestión de lotes base pasa del modal a un **tab** en la página Lote Aves de Engorde ("Lotes de engorde" | "Lotes base"). Ecuador sigue con el botón+modal actual.
3. **Lote base gana `fecha_activacion` (date) y `activo` (bool, default true):** vigente = activo **y** (sin fecha o año(fecha_activacion) = año en curso). Un lote base 2026 aparece todo 2026; en 2027 deja de aparecer. Desactivar es **manual** (toggle en la gestión). El select de crear-lote solo muestra **vigentes**; la gestión y el filtro del reporte muestran todos.

**Decisiones:**
- Obligatoriedad **100% frontend** (`CountryFilterService.isPanama()` + validador required en el select): `PuentePanamaService` crea lotes vía `ILoteAveEngordeService.CreateAsync` y una validación dura en backend rompería la sincronización automática. Mismo patrón del repo (permisos/reglas de venta 100% front).
- `FechaActivacion` = `DateTime?` con columna **`date`** (convención ImplementacionPlan.FechaInicio); persistir `.Date` con Kind Unspecified (gotcha Npgsql date). Vigencia comparada por **año** (`.Year == año actual`).
- Backend: `GET /api/LoteBaseEngorde?soloVigentes=true` (filtro en BD) + `PUT /api/LoteBaseEngorde/{id}/activo` (toggle manual).
- Migración `AddLoteBaseEngordeActivacion`: `ADD COLUMN IF NOT EXISTS fecha_activacion date NULL` + `activo boolean NOT NULL DEFAULT true`.
- Front: gestión extraída a `ng-template` reutilizado por el modal (Ecuador) y el tab (Panamá); tab bar solo Panamá con gate `ver` para el tab de lotes base.

**Casos de prueba:** Panamá sin lotes base vigentes → select vacío con hint y submit bloqueado; elegir lote base setea nombre+amarre; lote base inactivo o de año pasado no aparece en el select pero sí en gestión y reporte; Ecuador: sin cambios de comportamiento; toggle activo/inactivo con toasts.
