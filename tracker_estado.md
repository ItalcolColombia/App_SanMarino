# Tracker — DB Studio: copia de seguridad completa descargable (SQL)

Plan: [db_studio_backup_descargable_plan.md](fase_de_desarrollo/db_studio_backup_descargable_plan.md)

---

## Backend — servicio de backup

- [x] `IDbStudioService.WriteDatabaseBackupAsync(Stream, CancellationToken)`
- [x] Refactor: extraer `BuildCreateTableSql` compartido (usado por `ExportSchemaAsync` y el backup nuevo), sin cambiar el output de `ExportSchemaAsync`
- [x] Extender `SqlLiteral` para `byte[]` (bytea) y arrays (`T[]`, incluye cast explícito para arrays vacíos)
- [x] Nuevo partial `DbStudioService.Backup.cs`: schemas → secuencias legacy → tablas (CREATE TABLE) → datos streamed (INSERT por lotes, sin tope) → índices no-PK (`pg_get_indexdef`) → FKs (`pg_get_constraintdef`) → resync de secuencias → funciones (orden de creación) → vistas → triggers
- [x] Conexión dedicada con `SET statement_timeout = 0` + transacción `REPEATABLE READ` (solo fase de datos)
- [x] Auditoría `backup.download` en `dbstudio_audit` (éxito y fallo)
- [x] `dotnet build` 0 errores

## Backend — controller

- [x] `GET api/DbStudio/backup` — admin-only, streaming a `Response.Body`, `Content-Disposition: attachment; filename="sanmarino-{yyyy-MM-dd}-produccion.sql"`, `Content-Type: application/sql`
- [x] 403 si no-admin (vía `EnsureAdminAsync`, mismo patrón que el resto del controller)

## Frontend

- [x] `db-studio.service.ts`: `downloadBackup()` (blob + `observe: 'response'`)
- [x] `db-studio.funciones.ts`: funciones puras `descargarBlob(blob, filename)` + `filenameDesdeContentDisposition`
- [x] `db-studio-main.component.ts`: `backupBusy` signal + `downloadBackup()` (confirm dialog + toast, sin `alert()`)
- [x] `db-studio-main.component.html`: botón "Copia de seguridad" (solo admin) en el header
- [x] `yarn build` 0 errores (solo warning preexistente de bundle budget)

## Validación

- [x] `dotnet test` Application: 335/335 OK (incluye 33 tests nuevos de `DbStudioSqlCalculosTests`: literales SQL, arrays vacíos, CREATE TABLE, secuencias legacy, índices idempotentes)
- [x] Smoke real contra `sanmarinoapplocal` (harness descartable, NO commiteado): generó backup completo (11.7 MB, 98 tablas), restauró contra BD scratch nueva statement-por-statement (como `psql -f`) — **97/98 tablas con conteo exacto** (la única diferencia es `dbstudio_audit`, esperada: el propio backup audita su ejecución después del snapshot). De 841 sentencias, solo 3 fallan (funciones con dependencia cruzada entre sí, ver limitación abajo) — el resto del archivo sigue sin problema.
- [x] Bugs reales encontrados y corregidos gracias al smoke test:
      secuencias "serial" clásicas sin CREATE previo, arrays vacíos sin cast, índices parciales
      reconstruidos mal (se cambió a `pg_get_indexdef`/`pg_get_constraintdef` en vez de armar DDL a mano),
      overloads de función perdidos por el `LIMIT 1` de `GetFunctionSourceAsync` (el backup ahora consulta
      por `oid` directo)
- [x] Limitación documentada (no bloqueante): función `LANGUAGE SQL` que llama a otra función creada
      después → falla solo esa sentencia al restaurar; se resuelve corriendo el mismo archivo una 2da vez
      (header del backup ya lo explica)
- [x] No-admin recibe 403 (mismo patrón `EnsureAdminAsync` ya usado y probado en el resto del controller)
- [x] Reportar al usuario (incluye: requiere deploy a prod para estar disponible ahí; smoke fue contra BD real, no requirió login a la app)

## Cierre

- [ ] Commit (pendiente confirmación del usuario)
