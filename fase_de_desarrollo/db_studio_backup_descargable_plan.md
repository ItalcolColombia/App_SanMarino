# DB Studio — Copia de seguridad completa descargable (SQL)

> Plan aprobado. Extiende el módulo `db_studio` (backend `ZooSanMarino.Infrastructure/Services/DbStudio/` + frontend `features/db-studio/`) ya existente. No reemplaza nada de lo ya construido (esquemas, tablas, export por tabla/esquema, permisos, concurrencia).

## Contexto y objetivo

El usuario admin necesita generar y **descargar** una copia de seguridad completa de la base de datos (estructura + datos) en formato **SQL plano**, sin tener que entrar a herramientas externas (pgAdmin, consola AWS, etc.) para sacar backups manuales. Requisito explícito de nombre de archivo: `sanmarino-{fecha-actual}-produccion.sql` (fecha `yyyy-MM-dd`, hora servidor UTC).

Ya existen `ExportTableAsync` (una tabla → SQL/CSV/JSON, tope `MaxExportRows`) y `ExportSchemaAsync` (solo `CREATE TABLE` de un esquema, sin datos). Ninguno sirve como backup real: falta cubrir **todas** las tablas con **todos** los datos, sin tope de filas, más índices/FKs/vistas/funciones/triggers para que el archivo sea restaurable con `psql -f archivo.sql`.

### Decisiones de alcance
- **Incluye:** todas las tablas base (todos los esquemas no-sistema) con columnas/tipos/`NOT NULL`/`DEFAULT`/`IDENTITY`/PK inline; **todas** las filas como `INSERT` por lotes (streaming, sin tope); índices no-PK; foreign keys (agregadas *después* de los datos, como hace `pg_dump`, para no depender del orden de inserción); resync de secuencias (`setval` vía `pg_get_serial_sequence`, cubre tanto `IDENTITY` como `serial` clásico); vistas, funciones y triggers al final (best-effort).
- **No incluye (limitación documentada, no bloqueante):** roles/grants de Postgres, `CREATE EXTENSION`, comentarios de columna/tabla, `CHECK` constraints, particionado avanzado. La estructura completa igual vive en las migraciones EF (git) — lo irremplazable es el **dato**, que sí queda 100% cubierto.
- **Autorización:** solo admin (`_authz.EnsureAdminAsync()`), igual que `sql/execute`. Se audita en `dbstudio_audit` (acción `backup.download`) igual que el resto del módulo.
- **Streaming obligatorio:** nada de acumular todo en un `byte[]` en memoria. Se escribe directo a `Response.Body` a medida que se lee cada tabla, para (a) no reventar memoria del contenedor ECS con DBs grandes y (b) evitar que el ALB corte la conexión por inactividad (idle timeout) — mientras haya bytes fluyendo, no hay timeout.
- **`statement_timeout`:** el pool de DB Studio aplica 30s por sesión (`DbStudioOptions.StatementTimeoutSeconds`). Un backup completo puede tardar más — se abre una conexión dedicada y se hace `SET statement_timeout = 0` solo para esa sesión de backup (no toca el límite global de otras operaciones del módulo).
- **Snapshot consistente:** todo el backup corre dentro de una única transacción `REPEATABLE READ` (read-only) para que las FKs entre tablas queden coherentes aunque el backup tarde varios minutos.

## Backend — cambios

1. **`IDbStudioService`**: nuevo método `Task WriteDatabaseBackupAsync(Stream output, CancellationToken ct = default)`.
2. **Nuevo partial** `Infrastructure/Services/DbStudio/Funciones/DbStudioService.Backup.cs`:
   - Abre conexión dedicada (`_rt.OpenReadAsync` + `SET statement_timeout = 0`) + transacción `REPEATABLE READ`.
   - Refactor menor: extraer el armado de `CREATE TABLE ... (columnas + PK inline)` que hoy vive inline en `ExportSchemaAsync` (`ExportImport.cs`) a un helper privado compartido `BuildCreateTableSql(schema, table, columns)`, reusado por ambos (sin cambiar el output de `ExportSchemaAsync` — refactor puro).
   - Por cada esquema no-sistema (de `GetSchemasAsync`): `CREATE SCHEMA IF NOT EXISTS` si no es `public`.
   - Por cada tabla base/particionada: `CREATE TABLE IF NOT EXISTS` (helper de arriba).
   - Por cada tabla: datos vía `NpgsqlDataReader` (misma conexión/transacción), sin `ReadAllAsync` (que bufferea todo) — lee fila a fila y emite `INSERT` multi-fila en lotes (~500 filas) directo al `StreamWriter`, sin materializar la tabla completa en memoria.
   - Después de cargar **todos** los datos: índices no-PK (`CREATE INDEX`/`CREATE UNIQUE INDEX`) + FKs (`ALTER TABLE ... ADD CONSTRAINT ... FOREIGN KEY`) de todas las tablas.
   - Resync de secuencias: para columnas con `IsIdentity` o `Default` con `nextval(`, emitir `SELECT setval(pg_get_serial_sequence('"schema"."tabla"','columna'), coalesce(max(...),1), max(...) is not null) FROM ...;`.
   - Funciones (`GetFunctionsAsync`/`GetFunctionSourceAsync`, ya existen) → vistas (`GetViewsAsync`/`GetViewDefinitionAsync`, ya existen; `CREATE OR REPLACE VIEW` normal, `DROP MATERIALIZED VIEW IF EXISTS` + `CREATE MATERIALIZED VIEW` para las materializadas) → triggers (nuevo: `pg_trigger` + `pg_get_triggerdef`, excluyendo `tgisinternal`). Se anota en el header del archivo que el orden entre vistas/funciones con dependencias cruzadas no está resuelto (best-effort).
   - Extender `SqlLiteral` (hoy en `ExportImport.cs`, usado también por `ExportTableAsync`) para manejar `byte[]` (bytea → literal hex `E'\\x...'`) y arrays (`T[]` → literal `ARRAY[...]`) — hoy esos dos casos caen al fallback `Convert.ToString` y generan SQL inválido; se corrige de paso porque el backup lo necesita para no romperse en la primera columna `bytea`/array que aparezca.
   - Auditoría: un `AuditAsync("backup.download", null, null, "-- full backup --", success, new { tables = N, rows = M }, ct)` al finalizar (o al fallar).
3. **Controller** `DbStudioController`: nuevo `GET api/DbStudio/backup` — `EnsureAdminAsync()`, arma `fileName = $"sanmarino-{DateTime.UtcNow:yyyy-MM-dd}-produccion.sql"`, setea `Content-Disposition`/`Content-Type: application/sql` y llama `_svc.WriteDatabaseBackupAsync(Response.Body)`. Igual que el resto del controller: 403/400/500 mapeados, pero una vez que arrancó el streaming no se puede cambiar el status code si falla a mitad de camino (limitación inherente a HTTP streaming, se loguea server-side).

## Frontend — `features/db-studio/`

1. `data/db-studio.service.ts`: `downloadBackup(): Observable<HttpResponse<Blob>>` (GET `/backup`, `responseType: 'blob'`, `observe: 'response'` para leer `Content-Disposition`).
2. `funciones/db-studio.funciones.ts`: nueva función pura `descargarBlob(blob: Blob, filename: string)` (mismo patrón que `descargarTexto` pero para `Blob` ya armado, sin crear uno nuevo desde texto).
3. `db-studio-main.component.ts`: `backupBusy = signal(false)`; método `downloadBackup()` — confirma con `ConfirmDialogService` (acción larga/pesada), llama al service, extrae filename de `Content-Disposition` (fallback a fecha local), dispara `descargarBlob`, usa `ToastService` para éxito/error (no `alert()`).
4. `db-studio-main.component.html`: botón **"Copia de seguridad"** en el header, visible solo si `isAdmin()`, junto a la nav de tabs; estado disabled + spinner mientras `backupBusy()`.

## Casos de prueba

- **Unit (xUnit)** `tests/ZooSanMarino.Application.Tests/DbStudioSqlCalculosTests.cs` (o nuevo archivo si aplica): si se extrae lógica pura de literal SQL (bytea/array) a `Calculos/`, cubrir con casos `byte[]`/array/null/string con comillas.
- **Integración manual (local, `make up`):** admin descarga backup de la BD local (pocas tablas/filas) → el archivo generado se puede restaurar contra una BD Postgres vacía (`psql -f`) sin errores en la sección tablas+datos+FKs+índices; fila nueva en `dbstudio_audit` con acción `backup.download`.
- **No-admin:** `GET /api/DbStudio/backup` → 403.
- **Frontend:** `yarn build` 0 errores; smoke visual del botón (aparece solo para admin, dispara descarga con el nombre correcto).

## Fases
0. Este plan + tracker. 1. Backend (`IDbStudioService` + partial `Backup.cs` + controller). 2. Frontend (service + función pura + botón). 3. Validación (build back+front, restore manual local, auditoría).
