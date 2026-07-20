// src/ZooSanMarino.Infrastructure/Services/ReporteIndicadorPanamaService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
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
    private readonly ICurrentUser _currentUser;

    public ReporteIndicadorPanamaService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
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

        // OJO: la fn devuelve numerics sin redondear y los derivados encadenados (eef_dos, etc.)
        // llegan a 36+ decimales → System.Decimal no los soporta y Npgsql lanza Overflow al leer.
        // Se acota cada numeric a numeric(18,6) EN el SELECT (la UI muestra 2 decimales; el
        // redondeo a 6 no cambia ningún valor visible). Sin esto, el reporte 500ea con datos reales.
        var rows = await _context.Database
            .SqlQueryRaw<ReporteIndicadoresPanamaRow>(
                """
                SELECT
                    id,
                    id_usuario_registro,
                    id_lote,
                    metros_cuadrados::numeric(18,6)       AS metros_cuadrados,
                    aves_final_granja::numeric(18,6)      AS aves_final_granja,
                    produccion_kilo_pie::numeric(18,6)    AS produccion_kilo_pie,
                    dias_engorde,
                    dias_en_granja,
                    aves_beneficiada,
                    peso_promedio::numeric(18,6)          AS peso_promedio,
                    mortalidad_porc::numeric(18,6)        AS mortalidad_porc,
                    seleccion_porc::numeric(18,6)         AS seleccion_porc,
                    porc_mortalidad_total::numeric(18,6)  AS porc_mortalidad_total,
                    supervivencia::numeric(18,6)          AS supervivencia,
                    consumo_ave::numeric(18,6)            AS consumo_ave,
                    conversion::numeric(18,6)             AS conversion,
                    eficiencia_americana::numeric(18,6)   AS eficiencia_americana,
                    eef::numeric(18,6)                    AS eef,
                    eef_dos::numeric(18,6)                AS eef_dos,
                    aves_metros_cua::numeric(18,6)        AS aves_metros_cua,
                    kilos_metros_cua::numeric(18,6)       AS kilos_metros_cua,
                    productividad::numeric(18,6)          AS productividad,
                    faltante_sobra::numeric(18,6)         AS faltante_sobra,
                    consumo_alimento_total::numeric(18,6) AS consumo_alimento_total,
                    total_aves_seleccion::numeric(18,6)   AS total_aves_seleccion,
                    total_aves_muertas::numeric(18,6)     AS total_aves_muertas,
                    aves_encasetadas
                FROM fn_reporte_indicadores_panama({0}::int)
                """, loteAveEngordeId)
            .ToListAsync(ct);

        var r = rows.FirstOrDefault();
        return r?.ToDto();
    }

    public async Task<ReporteCorridaPanamaDto?> GetReportePorCorridaAsync(
        int granjaId, string corrida, string? nucleoId = null, string? galponId = null,
        CancellationToken ct = default)
    {
        if (granjaId <= 0)
            throw new InvalidOperationException("granjaId es requerido y debe ser mayor a 0.");
        var nombre = (corrida ?? string.Empty).Trim();
        if (nombre.Length == 0)
            throw new InvalidOperationException("corrida es requerida.");

        // En Panamá el lote_nombre ES el número de corrida (se repite entre granjas):
        // el alcance SIEMPRE va acotado por empresa activa + granja (match exacto del nombre).
        var query = _context.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == _currentUser.CompanyId &&
                        l.GranjaId == granjaId &&
                        l.DeletedAt == null &&
                        l.LoteNombre.Trim() == nombre);
        if (!string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(l => l.NucleoId != null && l.NucleoId.Trim() == nucleoId.Trim());
        if (!string.IsNullOrWhiteSpace(galponId))
            query = query.Where(l => l.GalponId != null && l.GalponId.Trim() == galponId.Trim());

        var lotes = await query
            .OrderBy(l => l.GalponId).ThenBy(l => l.LoteAveEngordeId)
            .Select(l => new
            {
                Id = l.LoteAveEngordeId ?? 0,
                l.LoteNombre,
                l.GalponId,
                l.FechaEncaset
            })
            .ToListAsync(ct);

        if (lotes.Count == 0) return null;

        // ≤ ~4 lotes por corrida (uno por galpón): la fn se ejecuta por lote, en secuencia (DbContext).
        var items = new List<ReporteCorridaPanamaItemDto>();
        var sinLiquidacion = new List<LoteCorridaPanamaResumenDto>();
        foreach (var l in lotes)
        {
            var reporte = await GetReporteAsync(l.Id, ct);
            if (reporte is null)
                sinLiquidacion.Add(new LoteCorridaPanamaResumenDto(l.Id, l.LoteNombre, l.GalponId, l.FechaEncaset));
            else
                items.Add(new ReporteCorridaPanamaItemDto(l.Id, l.LoteNombre, l.GalponId, l.FechaEncaset, reporte));
        }

        var consolidado = ReporteIndicadorPanamaCalculos.ConsolidarCorrida(
            items.Select(i => i.Reporte).ToList());

        return new ReporteCorridaPanamaDto(nombre, granjaId, items, sinLiquidacion, consolidado);
    }
}
