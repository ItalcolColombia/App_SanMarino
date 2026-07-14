# Plan — Módulo de Migraciones Masivas (Postura)

> Análisis técnico aprobado por el usuario (2026-07-12). Este archivo es el STEP 1 de CLAUDE.md; el estado vive en [`../tracker_estado.md`](../tracker_estado.md).

## Enfoque arquitectónico
Módulo **independiente** que orquesta la carga masiva por Excel **reusando** las reglas de negocio existentes. No modifica módulos actuales. Multi-tenant por `CompanyId` (helper `GetEffectiveCompanyIdAsync`, headers `X-Active-Company[-Id]`). Inserción **híbrida**: estructura vía servicios C# en transacción; históricos vía funciones plpgsql set-based con recálculo de agregados. Validación en dos fases (dry-run `/validar` → `/importar` all-or-nothing).

## Decisiones validadas
1. Inserción masiva **híbrida** (estructura=servicios C#; históricos=funciones SQL).
2. **Ventas = Aves + Huevos** (motivo/operación=Venta); Movimiento Aves/Huevos = traslados/retiros/ajustes no-venta.
3. Fases: **Estructura → Seguimientos → Ventas+Movimientos**.

## Archivos a crear (backend)
- `Domain/Entities/MigracionMasiva.cs` (+ `Persistence/Configurations/MigracionMasivaConfiguration.cs`, DbSet en `ZooSanMarinoContext`, migración EF idempotente).
- `Application/DTOs/Migracion/`: `MigracionResultDto`, `MigracionErrorDto`, `TipoMigracion` (enum), `raw/*Row` por tipo.
- `Application/Interfaces/`: `IMigracionService`, `IMigracionPlantillaService`, `IMigracionImportService`, `IMigracionValidacionService`, `IMigracionRepository`.
- `Infrastructure/Services/Migracion/`: `MigracionService.cs` (ancla partial) + `Funciones/MigracionService.Estructura.cs` + `Funciones/MigracionService.Historicos.cs` + `MigracionPlantillaService.cs` + `MigracionImportService.cs` + `MigracionValidacionService.cs` + `MigracionRepository.cs`.
- `Application/Calculos/MigracionCalculos.cs` (coerción pura) + tests `tests/ZooSanMarino.Application.Tests/MigracionCalculosTests.cs`.
- `API/Controllers/MigracionController.cs`.
- `backend/sql/fn_migracion_*.sql` (Fases 2-3) + migraciones EF que las embeben.
- DI en `API/Program.cs` (registrar servicios nuevos).

## Archivos a crear (frontend) — espejo `features/movimientos-pollo-engorde/`
- `features/migraciones-masivas/` con `migraciones-masivas-routing.module.ts`, `models/`, `funciones/`, `services/migracion.service.ts`, `pages/migraciones-masivas-page/`, `components/{selector-tipo-migracion,panel-plantilla-upload,reporte-errores-migracion}/`.
- Registrar ruta lazy en `app/app.config.ts` (`provideRouter`), antes del catch-all.
- Reusar `app-hierarchical-filter`, `ActiveCompanyService`/`company-selector`, `ToastService`, `ConfirmDialogService`, `shared/utils/format.ts`, patrón upload de `guia-genetica-admin`.
- Fila `role_menus` en BD (ruta `/migraciones-masivas`) — script SQL en `backend/sql/`.

## Cambios de BD/SQL
- Fase 0: tabla `migracion_masiva` (auditoría de corridas) vía migración EF idempotente.
- Fase 2-3: funciones plpgsql `fn_migracion_seguimiento_levante`, `_produccion`, `fn_migracion_ventas`, `fn_migracion_movimiento_aves`, `fn_migracion_movimiento_huevos` (INSERT…SELECT…WHERE NOT EXISTS + recálculo agregados), registradas por migración EF que embebe el SQL (convención del repo).
- Seed `role_menus`.

## Reglas de negocio (reuso, no duplicar)
- Estructura: `IFarmService/INucleoService/IGalponService.CreateAsync` (validan FK/empresa/duplicados; auto-`GalponId` `G0001…`; unique `(CompanyId,Name)`/`(CompanyId,GalponId)`).
- Elegibilidad históricos: Levante = existe Lote+LPL; Producción = Levante Cerrado+Liquidado + LPP.
- Históricos: insertar estado final + recomputar agregados **una vez** (no re-auto-procesar ni descuentos incrementales fila-a-fila).

## Casos de prueba
- Coerción pura (fechas serial Excel, números, IDs ocultos) — xUnit.
- `/validar`: FK inexistente, empresa ajena, duplicado in-file, duplicado BD, obligatorio faltante, formato/tipo/fecha inválidos, lote no elegible → devuelve errores, no inserta.
- `/importar`: 0 errores inserta y registra `migracion_masiva`; con errores no inserta.
- Estructura: side-effects correctos (UserFarm, GalponId auto), respeta uniques.
- Históricos: agregados recomputados == comportamiento incremental; **idempotencia** (re-import = 0 duplicados).
- Roles: admin elige empresa; usuario normal bloqueado a empresa activa.

## Verificación end-to-end
Back `:5002` + front `:4200`, BD `sanmarinoapplocal:5433`. Descargar plantilla → llenar → `/validar` (ver errores) → `/importar` → verificar filas + agregados en BD → re-import (idempotente). `dotnet build`/`dotnet test` + `yarn build`. `make down` al terminar.
