using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoLoteLevanteService : ISeguimientoLoteLevanteService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly IAlimentoNutricionProvider _alimentos;
    private readonly IGramajeProvider _gramaje;
    private readonly ICurrentUser _current;
    private readonly IMovimientoAvesService _movimientoAvesService;

    public SeguimientoLoteLevanteService(
        ZooSanMarinoContext ctx,
        IAlimentoNutricionProvider alimentos,
        IGramajeProvider gramaje,
        ICurrentUser current,
        IMovimientoAvesService movimientoAvesService)
    {
        _ctx = ctx;
        _alimentos = alimentos;
        _gramaje = gramaje;
        _current = current;
        _movimientoAvesService = movimientoAvesService;
    }

    // Mapeo a DTO actualizado con los nuevos campos opcionales
    private static readonly Expression<Func<SeguimientoLoteLevante, SeguimientoLoteLevanteDto>> ToDto =
        x => new SeguimientoLoteLevanteDto(
            x.Id, x.LoteId, x.FechaRegistro,
            x.MortalidadHembras, x.MortalidadMachos,
            x.SelH, x.SelM,
            x.ErrorSexajeHembras, x.ErrorSexajeMachos,
            x.ConsumoKgHembras, x.TipoAlimento, x.Observaciones,
            x.KcalAlH, x.ProtAlH, x.KcalAveH, x.ProtAveH, x.Ciclo,

            // NUEVOS (mantenidos para compatibilidad)
            x.ConsumoKgMachos,
            x.PesoPromH, x.PesoPromM,
            x.UniformidadH, x.UniformidadM,
            x.CvH, x.CvM,
            // Metadata JSONB para campos adicionales
            x.Metadata
        );

    // ===========================
    // LISTAR POR LOTE (seguro por compañía)
    // ===========================
    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> GetByLoteAsync(int loteId)
    {
        var q = from s in _ctx.SeguimientoLoteLevante.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking()
                    on s.LoteId equals l.LoteId
                where l.CompanyId == _current.CompanyId
                   && l.DeletedAt == null
                   && s.LoteId == loteId
                select s;

        return await q
            .OrderBy(x => x.FechaRegistro)
            .Select(ToDto)
            .ToListAsync();
    }

    // ===========================
    // CREAR
    // ===========================
    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        // 0) Verificar lote y tenant
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId
                                     && l.CompanyId == _current.CompanyId
                                     && l.DeletedAt == null);
        if (lote is null) throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // 1) Duplicado por Lote+Fecha
        var duplicado = await (from s in _ctx.SeguimientoLoteLevante
                               where s.LoteId == dto.LoteId
                                  && s.FechaRegistro.Date == dto.FechaRegistro.Date
                               select s.Id).AnyAsync();
        if (duplicado) throw new InvalidOperationException("Ya existe un registro para ese Lote y Fecha.");

        // 2) Autocompletar nutrientes si no vienen
        double? kcalAlH = dto.KcalAlH, protAlH = dto.ProtAlH;
        if (kcalAlH is null || protAlH is null)
        {
            var np = await _alimentos.GetNutrientesAsync(dto.TipoAlimento);
            if (np.HasValue)
            {
                kcalAlH ??= np.Value.kcal;
                protAlH ??= np.Value.prot;
            }
        }

        // 3) Sugerir consumo por gramaje si <= 0 (hembras)
        double consumoKgH = dto.ConsumoKgHembras;
        if (consumoKgH <= 0 && !string.IsNullOrWhiteSpace(lote.GalponId) && lote.FechaEncaset.HasValue)
        {
            int semana = CalcularSemana(lote.FechaEncaset.Value, dto.FechaRegistro);

            double? gramajeGrAve = null;
            if (int.TryParse(lote.GalponId, out var galponIdInt))
                gramajeGrAve = await _gramaje.GetGramajeGrPorAveAsync(galponIdInt, semana, dto.TipoAlimento);
            else
            {
                if (_gramaje is IGramajeProviderV2 v2)
                    gramajeGrAve = await v2.GetGramajeGrPorAveAsync(lote.GalponId, semana, dto.TipoAlimento);
            }

            if (gramajeGrAve.HasValue && gramajeGrAve.Value > 0)
            {
                int hembrasVivas = await CalcularHembrasVivasAsync(dto.LoteId);
                consumoKgH = Math.Round((gramajeGrAve.Value * hembrasVivas) / 1000.0, 3); // gr → kg
            }
        }

        // 4) Derivados (por ahora solo hembras, igual que antes)
        var (kcalAveH, protAveH) = CalcularDerivados(consumoKgH, kcalAlH, protAlH);

        var ent = new SeguimientoLoteLevante
        {
            LoteId = dto.LoteId,
            FechaRegistro = dto.FechaRegistro,
            MortalidadHembras = dto.MortalidadHembras,
            MortalidadMachos = dto.MortalidadMachos,
            SelH = dto.SelH,
            SelM = dto.SelM,
            ErrorSexajeHembras = dto.ErrorSexajeHembras,
            ErrorSexajeMachos = dto.ErrorSexajeMachos,

            ConsumoKgHembras = consumoKgH,
            ConsumoKgMachos = dto.ConsumoKgMachos,
            
            // Metadata JSONB para campos adicionales/extras
            Metadata = dto.Metadata,

            PesoPromH = dto.PesoPromH,
            PesoPromM = dto.PesoPromM,
            UniformidadH = dto.UniformidadH,
            UniformidadM = dto.UniformidadM,
            CvH = dto.CvH,
            CvM = dto.CvM,

            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,

            KcalAlH = kcalAlH,
            ProtAlH = protAlH,
            KcalAveH = kcalAveH,
            ProtAveH = protAveH,

            Ciclo = dto.Ciclo
        };

        _ctx.SeguimientoLoteLevante.Add(ent);
        await _ctx.SaveChangesAsync();

        // Registrar retiro automático si hay mortalidades o selecciones
        var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos + dto.SelH + dto.SelM;
        if (totalRetiradas > 0)
        {
            try
            {
                await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                    loteId: dto.LoteId,
                    hembrasRetiradas: dto.MortalidadHembras + dto.SelH,
                    machosRetirados: dto.MortalidadMachos + dto.SelM,
                    mixtasRetiradas: 0, // Los seguimientos levante no tienen mixtas
                    fechaMovimiento: dto.FechaRegistro,
                    fuenteSeguimiento: "Levante",
                    observaciones: $"Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}, M: {dto.SelM} | Observaciones: {dto.Observaciones}"
                );
            }
            catch (Exception ex)
            {
                // Log error pero no fallar el guardado del seguimiento
                // TODO: Agregar logging aquí
                Console.WriteLine($"Error al registrar retiro desde seguimiento levante: {ex.Message}");
            }
        }

        return await _ctx.SeguimientoLoteLevante.AsNoTracking()
            .Where(x => x.Id == ent.Id)
            .Select(ToDto)
            .SingleAsync();
    }

    // ===========================
    // ACTUALIZAR
    // ===========================
    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var ent = await _ctx.SeguimientoLoteLevante.FindAsync(dto.Id);
        if (ent is null) return null;

        // Validar lote + tenant
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId
                                     && l.CompanyId == _current.CompanyId
                                     && l.DeletedAt == null);
        if (lote is null) throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // Duplicado al cambiar Lote/Fecha
        var cambiaClave = ent.LoteId != dto.LoteId || ent.FechaRegistro.Date != dto.FechaRegistro.Date;
        if (cambiaClave)
        {
            var existeOtro = await _ctx.SeguimientoLoteLevante.AnyAsync(x =>
                x.Id != dto.Id && x.LoteId == dto.LoteId && x.FechaRegistro.Date == dto.FechaRegistro.Date);
            if (existeOtro) throw new InvalidOperationException("Ya existe un registro para ese Lote y Fecha.");
        }

        // Nutrientes (si faltan)
        double? kcalAlH = dto.KcalAlH, protAlH = dto.ProtAlH;
        if (kcalAlH is null || protAlH is null)
        {
            var np = await _alimentos.GetNutrientesAsync(dto.TipoAlimento);
            if (np.HasValue)
            {
                kcalAlH ??= np.Value.kcal;
                protAlH ??= np.Value.prot;
            }
        }

        // Consumo sugerido si no viene (>0 respeta valor del usuario)
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

        ent.LoteId = dto.LoteId;
        ent.FechaRegistro = dto.FechaRegistro;
        ent.MortalidadHembras = dto.MortalidadHembras;
        ent.MortalidadMachos = dto.MortalidadMachos;
        ent.SelH = dto.SelH;
        ent.SelM = dto.SelM;
        ent.ErrorSexajeHembras = dto.ErrorSexajeHembras;
        ent.ErrorSexajeMachos = dto.ErrorSexajeMachos;

        ent.ConsumoKgHembras = consumoKgH;
        ent.ConsumoKgMachos = dto.ConsumoKgMachos;
        
        // Metadata JSONB para campos adicionales/extras
        ent.Metadata = dto.Metadata;

        ent.PesoPromH = dto.PesoPromH;
        ent.PesoPromM = dto.PesoPromM;
        ent.UniformidadH = dto.UniformidadH;
        ent.UniformidadM = dto.UniformidadM;
        ent.CvH = dto.CvH;
        ent.CvM = dto.CvM;

        ent.TipoAlimento = dto.TipoAlimento;
        ent.Observaciones = dto.Observaciones;

        ent.KcalAlH = kcalAlH;
        ent.ProtAlH = protAlH;
        ent.KcalAveH = kcalAveH;
        ent.ProtAveH = protAveH;

        ent.Ciclo = dto.Ciclo;

        await _ctx.SaveChangesAsync();

        // Registrar retiro automático si hay mortalidades o selecciones
        var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos + dto.SelH + dto.SelM;
        if (totalRetiradas > 0)
        {
            try
            {
                await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                    loteId: dto.LoteId,
                    hembrasRetiradas: dto.MortalidadHembras + dto.SelH,
                    machosRetirados: dto.MortalidadMachos + dto.SelM,
                    mixtasRetiradas: 0,
                    fechaMovimiento: dto.FechaRegistro,
                    fuenteSeguimiento: "Levante",
                    observaciones: $"Actualización - Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}, M: {dto.SelM}"
                );
            }
            catch (Exception ex)
            {
                // Log error pero no fallar la actualización
                Console.WriteLine($"Error al registrar retiro desde seguimiento levante (actualización): {ex.Message}");
            }
        }

        return new SeguimientoLoteLevanteDto(
            ent.Id, ent.LoteId, ent.FechaRegistro,
            ent.MortalidadHembras, ent.MortalidadMachos,
            ent.SelH, ent.SelM, ent.ErrorSexajeHembras, ent.ErrorSexajeMachos,
            ent.ConsumoKgHembras, ent.TipoAlimento, ent.Observaciones,
            ent.KcalAlH, ent.ProtAlH, ent.KcalAveH, ent.ProtAveH, ent.Ciclo,

            // NUEVOS (mantenidos para compatibilidad)
            ent.ConsumoKgMachos,
            ent.PesoPromH, ent.PesoPromM,
            ent.UniformidadH, ent.UniformidadM,
            ent.CvH, ent.CvM,
            // Metadata JSONB para campos adicionales
            ent.Metadata
        );
    }

    // ===========================
    // ELIMINAR
    // ===========================
    public async Task<bool> DeleteAsync(int id)
    {
        var ent = await _ctx.SeguimientoLoteLevante.FindAsync(id);
        if (ent is null) return false;

        // (Opcional) Validar que el lote pertenezca a la compañía actual
        var ok = await _ctx.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == ent.LoteId && l.CompanyId == _current.CompanyId);
        if (!ok) return false;

        _ctx.SeguimientoLoteLevante.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ===========================
    // FILTRO (seguro por compañía)
    // ===========================
    public async Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta)
    {
        var q = from s in _ctx.SeguimientoLoteLevante.AsNoTracking()
                join l in _ctx.Lotes.AsNoTracking()
                    on s.LoteId equals l.LoteId
                where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                select s;

        if (loteId.HasValue)
            q = q.Where(x => x.LoteId == loteId.Value);
        if (desde.HasValue)
            q = q.Where(x => x.FechaRegistro >= desde.Value);
        if (hasta.HasValue)
            q = q.Where(x => x.FechaRegistro <= hasta.Value);

        return await q
            .OrderBy(x => x.LoteId).ThenBy(x => x.FechaRegistro)
            .Select(ToDto)
            .ToListAsync();
    }

    // ===========================
    // Helpers
    // ===========================
    private static (double? kcalAveH, double? protAveH) CalcularDerivados(double consumoKgHembras, double? kcalAlH, double? protAlH)
    {
        double? kcal = (kcalAlH is null) ? null : Math.Round(consumoKgHembras * kcalAlH.Value, 3);
        double? prot = (protAlH is null) ? null : Math.Round(consumoKgHembras * protAlH.Value, 3);
        return (kcal, prot);
    }

    private static int CalcularSemana(DateTime fechaEncaset, DateTime fechaRegistro)
    {
        var dias = (fechaRegistro.Date - fechaEncaset.Date).TotalDays;
        return Math.Max(1, (int)Math.Floor(dias / 7.0) + 1);
    }

    private async Task<int> CalcularHembrasVivasAsync(int loteId)
    {
        var baseH = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
            .Select(l => new { Base = l.HembrasL ?? 0, MortCaja = l.MortCajaH ?? 0 })
            .SingleAsync();

        var sum = await _ctx.SeguimientoLoteLevante.AsNoTracking()
            .Where(x => x.LoteId == loteId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MortH = g.Sum(x => x.MortalidadHembras),
                SelH = g.Sum(x => x.SelH),
                ErrH = g.Sum(x => x.ErrorSexajeHembras)
            })
            .SingleOrDefaultAsync();

        int mort = sum?.MortH ?? 0;
        int sel = sum?.SelH ?? 0;
        int err = sum?.ErrH ?? 0;

        var vivas = baseH.Base - baseH.MortCaja - mort - sel - err;
        return Math.Max(0, vivas);
    }
    
      public async Task<ResultadoLevanteResponse> GetResultadoAsync(
        int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true)
    {
        // 0) Validar lote/tenant
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == loteId
                                     && l.CompanyId == _current.CompanyId
                                     && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{loteId}' no existe o no pertenece a la compañía.");

        // 1) Recalcular on-demand (opcional)
        if (recalcular)
        {
            // IMPORTANTE: usa parámetro para evitar SQL injection
            await _ctx.Database.ExecuteSqlInterpolatedAsync(
                $"select sp_recalcular_seguimiento_levante({loteId})");
        }

        // 2) Query del snapshot
        var q = from r in _ctx.ProduccionResultadoLevante.AsNoTracking()
                where r.LoteId == loteId
                select r;

        if (desde.HasValue) q = q.Where(x => x.Fecha >= desde.Value.Date);
        if (hasta.HasValue) q = q.Where(x => x.Fecha <= hasta.Value.Date);

        var items = await q.OrderBy(x => x.Fecha)
            .Select(r => new ResultadoLevanteItemDto(
                r.Fecha, r.EdadSemana,
                // H
                r.HembraViva, r.MortH, r.SelHOut, r.ErrH,
                r.ConsKgH, r.PesoH, r.UnifH, r.CvH,
                r.MortHPct, r.SelHPct, r.ErrHPct,
                r.MsEhH, r.AcMortH, r.AcSelH, r.AcErrH,
                r.AcConsKgH, r.ConsAcGrH, r.GrAveDiaH,
                r.DifConsHPct, r.DifPesoHPct, r.RetiroHPct, r.RetiroHAcPct,
                // M
                r.MachoVivo, r.MortM, r.SelMOut, r.ErrM,
                r.ConsKgM, r.PesoM, r.UnifM, r.CvM,
                r.MortMPct, r.SelMPct, r.ErrMPct,
                r.MsEmM, r.AcMortM, r.AcSelM, r.AcErrM,
                r.AcConsKgM, r.ConsAcGrM, r.GrAveDiaM,
                r.DifConsMPct, r.DifPesoMPct, r.RetiroMPct, r.RetiroMAcPct,
                // Rel/Guías
                r.RelMHPct,
                r.PesoHGuia, r.UnifHGuia, r.ConsAcGrHGuia, r.GrAveDiaHGuia, r.MortHPctGuia,
                r.PesoMGuia, r.UnifMGuia, r.ConsAcGrMGuia, r.GrAveDiaMGuia, r.MortMPctGuia,
                r.AlimentoHGuia, r.AlimentoMGuia
            ))
            .ToListAsync();

        return new ResultadoLevanteResponse(
            loteId, desde?.Date, hasta?.Date, items.Count, items);
    }

}

// (Opcional): Si tienes provider que acepta string GalponId
public interface IGramajeProviderV2
{
    Task<double?> GetGramajeGrPorAveAsync(string galponId, int semana, string tipoAlimento);
}
