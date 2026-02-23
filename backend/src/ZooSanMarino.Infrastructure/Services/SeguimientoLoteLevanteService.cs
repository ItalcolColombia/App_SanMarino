// Seguimiento Diario Levante: persiste en la tabla unificada seguimiento_diario (tipo = 'levante')
// usando ISeguimientoDiarioService. La API y DTOs del módulo Levante se mantienen igual.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoLoteLevanteService : ISeguimientoLoteLevanteService
{
    private const string TipoLevante = "levante";

    private readonly ZooSanMarinoContext _ctx;
    private readonly ISeguimientoDiarioService _seguimientoDiarioService;
    private readonly IAlimentoNutricionProvider _alimentos;
    private readonly IGramajeProvider _gramaje;
    private readonly ICurrentUser _current;
    private readonly IMovimientoAvesService _movimientoAvesService;

    public SeguimientoLoteLevanteService(
        ZooSanMarinoContext ctx,
        ISeguimientoDiarioService seguimientoDiarioService,
        IAlimentoNutricionProvider alimentos,
        IGramajeProvider gramaje,
        ICurrentUser current,
        IMovimientoAvesService movimientoAvesService)
    {
        _ctx = ctx;
        _seguimientoDiarioService = seguimientoDiarioService;
        _alimentos = alimentos;
        _gramaje = gramaje;
        _current = current;
        _movimientoAvesService = movimientoAvesService;
    }

    private static SeguimientoLoteLevanteDto MapToLevanteDto(SeguimientoDiarioDto u)
    {
        return new SeguimientoLoteLevanteDto(
            Id: (int)u.Id,
            LoteId: int.Parse(u.LoteId),
            LotePosturaLevanteId: u.LotePosturaLevanteId,
            FechaRegistro: u.Fecha,
            MortalidadHembras: u.MortalidadHembras ?? 0,
            MortalidadMachos: u.MortalidadMachos ?? 0,
            SelH: u.SelH ?? 0,
            SelM: u.SelM ?? 0,
            ErrorSexajeHembras: u.ErrorSexajeHembras ?? 0,
            ErrorSexajeMachos: u.ErrorSexajeMachos ?? 0,
            ConsumoKgHembras: (double)(u.ConsumoKgHembras ?? 0),
            TipoAlimento: u.TipoAlimento ?? "",
            Observaciones: u.Observaciones,
            KcalAlH: u.KcalAlH,
            ProtAlH: u.ProtAlH,
            KcalAveH: u.KcalAveH,
            ProtAveH: u.ProtAveH,
            Ciclo: u.Ciclo ?? "Normal",
            ConsumoKgMachos: u.ConsumoKgMachos.HasValue ? (double)u.ConsumoKgMachos.Value : null,
            PesoPromH: u.PesoPromHembras,
            PesoPromM: u.PesoPromMachos,
            UniformidadH: u.UniformidadHembras,
            UniformidadM: u.UniformidadMachos,
            CvH: u.CvHembras,
            CvM: u.CvMachos,
            Metadata: u.Metadata,
            ItemsAdicionales: u.ItemsAdicionales,
            ConsumoAguaDiario: u.ConsumoAguaDiario,
            ConsumoAguaPh: u.ConsumoAguaPh,
            ConsumoAguaOrp: u.ConsumoAguaOrp,
            ConsumoAguaTemperatura: u.ConsumoAguaTemperatura
        );
    }

    private static CreateSeguimientoDiarioDto MapToCreateUnificado(SeguimientoLoteLevanteDto dto,
        double consumoKgHembras, double? kcalAlH, double? protAlH, double? kcalAveH, double? protAveH)
    {
        return new CreateSeguimientoDiarioDto(
            TipoSeguimiento: TipoLevante,
            LoteId: dto.LoteId.ToString(),
            LotePosturaLevanteId: dto.LotePosturaLevanteId,
            LotePosturaProduccionId: null,
            ReproductoraId: null,
            Fecha: dto.FechaRegistro,
            MortalidadHembras: dto.MortalidadHembras,
            MortalidadMachos: dto.MortalidadMachos,
            SelH: dto.SelH,
            SelM: dto.SelM,
            ErrorSexajeHembras: dto.ErrorSexajeHembras,
            ErrorSexajeMachos: dto.ErrorSexajeMachos,
            ConsumoKgHembras: (decimal)consumoKgHembras,
            ConsumoKgMachos: dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento: dto.TipoAlimento,
            Observaciones: dto.Observaciones,
            Ciclo: dto.Ciclo,
            PesoPromHembras: dto.PesoPromH,
            PesoPromMachos: dto.PesoPromM,
            UniformidadHembras: dto.UniformidadH,
            UniformidadMachos: dto.UniformidadM,
            CvHembras: dto.CvH,
            CvMachos: dto.CvM,
            ConsumoAguaDiario: dto.ConsumoAguaDiario,
            ConsumoAguaPh: dto.ConsumoAguaPh,
            ConsumoAguaOrp: dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura: dto.ConsumoAguaTemperatura,
            Metadata: dto.Metadata,
            ItemsAdicionales: dto.ItemsAdicionales,
            PesoInicial: null,
            PesoFinal: null,
            KcalAlH: kcalAlH,
            ProtAlH: protAlH,
            KcalAveH: kcalAveH,
            ProtAveH: protAveH,
            HuevoTot: null,
            HuevoInc: null,
            HuevoLimpio: null,
            HuevoTratado: null,
            HuevoSucio: null,
            HuevoDeforme: null,
            HuevoBlanco: null,
            HuevoDobleYema: null,
            HuevoPiso: null,
            HuevoPequeno: null,
            HuevoRoto: null,
            HuevoDesecho: null,
            HuevoOtro: null,
            PesoHuevo: null,
            Etapa: null,
            PesoH: null,
            PesoM: null,
            Uniformidad: null,
            CoeficienteVariacion: null,
            ObservacionesPesaje: null,
            CreatedByUserId: dto.CreatedByUserId
        );
    }

    private static UpdateSeguimientoDiarioDto MapToUpdateUnificado(SeguimientoLoteLevanteDto dto,
        double consumoKgHembras, double? kcalAlH, double? protAlH, double? kcalAveH, double? protAveH)
    {
        return new UpdateSeguimientoDiarioDto(
            Id: (long)dto.Id,
            TipoSeguimiento: TipoLevante,
            LoteId: dto.LoteId.ToString(),
            LotePosturaLevanteId: dto.LotePosturaLevanteId,
            LotePosturaProduccionId: null,
            ReproductoraId: null,
            Fecha: dto.FechaRegistro,
            MortalidadHembras: dto.MortalidadHembras,
            MortalidadMachos: dto.MortalidadMachos,
            SelH: dto.SelH,
            SelM: dto.SelM,
            ErrorSexajeHembras: dto.ErrorSexajeHembras,
            ErrorSexajeMachos: dto.ErrorSexajeMachos,
            ConsumoKgHembras: (decimal)consumoKgHembras,
            ConsumoKgMachos: dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento: dto.TipoAlimento,
            Observaciones: dto.Observaciones,
            Ciclo: dto.Ciclo,
            PesoPromHembras: dto.PesoPromH,
            PesoPromMachos: dto.PesoPromM,
            UniformidadHembras: dto.UniformidadH,
            UniformidadMachos: dto.UniformidadM,
            CvHembras: dto.CvH,
            CvMachos: dto.CvM,
            ConsumoAguaDiario: dto.ConsumoAguaDiario,
            ConsumoAguaPh: dto.ConsumoAguaPh,
            ConsumoAguaOrp: dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura: dto.ConsumoAguaTemperatura,
            Metadata: dto.Metadata,
            ItemsAdicionales: dto.ItemsAdicionales,
            PesoInicial: null,
            PesoFinal: null,
            KcalAlH: kcalAlH,
            ProtAlH: protAlH,
            KcalAveH: kcalAveH,
            ProtAveH: protAveH,
            HuevoTot: null,
            HuevoInc: null,
            HuevoLimpio: null,
            HuevoTratado: null,
            HuevoSucio: null,
            HuevoDeforme: null,
            HuevoBlanco: null,
            HuevoDobleYema: null,
            HuevoPiso: null,
            HuevoPequeno: null,
            HuevoRoto: null,
            HuevoDesecho: null,
            HuevoOtro: null,
            PesoHuevo: null,
            Etapa: null,
            PesoH: null,
            PesoM: null,
            Uniformidad: null,
            CoeficienteVariacion: null,
            ObservacionesPesaje: null
        );
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteAsync(int loteId)
    {
        var filter = new SeguimientoDiarioFilterRequest
        {
            TipoSeguimiento = TipoLevante,
            LoteId = loteId.ToString(),
            Page = 1,
            PageSize = 10_000,
            OrderBy = "Fecha",
            OrderAsc = true
        };
        var paged = await _seguimientoDiarioService.GetFilteredAsync(filter);
        return paged.Items.Select(MapToLevanteDto).ToList();
    }

    public async Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id)
    {
        var u = await _seguimientoDiarioService.GetByIdAsync((long)id);
        if (u is null || u.TipoSeguimiento != TipoLevante)
            return null;
        return MapToLevanteDto(u);
    }

    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta)
    {
        var filter = new SeguimientoDiarioFilterRequest
        {
            TipoSeguimiento = TipoLevante,
            LoteId = loteId?.ToString(),
            FechaDesde = desde,
            FechaHasta = hasta,
            Page = 1,
            PageSize = 10_000,
            OrderBy = "Fecha",
            OrderAsc = true
        };
        var paged = await _seguimientoDiarioService.GetFilteredAsync(filter);
        return paged.Items.Select(MapToLevanteDto).ToList();
    }

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        double? kcalAlH = dto.KcalAlH, protAlH = dto.ProtAlH;
        if (kcalAlH is null || protAlH is null)
        {
            var np = await _alimentos.GetNutrientesAsync(dto.TipoAlimento);
            if (np.HasValue) { kcalAlH ??= np.Value.kcal; protAlH ??= np.Value.prot; }
        }

        double consumoKgH = dto.ConsumoKgHembras;
        if (consumoKgH <= 0 && !string.IsNullOrWhiteSpace(lote.GalponId) && lote.FechaEncaset.HasValue)
        {
            int semana = CalcularSemana(lote.FechaEncaset.Value, dto.FechaRegistro);
            double? gramajeGrAve = null;
            if (int.TryParse(lote.GalponId, out var galponIdInt))
                gramajeGrAve = await _gramaje.GetGramajeGrPorAveAsync(galponIdInt, semana, dto.TipoAlimento);
            else if (_gramaje is IGramajeProviderV2 v2)
                gramajeGrAve = await v2.GetGramajeGrPorAveAsync(lote.GalponId, semana, dto.TipoAlimento);
            if (gramajeGrAve.HasValue && gramajeGrAve.Value > 0)
            {
                int hembrasVivas = await CalcularHembrasVivasAsync(dto.LoteId);
                consumoKgH = Math.Round((gramajeGrAve.Value * hembrasVivas) / 1000.0, 3);
            }
        }

        var (kcalAveH, protAveH) = CalcularDerivados(consumoKgH, kcalAlH, protAlH);
        var createDto = MapToCreateUnificado(dto, consumoKgH, kcalAlH, protAlH, kcalAveH, protAveH);
        var created = await _seguimientoDiarioService.CreateAsync(createDto);

        var hembrasRetiro = dto.MortalidadHembras + dto.SelH + dto.ErrorSexajeHembras;
        var machosRetiro = dto.MortalidadMachos + dto.SelM + dto.ErrorSexajeMachos;
        if (hembrasRetiro > 0 || machosRetiro > 0)
        {
            try
            {
                await DescontarAvesEnLotePosturaLevanteAsync(dto.LoteId, dto.LotePosturaLevanteId, hembrasRetiro, machosRetiro);
            }
            catch (Exception ex) { Console.WriteLine($"Error al descontar aves en lote postura levante: {ex.Message}"); }
        }

        return MapToLevanteDto(created);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        double? kcalAlH = dto.KcalAlH, protAlH = dto.ProtAlH;
        if (kcalAlH is null || protAlH is null)
        {
            var np = await _alimentos.GetNutrientesAsync(dto.TipoAlimento);
            if (np.HasValue) { kcalAlH ??= np.Value.kcal; protAlH ??= np.Value.prot; }
        }

        double consumoKgH = dto.ConsumoKgHembras;
        if (consumoKgH <= 0 && !string.IsNullOrWhiteSpace(lote.GalponId) && lote.FechaEncaset.HasValue)
        {
            int semana = CalcularSemana(lote.FechaEncaset.Value, dto.FechaRegistro);
            double? gramajeGrAve = null;
            if (int.TryParse(lote.GalponId, out var galponIdInt))
                gramajeGrAve = await _gramaje.GetGramajeGrPorAveAsync(galponIdInt, semana, dto.TipoAlimento);
            else if (_gramaje is IGramajeProviderV2 v2)
                gramajeGrAve = await v2.GetGramajeGrPorAveAsync(lote.GalponId, semana, dto.TipoAlimento);
            if (gramajeGrAve.HasValue && gramajeGrAve.Value > 0)
            {
                int hembrasVivas = await CalcularHembrasVivasAsync(dto.LoteId);
                consumoKgH = Math.Round((gramajeGrAve.Value * hembrasVivas) / 1000.0, 3);
            }
        }

        var oldRec = await _seguimientoDiarioService.GetByIdAsync((long)dto.Id);
        var oldH = (oldRec?.MortalidadHembras ?? 0) + (oldRec?.SelH ?? 0) + (oldRec?.ErrorSexajeHembras ?? 0);
        var oldM = (oldRec?.MortalidadMachos ?? 0) + (oldRec?.SelM ?? 0) + (oldRec?.ErrorSexajeMachos ?? 0);

        var (kcalAveH, protAveH) = CalcularDerivados(consumoKgH, kcalAlH, protAlH);
        var updateDto = MapToUpdateUnificado(dto, consumoKgH, kcalAlH, protAlH, kcalAveH, protAveH);
        var updated = await _seguimientoDiarioService.UpdateAsync(updateDto);
        if (updated is null) return null;

        var newH = dto.MortalidadHembras + dto.SelH + dto.ErrorSexajeHembras;
        var newM = dto.MortalidadMachos + dto.SelM + dto.ErrorSexajeMachos;
        var deltaH = oldH - newH;
        var deltaM = oldM - newM;
        if (deltaH != 0 || deltaM != 0)
        {
            try
            {
                await AjustarAvesEnLotePosturaLevanteAsync(dto.LoteId, dto.LotePosturaLevanteId, deltaH, deltaM);
            }
            catch (Exception ex) { Console.WriteLine($"Error al ajustar aves en lote postura levante (actualización): {ex.Message}"); }
        }

        return MapToLevanteDto(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var rec = await _seguimientoDiarioService.GetByIdAsync((long)id);
        if (rec != null && rec.TipoSeguimiento == TipoLevante)
        {
            var hembras = (rec.MortalidadHembras ?? 0) + (rec.SelH ?? 0) + (rec.ErrorSexajeHembras ?? 0);
            var machos = (rec.MortalidadMachos ?? 0) + (rec.SelM ?? 0) + (rec.ErrorSexajeMachos ?? 0);
            if (hembras > 0 || machos > 0)
            {
                try
                {
                    await AjustarAvesEnLotePosturaLevanteAsync(int.Parse(rec.LoteId), rec.LotePosturaLevanteId, hembras, machos);
                }
                catch (Exception ex) { Console.WriteLine($"Error al restaurar aves al eliminar seguimiento levante: {ex.Message}"); }
            }
        }
        return await _seguimientoDiarioService.DeleteAsync((long)id);
    }

    /// <summary>
    /// Suma mortalidad/selección/error desde la tabla unificada seguimiento_diario (tipo levante).
    /// Base de hembras desde lote_etapa_levante (historial) si existe; si no, desde lote.
    /// </summary>
    private async Task<int> CalcularHembrasVivasAsync(int loteId)
    {
        var loteIdStr = loteId.ToString();
        int baseH;
        int mortCajaH;
        var etapa = await _ctx.LoteEtapaLevante.AsNoTracking()
            .FirstOrDefaultAsync(el => el.LoteId == loteId);
        if (etapa != null)
        {
            baseH = etapa.AvesInicioHembras;
            var lote = await _ctx.Lotes.AsNoTracking()
                .Where(l => l.LoteId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
                .Select(l => new { MortCaja = l.MortCajaH ?? 0 })
                .SingleOrDefaultAsync();
            mortCajaH = lote?.MortCaja ?? 0;
        }
        else
        {
            var loteData = await _ctx.Lotes.AsNoTracking()
                .Where(l => l.LoteId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
                .Select(l => new { Base = l.HembrasL ?? 0, MortCaja = l.MortCajaH ?? 0 })
                .SingleAsync();
            baseH = loteData.Base;
            mortCajaH = loteData.MortCaja;
        }

        var sum = await _ctx.SeguimientoDiario.AsNoTracking()
            .Where(x => x.TipoSeguimiento == TipoLevante && x.LoteId == loteIdStr)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MortH = g.Sum(x => x.MortalidadHembras ?? 0),
                SelH = g.Sum(x => x.SelH ?? 0),
                ErrH = g.Sum(x => x.ErrorSexajeHembras ?? 0)
            })
            .SingleOrDefaultAsync();

        int mort = sum?.MortH ?? 0, sel = sum?.SelH ?? 0, err = sum?.ErrH ?? 0;
        var vivas = baseH - mortCajaH - mort - sel - err;
        return Math.Max(0, vivas);
    }

    private static (double? kcalAveH, double? protAveH) CalcularDerivados(double consumoKgHembras, double? kcalAlH, double? protAlH)
    {
        double? kcal = kcalAlH is null ? null : Math.Round(consumoKgHembras * kcalAlH.Value, 3);
        double? prot = protAlH is null ? null : Math.Round(consumoKgHembras * protAlH.Value, 3);
        return (kcal, prot);
    }

    private static int CalcularSemana(DateTime fechaEncaset, DateTime fechaRegistro)
    {
        var dias = (fechaRegistro.Date - fechaEncaset.Date).TotalDays;
        return Math.Max(1, (int)Math.Floor(dias / 7.0) + 1);
    }

    /// <summary>
    /// Descuenta aves de lote_postura_levante (aves_h_actual, aves_m_actual).
    /// Busca por lote_postura_levante_id o por lote_id.
    /// </summary>
    private async Task DescontarAvesEnLotePosturaLevanteAsync(int loteId, int? lotePosturaLevanteId, int hembras, int machos)
    {
        await AjustarAvesEnLotePosturaLevanteAsync(loteId, lotePosturaLevanteId, -hembras, -machos);
    }

    /// <summary>
    /// Ajusta aves en lote_postura_levante. deltaH/deltaM positivos = sumar, negativos = restar.
    /// </summary>
    private async Task AjustarAvesEnLotePosturaLevanteAsync(int loteId, int? lotePosturaLevanteId, int deltaH, int deltaM)
    {
        if (deltaH == 0 && deltaM == 0) return;

        var lev = lotePosturaLevanteId.HasValue
            ? await _ctx.LotePosturaLevante.FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId.Value && l.DeletedAt == null)
            : await _ctx.LotePosturaLevante.FirstOrDefaultAsync(l => l.LoteId == loteId && l.DeletedAt == null);
        if (lev == null) return;

        var avesH = (lev.AvesHActual ?? 0) + deltaH;
        var avesM = (lev.AvesMActual ?? 0) + deltaM;
        lev.AvesHActual = Math.Max(0, avesH);
        lev.AvesMActual = Math.Max(0, avesM);
        lev.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Resultado calculado: ejecuta SP y lee ProduccionResultadoLevante.
    /// NOTA: El SP sp_recalcular_seguimiento_levante debe leer de seguimiento_diario (tipo=levante)
    /// en lugar de seguimiento_lote_levante para que los datos coincidan.
    /// </summary>
    public async Task<ResultadoLevanteResponse> GetResultadoAsync(int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{loteId}' no existe o no pertenece a la compañía.");

        if (recalcular)
            await _ctx.Database.ExecuteSqlInterpolatedAsync($"select sp_recalcular_seguimiento_levante({loteId})");

        var q = from r in _ctx.ProduccionResultadoLevante.AsNoTracking()
                where r.LoteId == loteId
                select r;
        if (desde.HasValue) q = q.Where(x => x.Fecha >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(x => x.Fecha <= hasta.Value.Date);

        var items = await q.OrderBy(x => x.Fecha)
            .Select(r => new ResultadoLevanteItemDto(
                r.Fecha, r.EdadSemana,
                r.HembraViva, r.MortH, r.SelHOut, r.ErrH,
                r.ConsKgH, r.PesoH, r.UnifH, r.CvH,
                r.MortHPct, r.SelHPct, r.ErrHPct,
                r.MsEhH, r.AcMortH, r.AcSelH, r.AcErrH,
                r.AcConsKgH, r.ConsAcGrH, r.GrAveDiaH,
                r.DifConsHPct, r.DifPesoHPct, r.RetiroHPct, r.RetiroHAcPct,
                r.MachoVivo, r.MortM, r.SelMOut, r.ErrM,
                r.ConsKgM, r.PesoM, r.UnifM, r.CvM,
                r.MortMPct, r.SelMPct, r.ErrMPct,
                r.MsEmM, r.AcMortM, r.AcSelM, r.AcErrM,
                r.AcConsKgM, r.ConsAcGrM, r.GrAveDiaM,
                r.DifConsMPct, r.DifPesoMPct, r.RetiroMPct, r.RetiroMAcPct,
                r.RelMHPct,
                r.PesoHGuia, r.UnifHGuia, r.ConsAcGrHGuia, r.GrAveDiaHGuia, r.MortHPctGuia,
                r.PesoMGuia, r.UnifMGuia, r.ConsAcGrMGuia, r.GrAveDiaMGuia, r.MortMPctGuia,
                r.AlimentoHGuia, r.AlimentoMGuia
            ))
            .ToListAsync();

        return new ResultadoLevanteResponse(loteId, desde?.Date, hasta?.Date, items.Count, items);
    }
}

public interface IGramajeProviderV2
{
    Task<double?> GetGramajeGrPorAveAsync(string galponId, int semana, string tipoAlimento);
}
