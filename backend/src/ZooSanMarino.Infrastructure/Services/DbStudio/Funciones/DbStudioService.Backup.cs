using System.Data;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;
using ZooSanMarino.Application.DTOs;
using static ZooSanMarino.Application.Calculos.DbStudioSqlCalculos;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Copia de seguridad completa de la base de datos (todos los esquemas/tablas/datos, sin tope de filas)
/// como SQL restaurable, escrita en streaming directo al stream de salida — nunca se buferea el backup
/// completo en memoria. Ver plan: fase_de_desarrollo/db_studio_backup_descargable_plan.md.
/// </summary>
public sealed partial class DbStudioService
{
    private const int BackupBatchSize = 500;

    public async Task WriteDatabaseBackupAsync(Stream output, CancellationToken ct = default)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = false };
        var tableCount = 0;
        long rowCount = 0;

        try
        {
            await writer.WriteLineAsync($"-- Copia de seguridad SanMarino — generada por DB Studio el {DateTime.UtcNow:u}");
            await writer.WriteLineAsync("-- Formato SQL plano. Restaurar con: psql -h <host> -U <usuario> -d <bd> -f <archivo>.sql");
            await writer.WriteLineAsync("-- Alcance: tablas + datos completos + índices + FKs + secuencias + vistas + funciones + triggers.");
            await writer.WriteLineAsync("-- No incluye: roles/grants de Postgres, extensiones, comentarios, CHECK constraints.");
            await writer.WriteLineAsync("-- Funciones en orden topológico (cada una después de las que invoca), vistas alfabéticas al final.");
            await writer.WriteLineAsync("-- Restaurar sobre una base VACÍA y con -v ON_ERROR_STOP=1: no deberían quedar errores.");
            await writer.WriteLineAsync("-- ⚠ NO re-ejecutes este archivo completo sobre una base ya cargada: los INSERT no llevan ON CONFLICT y las");
            await writer.WriteLineAsync("--   tablas sin PK quedarían con filas duplicadas. Si alguna vez fallara una función/vista por dependencias,");
            await writer.WriteLineAsync("--   re-corré SOLO el tramo entre '-- Funciones/procedimientos' y '-- Triggers' (todo CREATE OR REPLACE).");
            await writer.WriteLineAsync("SET client_encoding = 'UTF8';");
            await writer.FlushAsync(ct);

            var schemas = (await GetSchemasAsync()).Select(s => s.Name).ToList();
            var tablesBySchema = new Dictionary<string, List<TableDto>>();
            var columnsByTable = new Dictionary<(string Schema, string Table), List<ColumnDto>>();

            // ===== 1a) Reconocimiento: esquemas/tablas/columnas (sin escribir todavía) =====
            foreach (var schema in schemas)
            {
                var tables = (await GetTablesAsync(schema))
                    .Where(t => t.Kind is "BASE TABLE" or "PARTITIONED TABLE")
                    .OrderBy(t => t.Name, StringComparer.Ordinal)
                    .ToList();
                tablesBySchema[schema] = tables;

                foreach (var t in tables)
                    columnsByTable[(schema, t.Name)] = (await GetTableColumnsAsync(schema, t.Name)).ToList();
            }

            // ===== 1b) Secuencias "serial" clásicas (no IDENTITY): deben existir ANTES que las tablas
            //           que las referencian en su DEFAULT, o el restore falla con "relation ... _seq
            //           does not exist" (IDENTITY no tiene este problema: CREATE TABLE crea su secuencia sola). =====
            var legacySeqsCreated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var schema in schemas)
                foreach (var t in tablesBySchema[schema])
                    foreach (var c in columnsByTable[(schema, t.Name)])
                        if (TryExtractLegacySequenceName(c, schema, out var seqSchema, out var seqName) &&
                            legacySeqsCreated.Add($"{seqSchema}.{seqName}"))
                            await writer.WriteLineAsync(BuildCreateSequenceSql(seqSchema, seqName));

            // ===== 1c) Esquemas + estructura de tablas =====
            foreach (var schema in schemas)
            {
                if (!string.Equals(schema, "public", StringComparison.Ordinal))
                    await writer.WriteLineAsync($"CREATE SCHEMA IF NOT EXISTS {QuoteIdent(schema)};");

                foreach (var t in tablesBySchema[schema])
                {
                    var cols = columnsByTable[(schema, t.Name)];
                    await writer.WriteLineAsync();
                    await writer.WriteAsync(BuildCreateTableSql(schema, t.Name, cols, ifNotExists: true, includeIdentity: true));
                    tableCount++;

                    foreach (var c in cols)
                        if (TryExtractLegacySequenceName(c, schema, out var seqSchema, out var seqName))
                            await writer.WriteLineAsync(BuildAlterSequenceOwnedBySql(seqSchema, seqName, t.Name, c.Name));
                }
            }
            await writer.FlushAsync(ct);

            // ===== 2) Datos: streaming en una única transacción REPEATABLE READ (snapshot consistente) =====
            await using (var dataConn = await _rt.OpenReadAsync(ct))
            {
                await using (var cmdTimeout = new NpgsqlCommand("SET statement_timeout = 0;", dataConn))
                    await cmdTimeout.ExecuteNonQueryAsync(ct);

                await using var tx = await dataConn.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
                foreach (var schema in schemas)
                {
                    foreach (var t in tablesBySchema[schema])
                    {
                        rowCount += await WriteTableDataAsync(writer, dataConn, schema, t.Name, columnsByTable[(schema, t.Name)], ct);
                        await writer.FlushAsync(ct);
                    }
                }
                await tx.CommitAsync(ct); // read-only: commit es un no-op funcional, cierra prolijo.
            }

            // ===== 3) Índices no-PK (pg_get_indexdef: fiel a índices parciales/de expresión, a diferencia
            //          de reconstruirlos a mano desde columnas) =====
            await WriteIndexesAsync(writer, schemas, tablesBySchema, ct);

            // ===== 4) Foreign keys (pg_get_constraintdef: fiel a FKs compuestas/MATCH/DEFERRABLE; después
            //          de los datos, sin depender del orden de inserción) =====
            await WriteForeignKeysAsync(writer, schemas, tablesBySchema, ct);

            // ===== 5) Resync de secuencias (identity y serial clásico) =====
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("-- Secuencias (autoincremento)");
            foreach (var schema in schemas)
                foreach (var t in tablesBySchema[schema])
                    foreach (var c in columnsByTable[(schema, t.Name)].Where(IsAutoIncrementColumn))
                        await writer.WriteLineAsync(BuildSetvalSql(schema, t.Name, c.Name));
            await writer.FlushAsync(ct);

            // ===== 6) Funciones, vistas, triggers (best-effort) =====
            await WriteRoutinesAsync(writer, schemas, ct);
            await WriteViewsAsync(writer, schemas, ct);
            await WriteTriggersAsync(writer, schemas, tablesBySchema, ct);

            await writer.WriteLineAsync();
            await writer.WriteLineAsync($"-- Fin del backup: {tableCount} tabla(s), {rowCount} fila(s).");
            await writer.FlushAsync(ct);

            await AuditAsync("backup.download", null, null, "-- full backup --", true,
                new { tables = tableCount, rows = rowCount }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando backup completo de la base de datos");
            await AuditAsync("backup.download", null, null, "-- full backup --", false,
                new { error = ex.Message, tables = tableCount, rows = rowCount }, CancellationToken.None);
            throw;
        }
    }

    /// <summary>Lee <paramref name="table"/> fila a fila (sin bufferear la tabla completa) y emite INSERTs por lotes.</summary>
    private static async Task<long> WriteTableDataAsync(
        StreamWriter writer, NpgsqlConnection conn, string schema, string table, List<ColumnDto> cols, CancellationToken ct)
    {
        var qtable = QuoteQualified(schema, table);
        var colSql = string.Join(", ", cols.Select(c => QuoteIdent(c.Name)));

        await using var cmd = new NpgsqlCommand($"SELECT {colSql} FROM {qtable};", conn) { CommandTimeout = 0 };
        await using var rd = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);

        long total = 0;
        var batch = new List<string>(BackupBatchSize);
        var wroteHeader = false;

        while (await rd.ReadAsync(ct))
        {
            if (!wroteHeader)
            {
                await writer.WriteLineAsync();
                await writer.WriteLineAsync($"-- Datos: {schema}.{table}");
                wroteHeader = true;
            }

            var vals = new string[rd.FieldCount];
            for (var i = 0; i < rd.FieldCount; i++)
                vals[i] = SqlLiteral(await rd.IsDBNullAsync(i, ct) ? null : rd.GetValue(i));
            batch.Add($"({string.Join(", ", vals)})");
            total++;

            if (batch.Count >= BackupBatchSize)
            {
                await writer.WriteLineAsync($"INSERT INTO {qtable} ({colSql}) VALUES\n{string.Join(",\n", batch)};");
                await writer.FlushAsync(ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            await writer.WriteLineAsync($"INSERT INTO {qtable} ({colSql}) VALUES\n{string.Join(",\n", batch)};");

        return total;
    }

    /// <summary>
    /// A diferencia de <c>GetFunctionSourceAsync</c> (busca por nombre + <c>LIMIT 1</c>, pensado para el
    /// explorador donde el usuario ya eligió un overload puntual), acá se consulta por <c>oid</c>
    /// directamente: si hay funciones con overloads (mismo nombre, distinta firma), cada una se exporta
    /// con su propio cuerpo en vez de duplicar el primer overload que Postgres devuelva y perder el resto.
    ///
    /// El orden de emisión NO es el de <c>oid</c> ni el alfabético, sino el topológico que calcula
    /// <see cref="DbStudioSqlCalculos.OrdenarRutinasPorDependencia"/> leyendo los cuerpos: una función
    /// <c>LANGUAGE sql</c> se valida contra el catálogo AL CREARSE, así que tiene que ir después de las
    /// que invoca o el restore falla con 42883. El orden por <c>oid</c> (creación) parece suficiente pero
    /// no lo es: <c>DROP FUNCTION</c> + <c>CREATE</c> —obligatorio para cambiarle el <c>RETURNS TABLE</c> a
    /// una función— le da un OID nuevo, más alto que el de sus llamadores, y la manda al final del archivo.
    /// Pasó de verdad con <c>fn_seguimiento_diario_engorde</c> (backup del 13ago26, 4 funciones sin crear).
    /// Ver plan: fase_de_desarrollo/db_studio_backup_orden_funciones_plan.md.
    ///
    /// Se bufferean las definiciones antes de escribir (a diferencia del resto del backup, que va en
    /// streaming puro): ordenar exige tenerlas todas. Son las rutinas del esquema, no las filas — el peso
    /// del backup lo domina el volumen de datos.
    /// </summary>
    private async Task WriteRoutinesAsync(StreamWriter writer, List<string> schemas, CancellationToken ct)
    {
        const string sql = @"
            select p.oid::bigint as oid, p.proname as name, pg_get_functiondef(p.oid) as def
            from pg_proc p
            join pg_namespace n on n.oid = p.pronamespace
            where n.nspname = @schema
            order by p.oid;";

        await writer.WriteLineAsync();
        await writer.WriteLineAsync("-- Funciones/procedimientos (orden topológico: cada una después de las que invoca)");
        await using var conn = await _rt.OpenReadAsync(ct);
        foreach (var schema in schemas)
        {
            var rutinas = new List<RoutineDef>();
            await using (var cmd = new NpgsqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("schema", schema);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    if (await rd.IsDBNullAsync(2, ct)) continue;
                    rutinas.Add(new RoutineDef(rd.GetInt64(0), rd.GetString(1), rd.GetString(2)));
                }
            }

            foreach (var r in OrdenarRutinasPorDependencia(rutinas))
                await writer.WriteLineAsync($"{r.Definition};");
            await writer.FlushAsync(ct);
        }
    }

    private async Task WriteViewsAsync(StreamWriter writer, List<string> schemas, CancellationToken ct)
    {
        await writer.WriteLineAsync();
        await writer.WriteLineAsync("-- Vistas (best-effort)");
        foreach (var schema in schemas)
        {
            foreach (var v in await GetViewsAsync(schema))
            {
                try
                {
                    var def = await GetViewDefinitionAsync(schema, v.Name);
                    if (string.IsNullOrWhiteSpace(def)) continue;
                    var qview = QuoteQualified(schema, v.Name);
                    if (v.Materialized)
                    {
                        await writer.WriteLineAsync($"DROP MATERIALIZED VIEW IF EXISTS {qview};");
                        await writer.WriteLineAsync($"CREATE MATERIALIZED VIEW {qview} AS\n{def};");
                    }
                    else
                    {
                        await writer.WriteLineAsync($"CREATE OR REPLACE VIEW {qview} AS\n{def};");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo exportar la vista {Schema}.{View} en el backup", schema, v.Name);
                    await writer.WriteLineAsync($"-- [omitida] vista {schema}.{v.Name}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// <c>pg_get_indexdef</c> devuelve la sentencia CREATE INDEX exacta (incluye índices parciales
    /// <c>WHERE ...</c> y de expresión) — reconstruirla a mano desde columnas pierde esa información y
    /// puede generar un índice único "más estricto" que el original (falla el restore por datos que el
    /// índice parcial real excluía).
    /// </summary>
    private async Task WriteIndexesAsync(
        StreamWriter writer, List<string> schemas, Dictionary<string, List<TableDto>> tablesBySchema, CancellationToken ct)
    {
        const string sql = @"
            select pg_get_indexdef(i.indexrelid) as def
            from pg_index i
            join pg_class c on c.oid = i.indrelid
            join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = @schema and c.relname = @table and not i.indisprimary
            order by i.indexrelid;";

        await writer.WriteLineAsync();
        await writer.WriteLineAsync("-- Índices");
        await using var conn = await _rt.OpenReadAsync(ct);
        foreach (var schema in schemas)
        {
            foreach (var t in tablesBySchema[schema])
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("schema", schema);
                cmd.Parameters.AddWithValue("table", t.Name);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                    await writer.WriteLineAsync($"{MakeCreateIndexIdempotent(rd.GetString(0))};");
            }
        }
    }

    /// <summary><c>pg_get_constraintdef</c> es fiel a FKs compuestas/MATCH FULL/DEFERRABLE (a diferencia de reconstruirlas a mano columna por columna).</summary>
    private async Task WriteForeignKeysAsync(
        StreamWriter writer, List<string> schemas, Dictionary<string, List<TableDto>> tablesBySchema, CancellationToken ct)
    {
        const string sql = @"
            select con.conname as name, pg_get_constraintdef(con.oid) as def
            from pg_constraint con
            join pg_class c on c.oid = con.conrelid
            join pg_namespace n on n.oid = c.relnamespace
            where con.contype = 'f' and n.nspname = @schema and c.relname = @table
            order by con.conname;";

        await writer.WriteLineAsync();
        await writer.WriteLineAsync("-- Foreign keys");
        await using var conn = await _rt.OpenReadAsync(ct);
        foreach (var schema in schemas)
        {
            foreach (var t in tablesBySchema[schema])
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("schema", schema);
                cmd.Parameters.AddWithValue("table", t.Name);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                {
                    var name = rd.GetString(0);
                    var def = rd.GetString(1);
                    await writer.WriteLineAsync($"ALTER TABLE {QuoteQualified(schema, t.Name)} ADD CONSTRAINT {QuoteIdent(name)} {def};");
                }
            }
        }
    }

    private async Task WriteTriggersAsync(
        StreamWriter writer, List<string> schemas, Dictionary<string, List<TableDto>> tablesBySchema, CancellationToken ct)
    {
        const string sql = @"
            select pg_get_triggerdef(t.oid) as def
            from pg_trigger t
            join pg_class c on c.oid = t.tgrelid
            join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = @schema and c.relname = @table and not t.tgisinternal
            order by t.tgname;";

        await writer.WriteLineAsync();
        await writer.WriteLineAsync("-- Triggers");
        await using var conn = await _rt.OpenReadAsync(ct);
        foreach (var schema in schemas)
        {
            foreach (var t in tablesBySchema[schema])
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("schema", schema);
                cmd.Parameters.AddWithValue("table", t.Name);
                await using var rd = await cmd.ExecuteReaderAsync(ct);
                while (await rd.ReadAsync(ct))
                    await writer.WriteLineAsync($"{rd.GetString(0)};");
            }
        }
    }
}
