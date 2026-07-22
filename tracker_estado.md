# Tracker — Panamá: lote base obligatorio + tab de gestión + vigencia por año

**Plan:** [fase_de_desarrollo/reporte_diario_costos_engorde_plan.md](fase_de_desarrollo/reporte_diario_costos_engorde_plan.md) (sección 8)

## Backend
- [x] `LoteBaseEngorde`: + `FechaActivacion` (DateTime?, columna date) + `Activo` (bool default true) + config
- [x] DTOs: fecha/activo en list y create/update + `SetActivoLoteBaseEngordeDto`
- [x] Service: `GetAllAsync(soloVigentes)` (activo + año actual en BD), mapeo fecha (Kind Unspecified), `SetActivoAsync`
- [x] Controller: `GET ?soloVigentes=true` + `PUT {id}/activo`
- [x] Migración `AddLoteBaseEngordeActivacion` (ADD COLUMN IF NOT EXISTS ×2)
- [x] Build API (output alterno por backend corriendo) 0 errores + tests 527/527

## Frontend
- [x] api: `fechaActivacion`/`activo`, `getAll(soloVigentes)`, `setActivo`
- [x] `lote-engorde-list`: `esPanama` (CountryFilterService), tabs "Lotes de engorde" | "Lotes base" (solo Panamá, gate ver), gestión extraída a ng-template compartido con el modal (Ecuador)
- [x] Gestión: campo Fecha de activación (requerido) + columna Activación/Estado + toggle activar/desactivar (gate editar)
- [x] Form crear lote (Panamá): "Nombre lote" = select de lotes base VIGENTES (required, setea nombre + loteBaseEngordeId); campo "Lote base (opcional)" solo Ecuador
- [x] `yarn build` verde (solo warning de bundle budget preexistente)

## Validación pendiente (usuario)
- [ ] `dotnet ef database update` local (BD :5433) — aplica también las 4 migraciones de la fase anterior
- [ ] Smoke Panamá: crear lote base con fecha 2026 → aparece en el select de crear lote; desactivarlo o fecharlo 2025 → desaparece; Ecuador intacto
