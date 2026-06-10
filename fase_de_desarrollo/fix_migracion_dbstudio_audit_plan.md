# Fix — Migración `AddDbStudioGrantsAndAudit` rompe el arranque (AWS + local)

## Síntoma
El backend crashea al arrancar (en AWS y reproducido en local con la BD de producción)
al aplicar la migración `20260607213501_AddDbStudioGrantsAndAudit`:

```
fail: 42703: column "created_at_utc" does not exist
CREATE INDEX IF NOT EXISTS ix_dbstudio_audit_created_at ON public.dbstudio_audit (created_at_utc);
```

Como el deploy aplica las migraciones al arrancar (`Database:RunMigrations=true`), la
excepción tumba el proceso → la tarea ECS nunca levanta (mismo patrón que el SIGSEGV histórico).

## Causa raíz (verificada contra la BD real de producción)
Producción **ya tiene** una tabla `public.dbstudio_audit` con un esquema **viejo/incompatible**,
creada por una iteración previa de DB Studio (anterior a esta migración):

| PROD (vieja)            | Código actual espera (entidad `DbStudioAudit` + `DbStudioAuditConfiguration`) |
|-------------------------|--------------------------------------------------------------------------------|
| `table_name` text       | `object_name` varchar(256)                                                     |
| `actor_user_id` **text**| `actor_user_id` **uuid**                                                        |
| `created_at` timestamptz| `created_at_utc` timestamptz                                                    |
| *(falta)*               | `success` boolean                                                              |
| *(falta)*               | `actor_email` varchar(256)                                                     |
| *(falta)*               | `company_id` integer NOT NULL                                                  |
| *(falta)*               | `ip_address` varchar(64)                                                       |

La migración usa `CREATE TABLE IF NOT EXISTS`, así que **no recrea** la tabla existente →
el índice posterior sobre `created_at_utc` (columna inexistente en la tabla vieja) falla →
toda la tanda de migraciones pendientes hace rollback y el proceso muere.

Hechos confirmados en la BD de prod (copiada a local, `localhost:5433` → PostgreSQL nativo):
- `dbstudio_audit` vieja está **vacía** (0 filas) y **sin** FKs/vistas dependientes.
- `dbstudio_object_grant` **no existe** (la migración la crea bien, de cero).
- `20260607213501` **no está** en `__EFMigrationsHistory` (última aplicada: `20260605024231`).

## Regla aplicada — EL CÓDIGO MANDA
La verdad es la entidad `DbStudioAudit` + `DbStudioAuditConfiguration.ToTable("dbstudio_audit")`,
que definen el esquema nuevo. La tabla vieja de prod debe converger a ese esquema.

## Enfoque
Editar **solo el `Up()`** de `20260607213501_AddDbStudioGrantsAndAudit` para auto-sanarse:
antes de crear índices, si existe una `dbstudio_audit` **sin** `created_at_utc` (marca inequívoca
del esquema viejo), eliminarla (está vacía y sin dependencias) y dejar que el
`CREATE TABLE IF NOT EXISTS` la reconstruya con el esquema correcto.

- Editar una migración **aún no aplicada** es seguro: EF rastrea solo la presencia del `migration_id`,
  no el hash del `Up()`. Esta migración nunca se aplicó con éxito en ningún entorno (siempre falla).
- En un entorno limpio (sin tabla vieja) el guard **no** elimina nada y el `CREATE TABLE IF NOT EXISTS`
  crea el esquema correcto directo. 100% idempotente y re-ejecutable.
- No se toca el `ModelSnapshot` (el esquema objetivo es idéntico) ni la entidad.

## Archivos
- `backend/src/ZooSanMarino.Infrastructure/Migrations/20260607213501_AddDbStudioGrantsAndAudit.cs`
  → agregar bloque de reconciliación al inicio del `Up()`. Sin otros cambios.

## Cambios de BD
Ninguno manual. La migración corregida se auto-aplica en el arranque/deploy.

## Tanda pendiente tras el fix (todas verificadas prod-safe)
1. `20260605182358_AddTicketCierreAdjuntos` — OK (ya se aplicó en el log)
2. `20260606031251_AddReaperturaToLoteReproductoraAveEngorde` — OK
3. `20260606031344_UpdateFnCruceReproductoraEngordeAgua` — OK
4. `20260606191515_SeedPermisosBotonesMovimientosPolloEngorde` — OK
5. `20260606224013_AddEsVentaMixtaToMovimientoPolloEngorde` — OK
6. `20260607213501_AddDbStudioGrantsAndAudit` — **corregida**
7. `20260608030455_ExtractLogoToLogoCompanias` — OK (`companies.logo_bytes`/`logo_content_type`
   existen en prod; `logo_companias` aún no) ⇒ HEAD

## Casos de prueba
1. `dotnet build` sin errores ni nuevas advertencias.
2. Arrancar el backend contra la copia de prod → aplica las 6 migraciones pendientes y levanta.
3. Re-arrancar → no-op, sin error (idempotencia).
4. Verificar esquema final de `dbstudio_audit` == entidad; índices `ix_dbstudio_audit_created_at`
   e `ix_dbstudio_audit_actor`; tabla `dbstudio_object_grant` + índice único `ux_dbstudio_grant_user_object`.
