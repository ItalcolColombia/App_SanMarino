using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.DbStudio;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>Monitoreo y control de concurrencia de PostgreSQL (solo admin).</summary>
public sealed class DbStudioConcurrencyService : IDbStudioConcurrencyService
{
    private readonly DbStudioRuntime _rt;
    private readonly DbStudioOptions _opts;
    private readonly ICurrentUser _current;
    private readonly IHttpContextAccessor _http;

    public DbStudioConcurrencyService(
        DbStudioRuntime rt, IOptions<DbStudioOptions> opts, ICurrentUser current, IHttpContextAccessor http)
    {
        _rt = rt;
        _opts = opts.Value;
        _current = current;
        _http = http;
    }

    public async Task<ActivitySnapshotDto> GetActivityAsync(CancellationToken ct = default)
    {
        const string sql = @"
            select a.pid, a.usename, a.application_name, host(a.client_addr) as client_addr,
                   a.state, a.wait_event_type, a.wait_event, a.query,
                   a.query_start, a.xact_start, a.backend_start,
                   pg_blocking_pids(a.pid) as blocked_by,
                   (a.pid = pg_backend_pid()) as is_current,
                   current_setting('max_connections')::int as max_conn
            from pg_stat_activity a
            where a.backend_type = 'client backend'
            order by a.query_start desc nulls last;";

        await using var conn = await _rt.OpenReadAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var snap = new ActivitySnapshotDto();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            snap.MaxConnections = rd.GetInt32(rd.GetOrdinal("max_conn"));
            var blocked = rd["blocked_by"] is int[] arr ? arr.ToList() : new List<int>();
            var s = new ActivitySessionDto
            {
                Pid = rd.GetInt32(rd.GetOrdinal("pid")),
                UserName = AsStr(rd["usename"]),
                ApplicationName = AsStr(rd["application_name"]),
                ClientAddr = AsStr(rd["client_addr"]),
                State = AsStr(rd["state"]),
                WaitEventType = AsStr(rd["wait_event_type"]),
                WaitEvent = AsStr(rd["wait_event"]),
                Query = AsStr(rd["query"]),
                QueryStart = AsDate(rd["query_start"]),
                XactStart = AsDate(rd["xact_start"]),
                BackendStart = AsDate(rd["backend_start"]),
                BlockedBy = blocked,
                IsCurrentSession = rd["is_current"] is bool b && b
            };
            snap.Sessions.Add(s);
        }

        snap.TotalConnections = snap.Sessions.Count;
        snap.ActiveConnections = snap.Sessions.Count(x => x.State == "active");
        snap.IdleConnections = snap.Sessions.Count(x => x.State == "idle");
        snap.IdleInTransaction = snap.Sessions.Count(x => x.State == "idle in transaction");
        snap.BlockedConnections = snap.Sessions.Count(x => x.BlockedBy.Count > 0);
        return snap;
    }

    public async Task<PoolStatsDto> GetPoolStatsAsync(CancellationToken ct = default)
    {
        const string sql = @"select count(*)::int from pg_stat_activity where application_name like @app;";
        await using var conn = await _rt.OpenReadAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("app", _opts.ApplicationName + "%");
        var n = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
        return new PoolStatsDto
        {
            PoolMinSize = _opts.PoolMinSize,
            PoolMaxSize = _opts.PoolMaxSize,
            DbStudioConnections = n,
            ApplicationName = _opts.ApplicationName
        };
    }

    public async Task<IEnumerable<LockDto>> GetLocksAsync(CancellationToken ct = default)
    {
        const string sql = @"
            select l.pid, l.locktype, l.mode, l.granted,
                   coalesce(c.relname, l.locktype) as relation,
                   a.query
            from pg_locks l
            left join pg_class c on c.oid = l.relation
            left join pg_stat_activity a on a.pid = l.pid
            where l.pid is not null
            order by l.granted, l.pid;";
        await using var conn = await _rt.OpenReadAsync(ct);
        await using var cmd = new NpgsqlCommand(sql, conn);
        var list = new List<LockDto>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new LockDto
            {
                Pid = rd.GetInt32(rd.GetOrdinal("pid")),
                LockType = AsStr(rd["locktype"]),
                Mode = AsStr(rd["mode"]),
                Granted = rd["granted"] is bool b && b,
                Relation = AsStr(rd["relation"]),
                Query = AsStr(rd["query"])
            });
        return list;
    }

    public Task<bool> CancelBackendAsync(int pid, CancellationToken ct = default)
        => KillAsync("pg_cancel_backend", "CANCEL_BACKEND", pid, ct);

    public Task<bool> TerminateBackendAsync(int pid, CancellationToken ct = default)
        => KillAsync("pg_terminate_backend", "TERMINATE_BACKEND", pid, ct);

    private async Task<bool> KillAsync(string fn, string action, int pid, CancellationToken ct)
    {
        await using var conn = await _rt.OpenWriteAsync(ct);

        // Auto-protección: nunca actuar sobre la propia sesión.
        await using (var self = new NpgsqlCommand("select pg_backend_pid();", conn))
        {
            var myPid = Convert.ToInt32(await self.ExecuteScalarAsync(ct) ?? 0);
            if (pid == myPid)
                throw new InvalidOperationException("No se puede cancelar/terminar la sesión propia de DB Studio.");
        }

        bool result;
        try
        {
            await using var cmd = new NpgsqlCommand($"select {fn}(@pid);", conn);
            cmd.Parameters.AddWithValue("pid", pid);
            result = await cmd.ExecuteScalarAsync(ct) is bool b && b;
            await AuditAsync(conn, action, pid, true, new { result }, ct);
        }
        catch (Exception ex)
        {
            await AuditAsync(conn, action, pid, false, new { error = ex.Message }, ct);
            throw new InvalidOperationException($"Error en {action}: {ex.Message}", ex);
        }
        return result;
    }

    private async Task AuditAsync(NpgsqlConnection conn, string action, int pid, bool success, object summary, CancellationToken ct)
    {
        try
        {
            const string sql = @"
                insert into public.dbstudio_audit
                  (action, schema_name, object_name, sql_text, result_summary, success,
                   actor_user_id, actor_email, company_id, ip_address, created_at_utc)
                values (@action, null, null, @sql, @summary::jsonb, @success, @actor, @email, @company, @ip, now() at time zone 'utc');";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("action", action);
            cmd.Parameters.AddWithValue("sql", $"{action}(pid={pid})");
            cmd.Parameters.Add(new NpgsqlParameter("summary", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(summary) });
            cmd.Parameters.AddWithValue("success", success);
            cmd.Parameters.AddWithValue("actor", (object?)_current.UserGuid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("email",
                (object?)(_http.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value) ?? DBNull.Value);
            cmd.Parameters.AddWithValue("company", _current.CompanyId);
            cmd.Parameters.AddWithValue("ip", (object?)_http.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch { /* la auditoría no debe tumbar la acción */ }
    }

    private static string? AsStr(object v) => v is DBNull ? null : Convert.ToString(v);
    private static DateTime? AsDate(object v) => v is DBNull ? null : Convert.ToDateTime(v);
}
