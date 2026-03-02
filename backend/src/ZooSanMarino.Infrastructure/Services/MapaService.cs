using System.Data;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using ZooSanMarino.Application.DTOs.Mapas;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class MapaService : IMapaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly IServiceScopeFactory _scopeFactory;

    public MapaService(ZooSanMarinoContext ctx, ICurrentUser current, ICompanyResolver companyResolver, IServiceScopeFactory scopeFactory)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _scopeFactory = scopeFactory;
    }

    private async Task<int> GetEffectiveCompanyIdAsync(CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    /// <summary>Obtiene el Guid del usuario actual (requerido para mapas; users.id es UUID).</summary>
    private Guid GetUserGuid()
    {
        if (_current.UserGuid == null)
            throw new InvalidOperationException("No se pudo identificar al usuario (se requiere sesión con UserGuid para el módulo Mapas).");
        return _current.UserGuid.Value;
    }

    /// <summary>Opciones se guarda como JSONB; si el texto no es JSON válido, devuelve null para evitar error en BD.</summary>
    private static string? NormalizeOpcionesJson(string? opciones)
    {
        if (string.IsNullOrWhiteSpace(opciones)) return null;
        var trimmed = opciones.Trim();
        if (trimmed.Length == 0) return null;
        try
        {
            using var _ = JsonDocument.Parse(trimmed);
            return trimmed;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<MapaListDto>> GetAllAsync(CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var mapas = await _ctx.Mapas
            .AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.DeletedAt == null)
            .OrderBy(m => m.Nombre)
            .Select(m => new MapaListDto
            {
                Id = m.Id,
                Nombre = m.Nombre,
                Descripcion = m.Descripcion,
                CodigoPlantilla = m.CodigoPlantilla,
                IsActive = m.IsActive,
                PaisId = m.PaisId,
                CreatedAt = m.CreatedAt,
                TotalEjecuciones = m.Ejecuciones.Count,
                UltimaEjecucionAt = m.Ejecuciones
                    .OrderByDescending(e => e.FechaEjecucion)
                    .Select(e => (DateTime?)e.FechaEjecucion)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
        return mapas;
    }

    public async Task<MapaDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var mapa = await _ctx.Mapas
            .AsNoTracking()
            .Include(m => m.Pasos.OrderBy(p => p.Orden))
            .Where(m => m.Id == id && m.CompanyId == companyId && m.DeletedAt == null)
            .Select(m => new
            {
                m.Id,
                m.Nombre,
                m.Descripcion,
                m.CodigoPlantilla,
                m.IsActive,
                m.CompanyId,
                m.PaisId,
                m.CreatedAt,
                Pasos = m.Pasos.Select(p => new MapaPasoDto
                {
                    Id = p.Id,
                    MapaId = p.MapaId,
                    Orden = p.Orden,
                    Tipo = p.Tipo,
                    NombreEtiqueta = p.NombreEtiqueta,
                    ScriptSql = p.ScriptSql,
                    Opciones = p.Opciones
                }).ToList(),
                TotalEjecuciones = m.Ejecuciones.Count
            })
            .FirstOrDefaultAsync(ct);
        if (mapa == null) return null;
        return new MapaDetailDto
        {
            Id = mapa.Id,
            Nombre = mapa.Nombre,
            Descripcion = mapa.Descripcion,
            CodigoPlantilla = mapa.CodigoPlantilla,
            IsActive = mapa.IsActive,
            CompanyId = mapa.CompanyId,
            PaisId = mapa.PaisId,
            CreatedAt = mapa.CreatedAt,
            Pasos = mapa.Pasos,
            TotalEjecuciones = mapa.TotalEjecuciones
        };
    }

    public async Task<MapaDetailDto> CreateAsync(CreateMapaDto dto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var ent = new Mapa
        {
            Nombre = (dto.Nombre ?? string.Empty).Trim(),
            Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim(),
            CodigoPlantilla = string.IsNullOrWhiteSpace(dto.CodigoPlantilla) ? null : dto.CodigoPlantilla.Trim(),
            PaisId = dto.PaisId,
            IsActive = dto.IsActive,
            CompanyId = companyId,
            CreatedByUserId = GetUserGuid(),
            CreatedAt = DateTime.UtcNow
        };
        _ctx.Mapas.Add(ent);
        await _ctx.SaveChangesAsync(ct);
        return (await GetByIdAsync(ent.Id, ct))!;
    }

    public async Task<MapaDetailDto?> UpdateAsync(int id, UpdateMapaDto dto, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var ent = await _ctx.Mapas
            .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId && m.DeletedAt == null, ct);
        if (ent == null) return null;
        ent.Nombre = (dto.Nombre ?? string.Empty).Trim();
        ent.Descripcion = string.IsNullOrWhiteSpace(dto.Descripcion) ? null : dto.Descripcion.Trim();
        ent.CodigoPlantilla = string.IsNullOrWhiteSpace(dto.CodigoPlantilla) ? null : dto.CodigoPlantilla.Trim();
        ent.PaisId = dto.PaisId;
        ent.IsActive = dto.IsActive;
        ent.UpdatedByUserId = GetUserGuid();
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var ent = await _ctx.Mapas
            .FirstOrDefaultAsync(m => m.Id == id && m.CompanyId == companyId && m.DeletedAt == null, ct);
        if (ent == null) return false;
        ent.DeletedAt = DateTime.UtcNow;
        ent.UpdatedByUserId = GetUserGuid();
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    public async Task SavePasosAsync(int mapaId, IEnumerable<MapaPasoDto> pasos, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var mapa = await _ctx.Mapas
            .Include(m => m.Pasos)
            .FirstOrDefaultAsync(m => m.Id == mapaId && m.CompanyId == companyId && m.DeletedAt == null, ct);
        if (mapa == null) throw new InvalidOperationException("Mapa no encontrado.");
        var list = pasos?.ToList() ?? new List<MapaPasoDto>();
        _ctx.MapaPasos.RemoveRange(mapa.Pasos);
        var orden = 0;
        foreach (var p in list.OrderBy(x => x.Orden))
        {
            var tipo = (p.Tipo ?? "head").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(tipo)) tipo = "head";
            if (tipo != "head" && tipo != "extraction" && tipo != "transformation" && tipo != "execute" && tipo != "export")
                tipo = "head";
            var opcionesVal = NormalizeOpcionesJson(p.Opciones);
            _ctx.MapaPasos.Add(new MapaPaso
            {
                MapaId = mapaId,
                Orden = ++orden,
                Tipo = tipo,
                NombreEtiqueta = string.IsNullOrWhiteSpace(p.NombreEtiqueta) ? null : p.NombreEtiqueta.Trim(),
                ScriptSql = string.IsNullOrWhiteSpace(p.ScriptSql) ? null : p.ScriptSql.Trim(),
                Opciones = opcionesVal,
                CreatedAt = DateTime.UtcNow
            });
        }
        mapa.UpdatedByUserId = GetUserGuid();
        mapa.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<EjecutarMapaResponse> EjecutarAsync(int mapaId, EjecutarMapaRequest request, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var mapa = await _ctx.Mapas
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == mapaId && m.CompanyId == companyId && m.DeletedAt == null, ct);
        if (mapa == null) throw new InvalidOperationException("Mapa no encontrado.");

        var tieneExport = await _ctx.MapaPasos
            .AnyAsync(p => p.MapaId == mapaId && p.Tipo == "export" && p.ScriptSql != null && p.ScriptSql != "", ct);
        if (!tieneExport)
            throw new InvalidOperationException("El mapa debe tener al menos un paso de tipo Export con script SQL configurado.");

        var parametrosJson = JsonSerializer.Serialize(new
        {
            fechaDesde = request.FechaDesde,
            fechaHasta = request.FechaHasta,
            granjaIds = request.GranjaIds ?? new List<int>(),
            tipoDato = request.TipoDato,
            formatoExport = request.FormatoExport ?? "excel"
        });
        var ejecucion = new MapaEjecucion
        {
            MapaId = mapaId,
            UsuarioId = GetUserGuid(),
            CompanyId = companyId,
            Parametros = parametrosJson,
            Estado = "en_proceso",
            FechaEjecucion = DateTime.UtcNow
        };
        _ctx.MapaEjecuciones.Add(ejecucion);
        await _ctx.SaveChangesAsync(ct);

        var ejecucionId = ejecucion.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IMapaService>();
                await svc.ProcessExecutionAsync(ejecucionId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                try
                {
                    using var scope2 = _scopeFactory.CreateScope();
                    var ctx = scope2.ServiceProvider.GetRequiredService<ZooSanMarinoContext>();
                    var exec = await ctx.MapaEjecuciones.FindAsync(ejecucionId);
                    if (exec != null && exec.Estado == "en_proceso")
                    {
                        exec.Estado = "error";
                        exec.MensajeError = ex.Message;
                        exec.MensajeEstado = null;
                        exec.PasoActual = null;
                        exec.TotalPasos = null;
                        await ctx.SaveChangesAsync(CancellationToken.None);
                    }
                }
                catch { /* no fallar el proceso si no se pudo actualizar */ }
            }
        });

        return new EjecutarMapaResponse
        {
            EjecucionId = ejecucion.Id,
            Estado = "en_proceso",
            Mensaje = null
        };
    }

    public async Task ProcessExecutionAsync(int ejecucionId, CancellationToken ct = default)
    {
        var ejecucion = await _ctx.MapaEjecuciones
            .Include(e => e.Mapa)
            .ThenInclude(m => m!.Pasos)
            .FirstOrDefaultAsync(e => e.Id == ejecucionId, ct);
        if (ejecucion == null || ejecucion.Estado != "en_proceso") return;

        var mapa = ejecucion.Mapa;
        if (mapa == null) return;

        var param = JsonSerializer.Deserialize<ParametrosEjecucionJson>(ejecucion.Parametros ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var fechaDesde = param?.FechaDesde ?? DateTime.UtcNow.Date.AddMonths(-1);
        var fechaHasta = param?.FechaHasta ?? DateTime.UtcNow.Date;
        var granjaIdsStr = param?.GranjaIds != null && param.GranjaIds.Count > 0
            ? string.Join(",", param.GranjaIds)
            : "";
        var formatoExport = (param?.FormatoExport ?? "excel").Trim().ToLowerInvariant();
        if (formatoExport != "excel" && formatoExport != "pdf") formatoExport = "excel";

        var companyId = ejecucion.CompanyId;
        var pasosOrdenados = mapa.Pasos.OrderBy(p => p.Orden).ToList();
        var totalPasos = pasosOrdenados.Count;
        var pasoIndex = 0;
        ejecucion.TotalPasos = totalPasos;

        try
        {
            List<Dictionary<string, object?>>? lastResult = null;
            foreach (var paso in pasosOrdenados)
            {
                pasoIndex++;
                ejecucion.PasoActual = pasoIndex;
                ejecucion.MensajeEstado = $"Paso {pasoIndex}/{totalPasos}: {paso.NombreEtiqueta ?? paso.Tipo ?? "—"}";
                await _ctx.SaveChangesAsync(ct);

                if (string.IsNullOrWhiteSpace(paso.ScriptSql)) continue;
                var sql = paso.ScriptSql.Trim();
                sql = sql.Replace("{{fechaDesde}}", "@p_fechaDesde", StringComparison.OrdinalIgnoreCase);
                sql = sql.Replace("{{fechaHasta}}", "@p_fechaHasta", StringComparison.OrdinalIgnoreCase);
                sql = sql.Replace("{{companyId}}", "@p_companyId", StringComparison.OrdinalIgnoreCase);
                sql = sql.Replace("{{granjaIds}}", "@p_granjaIds", StringComparison.OrdinalIgnoreCase);

                lastResult = await ExecuteQueryAsync(sql, companyId, fechaDesde, fechaHasta, granjaIdsStr, ct);
                if (paso.Tipo?.Trim().ToLowerInvariant() == "export")
                {
                    ejecucion.TipoArchivo = formatoExport;
                    ejecucion.ResultadoJson = JsonSerializer.Serialize(lastResult);
                    break;
                }
            }

            if (lastResult != null && pasosOrdenados.Any(p => p.Tipo?.Trim().ToLowerInvariant() == "export"))
                ejecucion.TipoArchivo = formatoExport;
            ejecucion.ResultadoJson = JsonSerializer.Serialize(lastResult ?? new List<Dictionary<string, object?>>());
            ejecucion.Estado = "completado";
            ejecucion.MensajeEstado = null;
            ejecucion.PasoActual = null;
            ejecucion.TotalPasos = null;
        }
        catch (Exception ex)
        {
            ejecucion.Estado = "error";
            ejecucion.MensajeError = ex.Message;
            ejecucion.MensajeEstado = null;
            ejecucion.PasoActual = null;
            ejecucion.TotalPasos = null;
        }

        await _ctx.SaveChangesAsync(ct);
    }

    private class ParametrosEjecucionJson
    {
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public List<int>? GranjaIds { get; set; }
        public string? FormatoExport { get; set; }
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(
        string sql, int companyId, DateTime fechaDesde, DateTime fechaHasta, string granjaIds, CancellationToken ct)
    {
        var conn = _ctx.Database.GetDbConnection();
        await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 120;
            var p1 = cmd.CreateParameter();
            p1.ParameterName = "@p_fechaDesde";
            p1.Value = fechaDesde;
            cmd.Parameters.Add(p1);
            var p2 = cmd.CreateParameter();
            p2.ParameterName = "@p_fechaHasta";
            p2.Value = fechaHasta;
            cmd.Parameters.Add(p2);
            var p3 = cmd.CreateParameter();
            p3.ParameterName = "@p_companyId";
            p3.Value = companyId;
            cmd.Parameters.Add(p3);
            var p4 = cmd.CreateParameter();
            p4.ParameterName = "@p_granjaIds";
            p4.Value = (object?)granjaIds ?? DBNull.Value;
            cmd.Parameters.Add(p4);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var columns = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
                columns.Add(reader.GetName(i));
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    if (val is DateTime dt)
                        row[columns[i]] = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                    else
                        row[columns[i]] = val;
                }
                rows.Add(row);
            }
            return rows;
        }
        finally
        {
            await conn.CloseAsync();
        }
    }

    public async Task<MapaEjecucionEstadoDto?> GetEjecucionEstadoAsync(int ejecucionId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var e = await _ctx.MapaEjecuciones
            .AsNoTracking()
            .Where(x => x.Id == ejecucionId && x.CompanyId == companyId)
            .Select(x => new MapaEjecucionEstadoDto
            {
                Id = x.Id,
                MapaId = x.MapaId,
                Estado = x.Estado,
                MensajeError = x.MensajeError,
                MensajeEstado = x.MensajeEstado,
                PasoActual = x.PasoActual,
                TotalPasos = x.TotalPasos,
                TipoArchivo = x.TipoArchivo,
                FechaEjecucion = x.FechaEjecucion,
                PuedeDescargar = x.Estado == "completado" && x.ResultadoJson != null && (x.TipoArchivo == "excel" || x.TipoArchivo == "pdf")
            })
            .FirstOrDefaultAsync(ct);
        return e;
    }

    public async Task<(Stream? Stream, string FileName)?> GetEjecucionArchivoAsync(int ejecucionId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var e = await _ctx.MapaEjecuciones
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ejecucionId && x.CompanyId == companyId && x.Estado == "completado" && x.ResultadoJson != null, ct);
        if (e == null) return null;
        var tipo = (e.TipoArchivo ?? "excel").Trim().ToLowerInvariant();
        var rows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(e.ResultadoJson!);
        if (rows == null || rows.Count == 0)
            return (new MemoryStream(), $"mapa_{e.MapaId}_ejecucion_{e.Id}.xlsx");

        if (tipo == "pdf")
            tipo = "excel"; // PDF no implementado en v1, devolvemos excel

        var columns = rows[0].Keys.ToList();
        var stream = new MemoryStream();
        ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
        using (var package = new ExcelPackage(stream))
        {
            var sheet = package.Workbook.Worksheets.Add("Datos");
            for (var c = 0; c < columns.Count; c++)
                sheet.Cells[1, c + 1].Value = columns[c];
            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (var c = 0; c < columns.Count; c++)
                {
                    if (!row.TryGetValue(columns[c], out var v))
                    {
                        sheet.Cells[r + 2, c + 1].Value = null;
                        continue;
                    }
                    object? cellVal = null;
                    if (v.ValueKind == JsonValueKind.Number) cellVal = v.TryGetDouble(out var d) ? d : v.GetRawText();
                    else if (v.ValueKind == JsonValueKind.String) cellVal = v.GetString();
                    else if (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) cellVal = v.GetBoolean();
                    else if (v.ValueKind == JsonValueKind.Null || v.ValueKind == JsonValueKind.Undefined) cellVal = null;
                    else cellVal = v.GetRawText();
                    sheet.Cells[r + 2, c + 1].Value = cellVal;
                }
            }
            package.Save();
        }
        stream.Position = 0;
        var fileName = $"mapa_{e.MapaId}_ejecucion_{e.Id}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
        return (stream, fileName);
    }

    public async Task<IEnumerable<MapaEjecucionHistorialDto>> GetEjecucionesByMapaAsync(int mapaId, int limit = 20, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        var list = await _ctx.MapaEjecuciones
            .AsNoTracking()
            .Where(x => x.MapaId == mapaId && x.CompanyId == companyId)
            .OrderByDescending(x => x.FechaEjecucion)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(x => new MapaEjecucionHistorialDto
            {
                Id = x.Id,
                MapaId = x.MapaId,
                Estado = x.Estado,
                MensajeError = x.MensajeError,
                FechaEjecucion = x.FechaEjecucion,
                PuedeDescargar = x.Estado == "completado" && x.ResultadoJson != null && (x.TipoArchivo == "excel" || x.TipoArchivo == "pdf"),
                TipoArchivo = x.TipoArchivo
            })
            .ToListAsync(ct);
        return list;
    }
}
