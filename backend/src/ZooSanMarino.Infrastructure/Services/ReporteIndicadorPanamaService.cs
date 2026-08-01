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
    private readonly ILocationScopeResolver _scopeResolver;

    public ReporteIndicadorPanamaService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        ILocationScopeResolver scopeResolver)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeResolver = scopeResolver;
    }

    public async Task<int> GuardarLiquidacionAsync(GuardarLiquidacionPanamaRequest request, CancellationToken ct = default)
    {
        if (request.LoteAveEngordeId <= 0)
            throw new InvalidOperationException("LoteAveEngordeId es requerido.");

        // Empresa efectiva + alcance granular FAIL-CLOSED (antes cualquier autenticado escribía la
        // liquidación de cualquier lote — fuga multi-empresa). Mismo patrón que
        // GetReportePorCorridaAsync. Y gate B9: los 6 insumos se digitan ANTES de cerrar (el modal
        // llama /liquidar y después /cerrar); tras liquidar quedan congelados con la copia.
        var lote = await ResolverLoteDeLaEmpresaYAlcanceAsync(request.LoteAveEngordeId, ct)
            ?? throw new InvalidOperationException("Lote no existe o no pertenece a la compañía.");
        LiquidacionCongeladaGateCalculos.ValidarEscritura(
            lote.EstadoOperativoLote, OperacionLoteEngordeLiquidado.LiquidacionInsumosPanama);

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

    /// <summary>
    /// El lote existe, pertenece a la EMPRESA ACTIVA y está dentro del alcance granular del
    /// usuario. <c>null</c> = no visible (fail-closed). Mismo criterio de ubicación que
    /// <see cref="GetReportePorCorridaAsync"/>: engorde se gobierna por galpón/núcleo.
    /// </summary>
    private async Task<LoteGateInfo?> ResolverLoteDeLaEmpresaYAlcanceAsync(int loteAveEngordeId, CancellationToken ct)
    {
        var lote = await _context.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteAveEngordeId &&
                        l.CompanyId == _currentUser.CompanyId &&
                        l.DeletedAt == null)
            .Select(l => new LoteGateInfo(l.GranjaId, l.NucleoId, l.GalponId, l.EstadoOperativoLote))
            .SingleOrDefaultAsync(ct);
        if (lote is null) return null;

        var scope = await _scopeResolver.GetScopeAsync(lote.GranjaId);
        if (!scope.IsGlobal)
        {
            var galpon = (lote.GalponId ?? "").Trim();
            var nucleo = (lote.NucleoId ?? "").Trim();
            var visible = galpon.Length > 0
                ? scope.GalponesVisibles.Contains(galpon)
                : nucleo.Length > 0 && scope.NucleosVisibles.Contains(nucleo);
            if (!visible) return null;
        }
        return lote;
    }

    private sealed record LoteGateInfo(int GranjaId, string? NucleoId, string? GalponId, string? EstadoOperativoLote);

    public async Task<ReporteIndicadoresPanamaDto?> GetReporteAsync(int loteAveEngordeId, CancellationToken ct = default)
    {
        if (loteAveEngordeId <= 0)
            throw new InvalidOperationException("loteAveEngordeId es requerido.");

        // Fail-closed multi-empresa: un lote de otra empresa o fuera del alcance no devuelve datos.
        if (await ResolverLoteDeLaEmpresaYAlcanceAsync(loteAveEngordeId, ct) is null) return null;

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

        // Alcance granular: en una granja restringida la corrida se acota a los galpones/núcleos
        // visibles (engorde se gobierna por ubicación, no por la tabla lotes). Fail-closed.
        var scope = await _scopeResolver.GetScopeAsync(granjaId);
        if (!scope.IsGlobal)
        {
            var galponesVisibles = scope.GalponesVisibles.ToList();
            var nucleosVisibles = scope.NucleosVisibles.ToList();
            query = query.Where(l =>
                (l.GalponId != null && l.GalponId != "" && galponesVisibles.Contains(l.GalponId))
                || ((l.GalponId == null || l.GalponId == "") && l.NucleoId != null && nucleosVisibles.Contains(l.NucleoId)));
        }

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
