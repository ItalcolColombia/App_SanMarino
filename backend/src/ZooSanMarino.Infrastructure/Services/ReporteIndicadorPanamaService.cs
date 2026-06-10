// src/ZooSanMarino.Infrastructure/Services/ReporteIndicadorPanamaService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Liquidación / reporte de indicadores técnicos para Panamá (Pollo Engorde).
/// Persiste los 6 insumos en liquidacion_lote_engorde_panama y delega el cálculo de
/// los indicadores derivados a la función SQL fn_reporte_indicadores_panama.
/// </summary>
public class ReporteIndicadorPanamaService : IReporteIndicadorPanamaService
{
    private readonly ZooSanMarinoContext _context;

    public ReporteIndicadorPanamaService(ZooSanMarinoContext context)
    {
        _context = context;
    }

    public async Task<int> GuardarLiquidacionAsync(GuardarLiquidacionPanamaRequest request, CancellationToken ct = default)
    {
        if (request.LoteAveEngordeId <= 0)
            throw new InvalidOperationException("LoteAveEngordeId es requerido.");

        var entity = await _context.LiquidacionLoteEngordePanama
            .FirstOrDefaultAsync(x => x.LoteAveEngordeId == request.LoteAveEngordeId, ct);

        if (entity is null)
        {
            entity = new LiquidacionLoteEngordePanama
            {
                LoteAveEngordeId = request.LoteAveEngordeId,
                CreatedAt = DateTime.UtcNow
            };
            _context.LiquidacionLoteEngordePanama.Add(entity);
        }
        else
        {
            entity.UpdatedAt = DateTime.UtcNow;
        }

        entity.MetrosCuadrados = request.MetrosCuadrados;
        entity.AvesFinalGranja = request.AvesFinalGranja;
        entity.AvesBeneficiada = request.AvesBeneficiada;
        entity.ProduccionKiloPie = request.ProduccionKiloPie;
        entity.DiasEngorde = request.DiasEngorde;
        entity.DiasEnGranja = request.DiasEnGranja;
        entity.RegistradoPorUserId = request.RegistradoPorUserId;

        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<ReporteIndicadoresPanamaDto?> GetReporteAsync(int loteAveEngordeId, CancellationToken ct = default)
    {
        if (loteAveEngordeId <= 0)
            throw new InvalidOperationException("loteAveEngordeId es requerido.");

        var rows = await _context.Database
            .SqlQueryRaw<ReporteIndicadoresPanamaRow>(
                "SELECT * FROM fn_reporte_indicadores_panama({0}::int)", loteAveEngordeId)
            .ToListAsync(ct);

        var r = rows.FirstOrDefault();
        return r?.ToDto();
    }
}
