// file: src/ZooSanMarino.Infrastructure/Services/LoteSeguimientoService.cs
// Seguimiento Diario Lote Reproductora: persiste en la tabla unificada seguimiento_diario (tipo = 'reproductora')
// usando ISeguimientoDiarioService. La API y DTOs del módulo (LoteSeguimientoController, LoteSeguimientoDto) se mantienen igual.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using AppInterfaces = ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LoteSeguimientoService : AppInterfaces.ILoteSeguimientoService
{
    private const string TipoReproductora = "reproductora";

    private readonly ZooSanMarinoContext _ctx;
    private readonly AppInterfaces.ICurrentUser _current;
    private readonly AppInterfaces.ISeguimientoDiarioService _seguimientoDiarioService;

    public LoteSeguimientoService(
        ZooSanMarinoContext ctx,
        AppInterfaces.ICurrentUser current,
        AppInterfaces.ISeguimientoDiarioService seguimientoDiarioService)
    {
        _ctx = ctx;
        _current = current;
        _seguimientoDiarioService = seguimientoDiarioService;
    }

    private static int ParseLoteId(string? loteId)
    {
        if (string.IsNullOrWhiteSpace(loteId)) return 0;
        return int.TryParse(loteId.Trim(), out var n) ? n : 0;
    }

    private static LoteSeguimientoDto MapFromUnificado(SeguimientoDiarioDto u)
    {
        return new LoteSeguimientoDto(
            (int)u.Id,
            u.Fecha,
            ParseLoteId(u.LoteId),
            u.ReproductoraId ?? "",
            u.PesoInicial,
            u.PesoFinal,
            u.MortalidadMachos,
            u.MortalidadHembras,
            u.SelM,
            u.SelH,
            u.ErrorSexajeMachos,
            u.ErrorSexajeHembras,
            u.TipoAlimento,
            u.ConsumoKgHembras,
            u.ConsumoKgMachos,
            u.Observaciones,
            u.Ciclo,
            u.PesoPromHembras,
            u.PesoPromMachos,
            u.UniformidadHembras,
            u.UniformidadMachos,
            u.CvHembras,
            u.CvMachos,
            u.ConsumoAguaDiario,
            u.ConsumoAguaPh,
            u.ConsumoAguaOrp,
            u.ConsumoAguaTemperatura,
            u.Metadata,
            u.ItemsAdicionales
        );
    }

    private static CreateSeguimientoDiarioDto MapToCreateUnificado(CreateLoteSeguimientoDto dto, string? createdByUserId)
    {
        var loteIdStr = dto.LoteId.ToString();
        return new CreateSeguimientoDiarioDto(
            TipoSeguimiento: TipoReproductora,
            LoteId: loteIdStr,
            LotePosturaLevanteId: null,
            LotePosturaProduccionId: null,
            ReproductoraId: dto.ReproductoraId?.Trim(),
            Fecha: dto.Fecha,
            MortalidadHembras: dto.MortalidadH,
            MortalidadMachos: dto.MortalidadM,
            SelH: dto.SelH,
            SelM: dto.SelM,
            ErrorSexajeHembras: dto.ErrorH,
            ErrorSexajeMachos: dto.ErrorM,
            ConsumoKgHembras: dto.ConsumoAlimento,
            ConsumoKgMachos: dto.ConsumoKgMachos,
            TipoAlimento: dto.TipoAlimento,
            Observaciones: dto.Observaciones,
            Ciclo: dto.Ciclo ?? "Normal",
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
            PesoInicial: dto.PesoInicial,
            PesoFinal: dto.PesoFinal,
            KcalAlH: null,
            ProtAlH: null,
            KcalAveH: null,
            ProtAveH: null,
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
            CreatedByUserId: createdByUserId
        );
    }

    private static UpdateSeguimientoDiarioDto MapToUpdateUnificado(UpdateLoteSeguimientoDto dto)
    {
        var loteIdStr = dto.LoteId.ToString();
        return new UpdateSeguimientoDiarioDto(
            Id: dto.Id,
            TipoSeguimiento: TipoReproductora,
            LoteId: loteIdStr,
            LotePosturaLevanteId: null,
            LotePosturaProduccionId: null,
            ReproductoraId: dto.ReproductoraId?.Trim(),
            Fecha: dto.Fecha,
            MortalidadHembras: dto.MortalidadH,
            MortalidadMachos: dto.MortalidadM,
            SelH: dto.SelH,
            SelM: dto.SelM,
            ErrorSexajeHembras: dto.ErrorH,
            ErrorSexajeMachos: dto.ErrorM,
            ConsumoKgHembras: dto.ConsumoAlimento,
            ConsumoKgMachos: dto.ConsumoKgMachos,
            TipoAlimento: dto.TipoAlimento,
            Observaciones: dto.Observaciones,
            Ciclo: dto.Ciclo ?? "Normal",
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
            PesoInicial: dto.PesoInicial,
            PesoFinal: dto.PesoFinal,
            KcalAlH: null,
            ProtAlH: null,
            KcalAveH: null,
            ProtAveH: null,
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

    public async Task<IEnumerable<LoteSeguimientoDto>> GetAllAsync()
    {
        var filter = new SeguimientoDiarioFilterRequest
        {
            TipoSeguimiento = TipoReproductora,
            LoteId = null,
            ReproductoraId = null,
            Page = 1,
            PageSize = 10_000,
            OrderBy = "Fecha",
            OrderAsc = false
        };
        var paged = await _seguimientoDiarioService.GetFilteredAsync(filter);
        return paged.Items.Select(MapFromUnificado).ToList();
    }

    public async Task<IEnumerable<LoteSeguimientoDto>> GetByLoteYReproAsync(string loteId, string reproductoraId, DateTime? desde = null, DateTime? hasta = null)
    {
        if (string.IsNullOrWhiteSpace(loteId) || string.IsNullOrWhiteSpace(reproductoraId))
            return Array.Empty<LoteSeguimientoDto>();

        var filter = new SeguimientoDiarioFilterRequest
        {
            TipoSeguimiento = TipoReproductora,
            LoteId = loteId.Trim(),
            ReproductoraId = reproductoraId.Trim(),
            FechaDesde = desde,
            FechaHasta = hasta,
            Page = 1,
            PageSize = 10_000,
            OrderBy = "Fecha",
            OrderAsc = true
        };
        var paged = await _seguimientoDiarioService.GetFilteredAsync(filter);
        return paged.Items.Select(MapFromUnificado).ToList();
    }

    public async Task<LoteSeguimientoDto?> GetByIdAsync(int id)
    {
        var u = await _seguimientoDiarioService.GetByIdAsync((long)id);
        if (u is null || (u.TipoSeguimiento?.Trim().ToLowerInvariant() != TipoReproductora))
            return null;
        return MapFromUnificado(u);
    }

    public async Task<LoteSeguimientoDto> CreateAsync(CreateLoteSeguimientoDto dto)
    {
        var loteIdStr = dto.LoteId.ToString();
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == loteIdStr &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        var existeReproductora = await (from lr in _ctx.LoteReproductoras.AsNoTracking()
                                        join l in _ctx.Lotes.AsNoTracking()
                                            on lr.LoteId equals l.LoteId.ToString()
                                        where lr.LoteId == loteIdStr &&
                                              lr.ReproductoraId == dto.ReproductoraId &&
                                              l.CompanyId == _current.CompanyId &&
                                              l.DeletedAt == null
                                        select 1).AnyAsync();
        if (!existeReproductora)
            throw new InvalidOperationException("La Reproductora indicada no existe para ese Lote.");

        var createdByUserId = _current.UserGuid?.ToString() ?? _current.UserId.ToString();
        var createDto = MapToCreateUnificado(dto, createdByUserId);
        var created = await _seguimientoDiarioService.CreateAsync(createDto);
        return MapFromUnificado(created);
    }

    public async Task<LoteSeguimientoDto?> UpdateAsync(UpdateLoteSeguimientoDto dto)
    {
        var loteIdStr = dto.LoteId.ToString();
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId.ToString() == loteIdStr &&
                                       l.CompanyId == _current.CompanyId &&
                                       l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        var existeReproductora = await (from lr in _ctx.LoteReproductoras.AsNoTracking()
                                        join l in _ctx.Lotes.AsNoTracking()
                                            on lr.LoteId equals l.LoteId.ToString()
                                        where lr.LoteId == loteIdStr &&
                                              lr.ReproductoraId == dto.ReproductoraId &&
                                              l.CompanyId == _current.CompanyId &&
                                              l.DeletedAt == null
                                        select 1).AnyAsync();
        if (!existeReproductora)
            throw new InvalidOperationException("La Reproductora indicada no existe para ese Lote.");

        var updateDto = MapToUpdateUnificado(dto);
        var updated = await _seguimientoDiarioService.UpdateAsync(updateDto);
        return updated is null ? null : MapFromUnificado(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        return await _seguimientoDiarioService.DeleteAsync((long)id);
    }
}
