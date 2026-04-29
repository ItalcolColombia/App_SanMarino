using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LiquidacionCierreLoteLevanteService : ILiquidacionCierreLoteLevanteService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    public LiquidacionCierreLoteLevanteService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    public async Task<LiquidacionCierreLoteLevanteDto> CalcularAsync(int lotePosturaLevanteId, CancellationToken ct = default)
    {
        var lote = await ObtenerLoteAsync(lotePosturaLevanteId, ct);
        var seguimientos = await ObtenerSeguimientosAsync(lotePosturaLevanteId, lote.FechaEncaset, ct);
        var guia = await ObtenerGuiaGeneticaAsync(lote.Raza, lote.AnoTablaGenetica, ct);
        return Calcular(lote, seguimientos, guia);
    }

    public async Task<LiquidacionCierreGuardadaDto> GuardarAsync(int lotePosturaLevanteId, CancellationToken ct = default)
    {
        var datos = await CalcularAsync(lotePosturaLevanteId, ct);

        var existente = await _ctx.LiquidacionCierreLoteLevante
            .FirstOrDefaultAsync(x => x.LotePosturaLevanteId == lotePosturaLevanteId, ct);

        var ahora = DateTime.UtcNow;

        if (existente is null)
        {
            existente = new Domain.Entities.LiquidacionCierreLoteLevante
            {
                LotePosturaLevanteId = lotePosturaLevanteId,
                CompanyId = _current.CompanyId,
                CreatedByUserId = _current.UserId,
                ClosedByUserId = _current.UserId,
                CreatedAt = ahora
            };
            _ctx.LiquidacionCierreLoteLevante.Add(existente);
        }
        else
        {
            existente.UpdatedAt = ahora;
            existente.ClosedByUserId = _current.UserId;
        }

        MapearDatos(existente, datos, ahora);
        await _ctx.SaveChangesAsync(ct);

        return new LiquidacionCierreGuardadaDto(
            existente.Id,
            lotePosturaLevanteId,
            existente.FechaCierre,
            existente.ClosedByUserId,
            datos);
    }

    public async Task<LiquidacionCierreGuardadaDto?> ObtenerPorLoteAsync(int lotePosturaLevanteId, CancellationToken ct = default)
    {
        var registro = await _ctx.LiquidacionCierreLoteLevante
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LotePosturaLevanteId == lotePosturaLevanteId, ct);

        if (registro is null) return null;

        var datos = await CalcularAsync(lotePosturaLevanteId, ct);
        return new LiquidacionCierreGuardadaDto(registro.Id, lotePosturaLevanteId, registro.FechaCierre, registro.ClosedByUserId, datos);
    }

    // ─── Privados ────────────────────────────────────────────────────────────

    private async Task<Domain.Entities.LotePosturaLevante> ObtenerLoteAsync(int id, CancellationToken ct)
    {
        var lote = await _ctx.LotePosturaLevante
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == id &&
                                      l.CompanyId == _current.CompanyId &&
                                      l.DeletedAt == null, ct);

        if (lote is null)
            throw new InvalidOperationException($"Lote levante {id} no encontrado.");

        return lote;
    }

    private async Task<List<Domain.Entities.SeguimientoDiario>> ObtenerSeguimientosAsync(
        int lotePosturaLevanteId,
        DateTime? fechaEncaset,
        CancellationToken ct)
    {
        var query = _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.LotePosturaLevanteId == lotePosturaLevanteId &&
                        s.TipoSeguimiento == "levante");

        // Máximo semana 25 = 175 días desde encasetamiento
        if (fechaEncaset.HasValue)
        {
            var fechaMax = fechaEncaset.Value.AddDays(175);
            query = query.Where(s => s.Fecha <= fechaMax);
        }

        return await query.OrderBy(s => s.Fecha).ToListAsync(ct);
    }

    /// <summary>
    /// Busca la fila de guía genética para la semana 25 (Edad = "25").
    /// Aplica normalización de nombres de raza para tolerar variaciones de escritura
    /// (igual lógica que LiquidacionTecnicaComparacionService).
    /// </summary>
    private async Task<GuiaLevante?> ObtenerGuiaGeneticaAsync(string? raza, int? ano, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(raza) || !ano.HasValue) return null;

        // Construir lista de nombres alternativos para la raza
        var variantes = NormalizarRaza(raza);
        var anoStr = ano.Value.ToString();

        // Formatos de Edad usados para semana 25
        var edades = new[] { "25", "25.0", "175" };

        foreach (var edad in edades)
        {
            var g = await _ctx.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p => p.CompanyId == _current.CompanyId &&
                            p.DeletedAt == null &&
                            variantes.Contains(p.Raza!) &&
                            p.AnioGuia == anoStr &&
                            p.Edad == edad)
                .FirstOrDefaultAsync(ct);

            if (g is null) continue;

            return new GuiaLevante(
                ConsumoAcH: ParseDec(g.ConsAcH),       // Consumo acumulado g/ave semanas 1-25
                GrAveDiaH:  ParseDec(g.GrAveDiaH),     // Consumo diario g/ave en semana 25
                RetiroAcH:  ParseDec(g.RetiroAcH),     // % retiro acumulado guía
                PesoH:      ParseDec(g.PesoH),         // Peso hembras semana 25
                Uniformidad:ParseDec(g.Uniformidad));   // Uniformidad semana 25
        }

        // Fallback: misma raza+año pero sin filtro Edad (puede devolver la semana más cercana a 25)
        {
            var g = await _ctx.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p => p.CompanyId == _current.CompanyId &&
                            p.DeletedAt == null &&
                            variantes.Contains(p.Raza!) &&
                            p.AnioGuia == anoStr)
                .OrderByDescending(p => p.Edad)         // la semana más alta disponible
                .FirstOrDefaultAsync(ct);

            if (g is null) return null;

            return new GuiaLevante(
                ParseDec(g.ConsAcH),
                ParseDec(g.GrAveDiaH),
                ParseDec(g.RetiroAcH),
                ParseDec(g.PesoH),
                ParseDec(g.Uniformidad));
        }
    }

    /// <summary>Genera variantes del nombre de raza para tolerancia de escritura.</summary>
    private static List<string> NormalizarRaza(string raza)
    {
        var variantes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { raza };

        var r = raza.ToUpperInvariant();
        if (r.Contains("ROSS") && r.Contains("308"))
            variantes.UnionWith(new[] { "R308", "Ross 308", "Ross308", "ROSS 308", "Ross 308 AP" });
        else if (r.Contains("ROSS") && r.Contains("708"))
            variantes.UnionWith(new[] { "R708", "Ross 708", "ROSS 708" });
        else if (r.Contains("COBB") && r.Contains("500"))
            variantes.UnionWith(new[] { "C500", "Cobb 500", "Cobb500", "COBB 500" });
        else if (r.Contains("COBB") && r.Contains("700"))
            variantes.UnionWith(new[] { "C700", "Cobb 700", "COBB 700" });
        else if (r.Contains("AP") || r.Contains("ARBOR"))
            variantes.UnionWith(new[] { "AP", "Arbor Acres Plus", "Arbor Acres" });

        return variantes.ToList();
    }

    private static LiquidacionCierreLoteLevanteDto Calcular(
        Domain.Entities.LotePosturaLevante lote,
        List<Domain.Entities.SeguimientoDiario> segs,
        GuiaLevante? guia)
    {
        var hembrasIni = lote.HembrasL ?? 0;
        var machosIni = lote.MachosL ?? 0;

        // ── Retiro acumulado hembras ──────────────────────────────────────────
        var totalMortH = segs.Sum(s => s.MortalidadHembras ?? 0);
        var totalSelH  = segs.Sum(s => s.SelH ?? 0);
        var totalErrH  = segs.Sum(s => s.ErrorSexajeHembras ?? 0);

        decimal porcMortH  = hembrasIni > 0 ? (decimal)totalMortH / hembrasIni * 100 : 0;
        decimal porcSelH   = hembrasIni > 0 ? (decimal)totalSelH  / hembrasIni * 100 : 0;
        decimal porcErrH   = hembrasIni > 0 ? (decimal)totalErrH  / hembrasIni * 100 : 0;
        decimal porcRetiro = porcMortH + porcSelH + porcErrH;

        // ── Consumo acumulado real (kg → g, total del lote) ──────────────────
        // La guía ConsAcH también está en g totales (o g/ave según carga).
        // Mantenemos la misma unidad que LiquidacionTecnicaService para coherencia.
        decimal consumoRealGramos = segs.Sum(s => (decimal)(s.ConsumoKgHembras ?? 0)) * 1000m;

        // ── Último registro de la semana más avanzada ────────────────────────
        var ultimo   = segs.LastOrDefault();
        decimal? pesoReal = ultimo?.PesoPromHembras  is { } ph ? (decimal)ph : null;
        decimal? unifReal = ultimo?.UniformidadHembras is { } uh ? (decimal)uh : null;

        // Semana del último registro
        int? semanaUltimo = null;
        if (lote.FechaEncaset.HasValue && ultimo is not null)
            semanaUltimo = (int)((ultimo.Fecha - lote.FechaEncaset.Value).TotalDays / 7) + 1;

        // ── Comparación con guía ──────────────────────────────────────────────
        decimal? consumoGuia       = guia?.ConsumoAcH;
        decimal? grAveDia25Guia    = guia?.GrAveDiaH;
        decimal? porcDifConsumo    = DifPorc(consumoRealGramos, consumoGuia);
        decimal? pesoGuia          = guia?.PesoH;
        decimal? porcDifPeso       = DifPorc(pesoReal, pesoGuia);
        decimal? unifGuia          = guia?.Uniformidad;
        decimal? porcDifUnif       = DifPorc(unifReal, unifGuia);
        decimal? porcRetiroGuia    = guia?.RetiroAcH;

        return new LiquidacionCierreLoteLevanteDto(
            LotePosturaLevanteId: lote.LotePosturaLevanteId!.Value,
            LoteNombre:           lote.LoteNombre,
            Raza:                 lote.Raza,
            AnoTablaGenetica:     lote.AnoTablaGenetica,
            HembrasEncasetadas:   hembrasIni > 0 ? hembrasIni : null,
            MachosEncasetados:    machosIni  > 0 ? machosIni  : null,

            PorcentajeMortalidadHembras:  Math.Round(porcMortH,  2),
            PorcentajeSeleccionHembras:   Math.Round(porcSelH,   2),
            PorcentajeErrorSexajeHembras: Math.Round(porcErrH,   2),
            PorcentajeRetiroAcumulado:    Math.Round(porcRetiro, 2),
            PorcentajeRetiroGuia:         porcRetiroGuia.HasValue ? Math.Round(porcRetiroGuia.Value, 2) : null,

            ConsumoAlimentoRealGramos:    Math.Round(consumoRealGramos, 0),
            ConsumoAlimentoGuiaGramos:    consumoGuia.HasValue    ? Math.Round(consumoGuia.Value,    0) : null,
            PorcentajeDiferenciaConsumo:  porcDifConsumo.HasValue ? Math.Round(porcDifConsumo.Value, 2) : null,
            ConsumoGrAveDiaSemana25Guia:  grAveDia25Guia.HasValue ? Math.Round(grAveDia25Guia.Value, 1) : null,

            PesoSemana25Real:             pesoReal.HasValue  ? Math.Round(pesoReal.Value,  1) : null,
            PesoSemana25Guia:             pesoGuia.HasValue  ? Math.Round(pesoGuia.Value,  1) : null,
            PorcentajeDiferenciaPeso:     porcDifPeso.HasValue ? Math.Round(porcDifPeso.Value, 2) : null,

            UniformidadReal:              unifReal.HasValue  ? Math.Round(unifReal.Value,  1) : null,
            UniformidadGuia:              unifGuia.HasValue  ? Math.Round(unifGuia.Value,  1) : null,
            PorcentajeDiferenciaUniformidad: porcDifUnif.HasValue ? Math.Round(porcDifUnif.Value, 2) : null,

            FechaCalculo:               DateTime.UtcNow,
            TotalRegistrosSeguimiento:  segs.Count,
            SemanaUltimoRegistro:       semanaUltimo,
            TieneGuiaGenetica:          guia is not null);
    }

    private static void MapearDatos(Domain.Entities.LiquidacionCierreLoteLevante e, LiquidacionCierreLoteLevanteDto d, DateTime ahora)
    {
        e.FechaCierre                   = ahora;
        e.HembrasEncasetadas            = d.HembrasEncasetadas;
        e.MachosEncasetados             = d.MachosEncasetados;
        e.PorcentajeMortalidadHembras   = d.PorcentajeMortalidadHembras;
        e.PorcentajeSeleccionHembras    = d.PorcentajeSeleccionHembras;
        e.PorcentajeErrorSexajeHembras  = d.PorcentajeErrorSexajeHembras;
        e.PorcentajeRetiroAcumulado     = d.PorcentajeRetiroAcumulado;
        e.PorcentajeRetiroGuia          = d.PorcentajeRetiroGuia;
        e.ConsumoAlimentoRealGramos     = d.ConsumoAlimentoRealGramos;
        e.ConsumoAlimentoGuiaGramos     = d.ConsumoAlimentoGuiaGramos;
        e.ConsumoGrAveDiaSemana25Guia   = d.ConsumoGrAveDiaSemana25Guia;
        e.PorcentajeDiferenciaConsumo   = d.PorcentajeDiferenciaConsumo;
        e.PesoSemana25Real              = d.PesoSemana25Real;
        e.PesoSemana25Guia              = d.PesoSemana25Guia;
        e.PorcentajeDiferenciaPeso      = d.PorcentajeDiferenciaPeso;
        e.UniformidadReal               = d.UniformidadReal;
        e.UniformidadGuia               = d.UniformidadGuia;
        e.PorcentajeDiferenciaUniformidad = d.PorcentajeDiferenciaUniformidad;
        e.RazaGuia                      = d.Raza;
        e.AnoGuia                       = d.AnoTablaGenetica;

        e.Metadata = JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(new
            {
                semanaUltimoRegistro = d.SemanaUltimoRegistro,
                totalRegistros       = d.TotalRegistrosSeguimiento,
                tieneGuia            = d.TieneGuiaGenetica
            }));
    }

    private static decimal? DifPorc(decimal? real, decimal? guia)
    {
        if (real is null || guia is null || guia.Value == 0) return null;
        return (real.Value - guia.Value) / guia.Value * 100;
    }

    private static decimal? ParseDec(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        return decimal.TryParse(v.Replace(",", "."),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : null;
    }

    private record GuiaLevante(
        decimal? ConsumoAcH,    // ConsAcH  — consumo acumulado g/ave semanas 1-25
        decimal? GrAveDiaH,     // GrAveDiaH — g/ave/día en semana 25
        decimal? RetiroAcH,     // RetiroAcH — % retiro acumulado
        decimal? PesoH,         // PesoH     — peso hembras semana 25
        decimal? Uniformidad);  // Uniformidad — uniformidad semana 25
}
