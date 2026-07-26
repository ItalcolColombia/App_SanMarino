using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio de traslados de aves desde la pantalla "Seguimiento Diario".
///
/// Feature 13 — Reescrito para:
///   1. Validar misma etapa (Levante→Levante o Producción→Producción).
///   2. Usar el saldo REAL (no aves_h_actual del encasetamiento) — obtenido
///      vía ILoteService.GetMortalidadResumenAsync que ya incluye traslados.
///   3. Generar DOS registros en seguimiento_diario (SALIDA en origen,
///      INGRESO en destino) con es_traslado=true y dirección.
///   4. Actualizar los acumulados traslado_ingreso_/salida_ en lote_postura_levante.
///   5. Mantener aves_h_actual / aves_m_actual para compatibilidad con código legacy.
///
/// Santa Reyes (Fase 3) — el traslado CROSS-ETAPA (Levante→Producción) se habilita por empresa
/// (<c>companies.permite_traslado_aves_cross_etapa</c>) y TODO traslado registra una cohorte en el
/// lote destino para conservar la edad de las aves recibidas. Ver los partial de <c>Funciones/</c>.
///
/// Este archivo es el ANCLA del <c>partial class</c>: usings, campos, ctor, interfaz y las
/// consultas de disponibilidad. La ejecución del traslado vive en
/// <c>Funciones/TrasladoAvesDesdeSegService.Traslado.cs</c> y las cohortes en
/// <c>Funciones/TrasladoAvesDesdeSegService.Cohortes.cs</c>.
/// </summary>
public partial class TrasladoAvesDesdeSegService : ITrasladoAvesDesdeSegService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;
    private readonly ILoteService _loteService;

    public TrasladoAvesDesdeSegService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ICompanyResolver companyResolver,
        ILoteService loteService)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
        _loteService = loteService;
    }

    /// <summary>
    /// Datos del lado (origen o destino) de un traslado, independientes de la etapa: permiten
    /// componer patas Levante y Producción sin duplicar el código de cada camino.
    /// </summary>
    /// <param name="EspejoId">Id del espejo: <c>lote_postura_levante_id</c> o <c>lote_postura_produccion_id</c>.</param>
    /// <param name="LoteBaseId">Id del lote base (<c>lotes.lote_id</c>) asociado al espejo.</param>
    /// <param name="GranjaId">Granja del espejo.</param>
    /// <param name="LoteNombre">Nombre del lote (para las observaciones del seguimiento).</param>
    /// <param name="FechaEncasetEspejo">Fecha de encasetamiento registrada en el espejo (fallback de la cohorte).</param>
    private sealed record LadoTraslado(
        int EspejoId,
        int LoteBaseId,
        int GranjaId,
        string LoteNombre,
        DateTime? FechaEncasetEspejo);

    private async Task<int> GetEffectiveCompanyIdAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    /// <summary>
    /// Empresa dueña de una granja (<c>farms.company_id</c>) — misma resolución que usa el consumo de
    /// inventario. Devuelve <c>null</c> si la granja no existe o no tiene empresa válida (fail-closed:
    /// quien la usa decide, nunca asume una empresa).
    /// </summary>
    private async Task<int?> ResolverCompanyIdDeGranjaAsync(int granjaId, CancellationToken ct)
    {
        var companyId = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync(ct);
        return companyId is null or <= 0 ? null : companyId;
    }

    public async Task<DisponibilidadAvesDto?> GetDisponibilidadAvesAsync(
        int loteId, string tipo, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);

        if (tipo.Equals("Levante", StringComparison.OrdinalIgnoreCase))
        {
            var lpl = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Include(l => l.Farm)
                .Where(l => l.LotePosturaLevanteId == loteId
                         && l.CompanyId == companyId
                         && l.DeletedAt == null)
                .FirstOrDefaultAsync(ct);

            if (lpl is null) return null;

            // ── Saldo REAL: si el LPL tiene Lote base asociado, usar resumen-mortalidad
            int avesHReal = lpl.AvesHActual ?? 0;
            int avesMReal = lpl.AvesMActual ?? 0;
            if (lpl.LoteId is int loteBaseId)
            {
                var resumen = await _loteService.GetMortalidadResumenAsync(loteBaseId);
                if (resumen != null)
                {
                    avesHReal = resumen.SaldoHembras;
                    avesMReal = resumen.SaldoMachos;
                }
            }

            return new DisponibilidadAvesDto(
                LoteId: loteId,
                LoteNombre: lpl.LoteNombre,
                TipoLote: "Levante",
                AvesHActual: avesHReal,
                AvesMActual: avesMReal,
                GranjaId: lpl.GranjaId,
                GranjaNombre: lpl.Farm?.Name,
                GalponId: lpl.GalponId,
                GalponNombre: null
            );
        }
        else
        {
            var lpp = await _ctx.LotePosturaProduccion
                .AsNoTracking()
                .Include(l => l.Farm)
                .Where(l => l.LotePosturaProduccionId == loteId
                         && l.CompanyId == companyId
                         && l.DeletedAt == null)
                .FirstOrDefaultAsync(ct);

            if (lpp is null) return null;

            return new DisponibilidadAvesDto(
                LoteId: loteId,
                LoteNombre: lpp.LoteNombre,
                TipoLote: "Produccion",
                AvesHActual: lpp.AvesHActual ?? 0,
                AvesMActual: lpp.AvesMActual ?? 0,
                GranjaId: lpp.GranjaId,
                GranjaNombre: lpp.Farm?.Name,
                GalponId: lpp.GalponId,
                GalponNombre: null
            );
        }
    }
}
