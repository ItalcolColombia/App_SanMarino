# Estado — Fix migración `AddDbStudioGrantsAndAudit` (arranque AWS/local)

Plan: [fix_migracion_dbstudio_audit_plan.md](./fase_de_desarrollo/fix_migracion_dbstudio_audit_plan.md)

## Diagnóstico
- [x] Reproducir error en local contra la BD de producción
- [x] Identificar que la app usa el PostgreSQL **nativo** de Windows en `localhost:5433` (no el contenedor Docker, que está vacío)
- [x] Confirmar esquema viejo/incompatible de `public.dbstudio_audit` en prod (sin `created_at_utc`, etc.)
- [x] Confirmar tabla vieja **vacía** (0 filas) y **sin** FKs/vistas dependientes
- [x] Confirmar que `20260607213501` no está en `__EFMigrationsHistory` (última: `20260605024231`)
- [x] Auditar el resto de la tanda pendiente (incl. `ExtractLogoToLogoCompanias`) → prod-safe

## Implementación
- [x] Editar `Up()` de `20260607213501_AddDbStudioGrantsAndAudit.cs` con reconciliación idempotente
- [x] `dotnet build` sin errores ni nuevas advertencias (4 warnings preexistentes, ajenos)
- [x] El fix de `dbstudio_audit` **resuelve el error original** (la migración avanza más allá del `created_at_utc`)
- [x] Decisión del usuario: **dejar solo el fix de código**, sin GRANT local. Validación de la cadena completa vía deploy.
- [ ] (Deploy) Mergear a `main-produccion` → el deploy aplica las 6 migraciones al arrancar; verificar post-deploy (sección 🚀 del CLAUDE.md)

## Hallazgos importantes
- **EF Core corre toda la tanda pendiente en UNA transacción** → si una migración falla, revierte TODO. No quedan estados a medias.
- **Producción NO fue modificada** (el rollback transaccional revierte todo, igual que en local).
- ⚠️ **`appsettings.json` base apunta a PROD RDS** (`repropesa01@reproductoras-pesadas...`). `dotnet ef` sin `ASPNETCORE_ENVIRONMENT=Development` usa ese archivo → riesgo de pegarle a prod. Para validar local: forzar `Development` + `--connection` local explícita.
- ⛔ **Segundo bloqueante (solo LOCAL):** la migración `20260608030455_ExtractLogoToLogoCompanias` falla con `42501: permission denied for schema public` en el chequeo de la FK. Causa: `companies`/`logo_companias` son propiedad de `repropesa01`, y el restore del dump custom **no trae los permisos de schema** de ese rol. En prod `repropesa01` es el usuario de la app (tiene USAGE/CREATE sobre `public`) → allá la migración corre bien.
- Para validar la cadena completa en local hace falta replicar ese permiso: `GRANT USAGE, CREATE ON SCHEMA public TO repropesa01;` (solo en la copia local). Pendiente de tu OK.
