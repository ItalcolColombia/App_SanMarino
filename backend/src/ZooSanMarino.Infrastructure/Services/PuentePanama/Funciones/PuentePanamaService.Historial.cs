// Historial de corridas del puente (empresa activa): lista paginada con contadores (sin cargar el
// jsonb pesado) y detalle puntual reconstruyendo el ResultadoSincronizacionDto persistido en
// detalle_json. Consulta directa al contexto (proyección) para no tocar el contrato de
// IMigracionRepository, que sigue sirviendo al historial genérico de Migraciones Masivas.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.PuentePanama;

namespace ZooSanMarino.Infrastructure.Services;

public partial class PuentePanamaService
{
    public async Task<SincronizacionHistorialPagedDto> GetHistorialAsync(int page, int pageSize, bool incluirValidaciones, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100);

        var query = _ctx.MigracionMasiva.AsNoTracking()
            .Where(m => m.CompanyId == companyId && m.Tipo == TipoAuditoria && m.DeletedAt == null);
        if (!incluirValidaciones)
            query = query.Where(m => !m.FueDryRun);

        var total = await query.CountAsync(ct);

        // Proyección SIN detalle_json: la lista no necesita el jsonb (puede pesar cientos de KB por corrida).
        var items = await query
            .OrderByDescending(m => m.FechaProceso).ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new SincronizacionHistorialItemDto
            {
                Id = m.Id,
                FechaProceso = m.FechaProceso,
                FueDryRun = m.FueDryRun,
                Estado = m.Estado,
                DuracionMs = m.DuracionMs,
                NombreArchivo = m.NombreArchivo,
                LotesTotales = m.FilasTotales,
                LotesNuevos = m.FilasProcesadas,
                LotesOmitidos = m.FilasOmitidas,
                LotesConError = m.FilasError,
                TieneDetalle = m.DetalleJson != null
            })
            .ToListAsync(ct);

        foreach (var it in items)
            it.LotesPendientes = PuentePanamaCalculos.LotesPendientesDerivados(
                it.LotesTotales, it.LotesNuevos, it.LotesOmitidos, it.LotesConError);

        return new SincronizacionHistorialPagedDto { Page = page, PageSize = pageSize, Total = total, Items = items };
    }

    public async Task<SincronizacionHistorialDetalleDto?> GetHistorialDetalleAsync(int id, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var m = await _ctx.MigracionMasiva.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && x.Tipo == TipoAuditoria && x.DeletedAt == null, ct);
        if (m is null) return null;

        ResultadoSincronizacionDto? resultado = null;
        if (!string.IsNullOrWhiteSpace(m.DetalleJson))
        {
            try { resultado = JsonSerializer.Deserialize<ResultadoSincronizacionDto>(m.DetalleJson!); }
            catch (JsonException) { /* detalle ilegible: se devuelven solo los metadatos */ }
        }
        if (resultado is not null) resultado.AuditoriaId = m.Id;

        return new SincronizacionHistorialDetalleDto
        {
            Id = m.Id,
            FechaProceso = m.FechaProceso,
            FueDryRun = m.FueDryRun,
            Estado = m.Estado,
            DuracionMs = m.DuracionMs,
            NombreArchivo = m.NombreArchivo,
            Resultado = resultado
        };
    }
}
