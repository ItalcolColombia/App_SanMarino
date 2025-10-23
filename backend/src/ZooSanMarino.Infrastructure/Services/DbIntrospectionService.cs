using Microsoft.Extensions.Configuration;
using Npgsql;
using ZooSanMarino.Application.DTOs.DbStudio;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.DbStudio;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class DbIntrospectionService : IDbIntrospectionService
{
    private readonly string _connStr;

    public DbIntrospectionService(IConfiguration cfg, ZooSanMarinoContext? ctx = null)
    {
        _connStr = ConnectionStringResolver.Resolve(cfg, ctx);
    }

    public async Task<IReadOnlyList<SchemaDto>> GetSchemasAsync(CancellationToken ct)
    {
        const string sql = @"
        select n.nspname as schema_name,
               count(*) filter (where c.relkind in ('r','p','v','m')) as tables
        from pg_namespace n
        left join pg_class c on c.relnamespace = n.oid
        where n.nspname not like 'pg_%' and n.nspname <> 'information_schema'
        group by n.nspname
        order by 1;";
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var list = new List<SchemaDto>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new SchemaDto(rd.GetString(0), rd.GetInt64(1)));
        return list;
    }

    public async Task<IReadOnlyList<TableDto>> GetTablesAsync(string schema, CancellationToken ct)
    {
        const string sql = @"
        select c.relname as table_name,
               case c.relkind when 'r' then 'BASE TABLE' when 'p' then 'PARTITIONED TABLE'
                              when 'v' then 'VIEW' when 'm' then 'MATERIALIZED VIEW' else c.relkind::text end as kind,
               coalesce(c.reltuples::bigint,0) as approx_rows
        from pg_class c
        join pg_namespace n on n.oid = c.relnamespace
        where n.nspname = $1 and c.relkind in ('r','p','v','m')
        order by 1;";
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(schema);
        var list = new List<TableDto>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new TableDto(schema, rd.GetString(0), rd.GetString(1), rd.GetInt64(2)));
        return list;
    }

    public async Task<IReadOnlyList<ColumnDto>> GetColumnsAsync(string schema, string table, CancellationToken ct)
    {
        const string sql = @"
        select a.attname as column_name,
               pg_catalog.format_type(a.atttypid, a.atttypmod) as data_type,
               not a.attnotnull as is_nullable,
               pg_get_expr(ad.adbin, ad.adrelid) as default_expr,
               exists(
                 select 1 from pg_index i where i.indrelid = c.oid and i.indisprimary
                   and a.attnum = any(i.indkey)
               ) as is_pk
        from pg_attribute a
        join pg_class c on c.oid = a.attrelid
        join pg_namespace n on n.oid = c.relnamespace
        left join pg_attrdef ad on ad.adrelid = c.oid and ad.adnum = a.attnum
        where n.nspname = $1 and c.relname = $2 and a.attnum > 0 and not a.attisdropped
        order by a.attnum;";
        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue(schema);
        cmd.Parameters.AddWithValue(table);
        var list = new List<ColumnDto>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new ColumnDto(
                rd.GetString(0),
                rd.GetString(1),
                rd.GetBoolean(2),
                rd.IsDBNull(3) ? null : rd.GetString(3),
                rd.GetBoolean(4)
            ));
        return list;
    }

    public async Task<QueryPageDto> PreviewAsync(string schema, string table, int limit, int offset, CancellationToken ct)
    {
        var qname = DbStudioCommon.QTable(schema, table);
        var sql = $"select * from {qname} offset $1 limit $2;";

        await using var conn = new NpgsqlConnection(_connStr);
        await conn.OpenAsync(ct);
        var rows = new List<Dictionary<string, object?>>();
        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            cmd.Parameters.AddWithValue(offset);
            cmd.Parameters.AddWithValue(limit);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(rd.FieldCount);
                for (var i = 0; i < rd.FieldCount; i++)
                    row[rd.GetName(i)] = await rd.IsDBNullAsync(i, ct) ? null : rd.GetValue(i);
                rows.Add(row);
            }
        }

        var countSql = $"select reltuples::bigint from pg_class where oid = '{schema}.{table}'::regclass;";
        long approx = 0;
        await using (var cmd2 = new NpgsqlCommand(countSql, conn))
            approx = (long)(await cmd2.ExecuteScalarAsync(ct) ?? 0L);

        return new QueryPageDto(rows, (int)Math.Min(int.MaxValue, approx), limit, offset);
    }
}
