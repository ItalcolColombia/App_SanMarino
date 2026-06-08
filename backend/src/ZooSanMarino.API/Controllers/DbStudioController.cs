using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// DB Studio: explorador/editor de base de datos embebido. La autorización se aplica
/// EXPLÍCITAMENTE acá (las policies de ASP.NET están neutralizadas en este proyecto).
/// Admin = todo; no-admin = solo objetos con grant (lectura o escritura de datos), sin DDL ni SQL arbitrario.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DbStudioController : ControllerBase
{
    private readonly IDbStudioService _svc;
    private readonly IDbStudioAuthorization _authz;
    private readonly IDbStudioPermissionService _perms;
    private readonly IDbStudioConcurrencyService _concurrency;
    private readonly ILogger<DbStudioController> _logger;

    public DbStudioController(
        IDbStudioService svc,
        IDbStudioAuthorization authz,
        IDbStudioPermissionService perms,
        IDbStudioConcurrencyService concurrency,
        ILogger<DbStudioController> logger)
    {
        _svc = svc;
        _authz = authz;
        _perms = perms;
        _concurrency = concurrency;
        _logger = logger;
    }

    /// <summary>Ejecuta una acción ya autorizada, mapeando excepciones a códigos HTTP claros.</summary>
    private async Task<ActionResult> Run(Func<Task<object?>> action)
    {
        try
        {
            var result = await action();
            return result is null ? Ok() : Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DbStudio error");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    // ===================== ACCESO / PERMISOS PROPIOS =====================
    [HttpGet("my-access")]
    public Task<ActionResult> MyAccess() => Run(async () =>
    {
        await _authz.EnsureModuleAccessAsync();
        return (object?)await _authz.GetMyAccessAsync();
    });

    // ===================== ESQUEMAS =====================
    [HttpGet("schemas")]
    public Task<ActionResult> GetSchemas() => Run(async () =>
    {
        await _authz.EnsureModuleAccessAsync();
        return (object?)await _svc.GetSchemasAsync();
    });

    [HttpGet("schemas/{schema}/export")]
    public async Task<IActionResult> ExportSchema(string schema)
    {
        try
        {
            await _authz.EnsureAdminAsync();
            var content = await _svc.ExportSchemaAsync(schema);
            return File(content, "application/sql", $"{schema}_schema.sql");
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "ExportSchema"); return StatusCode(500, new { message = "Error interno" }); }
    }

    // ===================== TABLAS =====================
    [HttpGet("tables")]
    public Task<ActionResult> GetTables([FromQuery] string? schema = null) => Run(async () =>
    {
        await _authz.EnsureModuleAccessAsync();
        return (object?)await _svc.GetTablesAsync(schema);
    });

    [HttpGet("tables/{schema}/{table}/details")]
    public Task<ActionResult> GetTableDetails(string schema, string table) => Run(async () =>
    {
        await _authz.EnsureCanReadAsync(schema, table);
        return (object?)await _svc.GetTableDetailsAsync(schema, table);
    });

    [HttpGet("tables/{schema}/{table}/columns")]
    public Task<ActionResult> GetTableColumns(string schema, string table) => Run(async () =>
    {
        await _authz.EnsureCanReadAsync(schema, table);
        return (object?)await _svc.GetTableColumnsAsync(schema, table);
    });

    [HttpGet("tables/{schema}/{table}/indexes")]
    public Task<ActionResult> GetTableIndexes(string schema, string table) => Run(async () =>
    {
        await _authz.EnsureCanReadAsync(schema, table);
        return (object?)await _svc.GetTableIndexesAsync(schema, table);
    });

    [HttpGet("tables/{schema}/{table}/foreign-keys")]
    public Task<ActionResult> GetTableForeignKeys(string schema, string table) => Run(async () =>
    {
        await _authz.EnsureCanReadAsync(schema, table);
        return (object?)await _svc.GetTableForeignKeysAsync(schema, table);
    });

    [HttpGet("tables/{schema}/{table}/stats")]
    public Task<ActionResult> GetTableStats(string schema, string table) => Run(async () =>
    {
        await _authz.EnsureCanReadAsync(schema, table);
        return (object?)await _svc.GetTableStatsAsync(schema, table);
    });

    [HttpGet("tables/{schema}/{table}/preview")]
    public Task<ActionResult> PreviewTable(string schema, string table, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
        => Run(async () =>
    {
        await _authz.EnsureCanReadAsync(schema, table);
        return (object?)await _svc.PreviewTableAsync(schema, table, limit, offset);
    });

    [HttpGet("tables/{schema}/{table}/dependencies")]
    public Task<ActionResult> GetTableDependencies(string schema, string table) => Run(async () =>
    {
        await _authz.EnsureCanReadAsync(schema, table);
        return (object?)await _svc.GetTableDependenciesAsync(schema, table);
    });

    // ===================== VISTAS =====================
    [HttpGet("views")]
    public Task<ActionResult> GetViews([FromQuery] string? schema = null) => Run(async () =>
    {
        await _authz.EnsureModuleAccessAsync();
        return (object?)await _svc.GetViewsAsync(schema);
    });

    [HttpGet("views/{schema}/{name}/definition")]
    public Task<ActionResult> GetViewDefinition(string schema, string name) => Run(async () =>
    {
        await _authz.EnsureCanReadAsync(schema, name);
        return (object?)new { definition = await _svc.GetViewDefinitionAsync(schema, name) };
    });

    [HttpPost("views")]
    public Task<ActionResult> CreateOrReplaceView([FromBody] CreateViewRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.CreateOrReplaceViewAsync(request);
        return (object?)null;
    });

    [HttpDelete("views/{schema}/{name}")]
    public Task<ActionResult> DropView(string schema, string name, [FromQuery] bool materialized = false) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.DropViewAsync(schema, name, materialized);
        return (object?)null;
    });

    // ===================== FUNCIONES (admin) =====================
    [HttpGet("functions")]
    public Task<ActionResult> GetFunctions([FromQuery] string? schema = null) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _svc.GetFunctionsAsync(schema);
    });

    [HttpGet("functions/{schema}/{name}/source")]
    public Task<ActionResult> GetFunctionSource(string schema, string name) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _svc.GetFunctionSourceAsync(schema, name);
    });

    [HttpPost("functions")]
    public Task<ActionResult> CreateOrReplaceRoutine([FromBody] CreateRoutineRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.CreateOrReplaceRoutineAsync(request);
        return (object?)null;
    });

    // ===================== CONSULTAS SQL (admin) =====================
    [HttpPost("query/select")]
    public Task<ActionResult> ExecuteSelectQuery([FromBody] SelectQueryRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _svc.ExecuteSelectQueryAsync(request);
    });

    [HttpPost("query/execute")]
    public Task<ActionResult> ExecuteQuery([FromBody] ExecuteQueryRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _svc.ExecuteQueryAsync(request);
    });

    [HttpPost("sql/execute")]
    public Task<ActionResult> ExecuteSql([FromBody] ExecuteSqlRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _svc.ExecuteSqlAsync(request);
    });

    [HttpPost("sql/classify")]
    public Task<ActionResult> ClassifySql([FromBody] SqlValidationRequest request) => Run(async () =>
    {
        await _authz.EnsureModuleAccessAsync();
        return (object?)_svc.ClassifySql(request.Sql);
    });

    [HttpPost("validate-sql")]
    public Task<ActionResult> ValidateSql([FromBody] SqlValidationRequest request) => Run(async () =>
    {
        await _authz.EnsureModuleAccessAsync();
        return (object?)await _svc.ValidateSqlAsync(request.Sql);
    });

    // ===================== DDL (admin) =====================
    [HttpPost("tables")]
    public Task<ActionResult> CreateTable([FromBody] CreateTableRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.CreateTableAsync(request);
        return (object?)null;
    });

    [HttpDelete("tables/{schema}/{table}")]
    public Task<ActionResult> DropTable(string schema, string table, [FromQuery] bool cascade = false) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.DropTableAsync(schema, table, cascade);
        return (object?)null;
    });

    [HttpPost("tables/{schema}/{table}/columns")]
    public Task<ActionResult> AddColumn(string schema, string table, [FromBody] AddColumnRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.AddColumnAsync(schema, table, request);
        return (object?)null;
    });

    [HttpPatch("tables/{schema}/{table}/columns/{column}")]
    public Task<ActionResult> AlterColumn(string schema, string table, string column, [FromBody] AlterColumnRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.AlterColumnAsync(schema, table, column, request);
        return (object?)null;
    });

    [HttpDelete("tables/{schema}/{table}/columns/{column}")]
    public Task<ActionResult> DropColumn(string schema, string table, string column) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.DropColumnAsync(schema, table, column);
        return (object?)null;
    });

    [HttpPost("tables/{schema}/{table}/indexes")]
    public Task<ActionResult> CreateIndex(string schema, string table, [FromBody] CreateIndexRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.CreateIndexAsync(schema, table, request);
        return (object?)null;
    });

    [HttpDelete("tables/{schema}/{table}/indexes/{indexName}")]
    public Task<ActionResult> DropIndex(string schema, string table, string indexName) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.DropIndexAsync(schema, table, indexName);
        return (object?)null;
    });

    [HttpPost("tables/{schema}/{table}/foreign-keys")]
    public Task<ActionResult> CreateForeignKey(string schema, string table, [FromBody] CreateForeignKeyRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.CreateForeignKeyAsync(schema, table, request);
        return (object?)null;
    });

    [HttpDelete("tables/{schema}/{table}/foreign-keys/{fkName}")]
    public Task<ActionResult> DropForeignKey(string schema, string table, string fkName) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _svc.DropForeignKeyAsync(schema, table, fkName);
        return (object?)null;
    });

    // ===================== DATOS (grant write) =====================
    [HttpPost("tables/{schema}/{table}/data")]
    public Task<ActionResult> InsertData(string schema, string table, [FromBody] InsertDataRequest request) => Run(async () =>
    {
        await _authz.EnsureCanWriteDataAsync(schema, table);
        var rows = request.Rows.Select(r => r.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? (object)DBNull.Value)).ToList();
        await _svc.InsertDataAsync(schema, table, rows);
        return (object?)null;
    });

    [HttpPatch("tables/{schema}/{table}/data")]
    public Task<ActionResult> UpdateData(string schema, string table, [FromBody] UpdateDataRequest request) => Run(async () =>
    {
        await _authz.EnsureCanWriteDataAsync(schema, table);
        var data = request.Data.ToDictionary(k => k.Key, v => v.Value ?? (object)DBNull.Value);
        var where = request.Where.ToDictionary(k => k.Key, v => v.Value ?? (object)DBNull.Value);
        await _svc.UpdateDataAsync(schema, table, data, where);
        return (object?)null;
    });

    [HttpDelete("tables/{schema}/{table}/data")]
    public Task<ActionResult> DeleteData(string schema, string table, [FromBody] DeleteDataRequest request) => Run(async () =>
    {
        await _authz.EnsureCanWriteDataAsync(schema, table);
        var where = request.Where.ToDictionary(k => k.Key, v => v.Value ?? (object)DBNull.Value);
        await _svc.DeleteDataAsync(schema, table, where);
        return (object?)null;
    });

    // ===================== EXPORT / IMPORT =====================
    [HttpGet("tables/{schema}/{table}/export")]
    public async Task<IActionResult> ExportTable(string schema, string table, [FromQuery] string format = "sql")
    {
        try
        {
            await _authz.EnsureCanReadAsync(schema, table);
            var content = await _svc.ExportTableAsync(schema, table, format);
            var contentType = format.ToLower() switch
            {
                "csv" => "text/csv",
                "json" => "application/json",
                _ => "application/sql"
            };
            return File(content, contentType, $"{schema}_{table}.{format}");
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) { _logger.LogError(ex, "ExportTable"); return StatusCode(500, new { message = "Error interno" }); }
    }

    [HttpPost("tables/{schema}/{table}/import")]
    [Consumes("multipart/form-data")]
    public Task<ActionResult> ImportTable(string schema, string table, IFormFile file, [FromForm] string format = "csv") => Run(async () =>
    {
        await _authz.EnsureCanWriteDataAsync(schema, table);
        if (file is null || file.Length == 0) throw new InvalidOperationException("Archivo no proporcionado.");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        await _svc.ImportTableAsync(schema, table, ms.ToArray(), format);
        return (object?)new { message = "Datos importados exitosamente" };
    });

    // ===================== ANÁLISIS / UTILIDADES =====================
    [HttpGet("data-types")]
    public Task<ActionResult> GetDataTypes() => Run(async () =>
    {
        await _authz.EnsureModuleAccessAsync();
        return (object?)await _svc.GetDataTypesAsync();
    });

    [HttpGet("database/analyze")]
    public Task<ActionResult> AnalyzeDatabase() => Run(async () =>
    {
        await _authz.EnsureModuleAccessAsync();
        return (object?)await _svc.AnalyzeDatabaseAsync();
    });

    // ===================== PERMISOS / GRANTS (admin) =====================
    [HttpGet("grants")]
    public Task<ActionResult> GetGrants([FromQuery] Guid? userId = null) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return userId.HasValue
            ? (object?)await _perms.GetGrantsByUserAsync(userId.Value)
            : (object?)await _perms.GetAllGrantsAsync();
    });

    [HttpPost("grants")]
    public Task<ActionResult> UpsertGrant([FromBody] GrantRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _perms.UpsertGrantAsync(request);
    });

    [HttpDelete("grants/{grantId:long}")]
    public Task<ActionResult> RevokeGrant(long grantId) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        await _perms.RevokeGrantAsync(grantId);
        return (object?)null;
    });

    // ===================== CONCURRENCIA / HILOS (admin) =====================
    [HttpGet("concurrency/activity")]
    public Task<ActionResult> GetActivity() => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _concurrency.GetActivityAsync();
    });

    [HttpGet("concurrency/pool")]
    public Task<ActionResult> GetPoolStats() => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _concurrency.GetPoolStatsAsync();
    });

    [HttpGet("concurrency/locks")]
    public Task<ActionResult> GetLocks() => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)await _concurrency.GetLocksAsync();
    });

    [HttpPost("concurrency/cancel")]
    public Task<ActionResult> CancelBackend([FromBody] ConcurrencyActionRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)new { cancelled = await _concurrency.CancelBackendAsync(request.Pid) };
    });

    [HttpPost("concurrency/terminate")]
    public Task<ActionResult> TerminateBackend([FromBody] ConcurrencyActionRequest request) => Run(async () =>
    {
        await _authz.EnsureAdminAsync();
        return (object?)new { terminated = await _concurrency.TerminateBackendAsync(request.Pid) };
    });
}
