# Plan: Corregir migración de clientes para producción

## Objetivo
Crear una nueva migración de EF Core que garantice en producción la existencia de la tabla `clientes`, los índices asociados y el menú `/config/clientes`, tal como se corrigió localmente con un parche SQL.

## Alcance
- Generar una migración nueva en `backend/src/ZooSanMarino.Infrastructure/Migrations`.
- La migración debe ser idempotente y segura para ambientes donde el historial (`__EFMigrationsHistory`) ya pueda marcar `AddGestionClientes` como aplicada.
- No modificar la migración histórica existente `20260517135042_AddGestionClientes`.

## Técnica
1. Crear migración EF Core vacía/semántica con nombre `FixMissingClientesTableAndMenu`.
2. Editar el archivo generado para incluir:
   - `CREATE TABLE IF NOT EXISTS public.clientes (...)` con las mismas columnas e índices que la migración original.
   - SQL para insertar el menú `/config/clientes` si no existe.
   - SQL para asignar el menú a `role_menus` y `company_menus` de forma guardada.
3. Usar `migrationBuilder.Sql(...)` en `Up()` y `Down()` para operaciones de corrección.
4. Probar localmente con `dotnet ef database update` y validar con `psql`.

## Archivos a crear/modificar
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<timestamp>_FixMissingClientesTableAndMenu.cs`
- `backend/src/ZooSanMarino.Infrastructure/Migrations/<timestamp>_FixMissingClientesTableAndMenu.Designer.cs`
- `tracker_estado.md`

## Verificación
- `dotnet build` en `backend/src/ZooSanMarino.API` debe pasar.
- `dotnet ef migrations add FixMissingClientesTableAndMenu ...` debe generar la migración.
- La migración debe aplicarse en local sin errores.
- La tabla `public.clientes` debe existir y `menus` debe incluir `/config/clientes`.
- El archivo de migración debe contener SQL con `IF NOT EXISTS` y no depender de la ausencia de otras migraciones.

## Riesgos mitigados
- Si `__EFMigrationsHistory` cree que `AddGestionClientes` ya se aplicó, esta migración hará la corrección de esquema y datos faltantes.
- Si la tabla ya existe, la migración no fallará gracias a `IF NOT EXISTS`.
- El menú ya presente no se duplicará gracias a comprobaciones con `WHERE NOT EXISTS`.
