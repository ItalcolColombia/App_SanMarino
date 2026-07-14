# Tracker — Permisos de acceso a Migraciones Masivas (Postura / Pollo Engorde)

Plan: [migraciones_masivas_permiso_carga_masiva_plan.md](fase_de_desarrollo/migraciones_masivas_permiso_carga_masiva_plan.md)

---

## Paso 0 — Commit del backlog pendiente del módulo Migraciones Masivas

- [x] Revisar diff pendiente (mejoras 2026-07-13 + fixes descuento aves 2026-07-14) ya validado por memoria
- [x] `dotnet build` sanity (Infrastructure/Application)
- [x] `yarn build` sanity (frontend)
- [x] Commit del backlog pendiente (`af3ad69`)

## Backend — permisos nuevos

- [x] Migración EF idempotente `AddPermisosCargaMasivaMigracionesMasivas` (seed `carga_masiva_pollo_engorde` + `carga_masiva_postura`)
- [x] `dotnet build` 0 errores
- [x] `dotnet ef database update` local sin error (ids 59/60 verificados por psql)

## Frontend — gating de tiles

- [x] `funciones/agrupar-tipo-migracion.funcion.ts` — función pura `esTipoPolloEngorde`
- [x] `selector-tipo-migracion.component.ts` — inyectar `UserPermissionService`, `tienePermiso`, disabled+badge+guard de click
- [x] Estilos tile bloqueado (gris, cursor not-allowed)
- [x] `yarn build` 0 errores

## Validación

- [x] Smoke visual en navegador: NO realizado — requiere iniciar sesión y no ingreso credenciales
      (ni siquiera de dev) por regla operativa propia. Verificación quedó en: build limpio
      (back+front) + permisos confirmados en BD (ids 59/60) + revisión de código del template.
- [x] Reportar al usuario (incluye aviso de asignar permisos a roles tras el deploy + pendiente de smoke visual)

## Cierre

- [x] Commit de la feature de permisos (`354368f`)
