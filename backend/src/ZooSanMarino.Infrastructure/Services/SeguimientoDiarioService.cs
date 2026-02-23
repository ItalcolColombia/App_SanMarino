// src/ZooSanMarino.Infrastructure/Services/SeguimientoDiarioService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using PagedResultSeguimiento = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.SeguimientoDiarioDto>;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoDiarioService : ISeguimientoDiarioService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;

    private static readonly string[] ValidTipos = { "levante", "produccion", "reproductora" };

    public SeguimientoDiarioService(ZooSanMarinoContext ctx, ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    /// <summary>
    /// Query base: solo registros cuyo lote pertenece a la compañía, o cuyo lote_postura_produccion pertenece a la compañía.
    /// </summary>
    private IQueryable<SeguimientoDiario> BaseQuery()
    {
        var companyId = _current.CompanyId;
        var validLoteIds = _ctx.Lotes.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteId != null)
            .Select(l => l.LoteId!.Value.ToString());
        var validLppIds = _ctx.LotePosturaProduccion.AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LotePosturaProduccionId != null)
            .Select(l => l.LotePosturaProduccionId!.Value);
        return _ctx.SeguimientoDiario.AsNoTracking()
            .Where(s => validLoteIds.Contains(s.LoteId)
                || (s.LotePosturaProduccionId != null && validLppIds.Contains(s.LotePosturaProduccionId.Value)));
    }

    private static SeguimientoDiarioDto ToDto(SeguimientoDiario x)
    {
        return new SeguimientoDiarioDto(
            x.Id, x.TipoSeguimiento, x.LoteId, x.LotePosturaLevanteId, x.LotePosturaProduccionId, x.ReproductoraId, x.Fecha,
            x.MortalidadHembras, x.MortalidadMachos, x.SelH, x.SelM,
            x.ErrorSexajeHembras, x.ErrorSexajeMachos, x.ConsumoKgHembras, x.ConsumoKgMachos,
            x.TipoAlimento, x.Observaciones, x.Ciclo,
            x.PesoPromHembras, x.PesoPromMachos, x.UniformidadHembras, x.UniformidadMachos,
            x.CvHembras, x.CvMachos,
            x.ConsumoAguaDiario, x.ConsumoAguaPh, x.ConsumoAguaOrp, x.ConsumoAguaTemperatura,
            x.Metadata, x.ItemsAdicionales,
            x.PesoInicial, x.PesoFinal,
            x.KcalAlH, x.ProtAlH, x.KcalAveH, x.ProtAveH,
            x.HuevoTot, x.HuevoInc, x.HuevoLimpio, x.HuevoTratado, x.HuevoSucio, x.HuevoDeforme,
            x.HuevoBlanco, x.HuevoDobleYema, x.HuevoPiso, x.HuevoPequeno, x.HuevoRoto, x.HuevoDesecho, x.HuevoOtro,
            x.PesoHuevo, x.Etapa, x.PesoH, x.PesoM, x.Uniformidad, x.CoeficienteVariacion, x.ObservacionesPesaje,
            x.CreatedByUserId, x.CreatedAt, x.UpdatedAt
        );
    }

    public async Task<SeguimientoDiarioDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var ent = await BaseQuery().Where(s => s.Id == id).SingleOrDefaultAsync(ct);
        return ent is null ? null : ToDto(ent);
    }

    public async Task<PagedResultSeguimiento> GetFilteredAsync(SeguimientoDiarioFilterRequest filter, CancellationToken ct = default)
    {
        var q = BaseQuery();

        if (!string.IsNullOrWhiteSpace(filter.TipoSeguimiento))
        {
            var tipo = filter.TipoSeguimiento.Trim().ToLowerInvariant();
            if (ValidTipos.Contains(tipo))
                q = q.Where(s => s.TipoSeguimiento == tipo);
        }

        if (!string.IsNullOrWhiteSpace(filter.LoteId))
            q = q.Where(s => s.LoteId == filter.LoteId.Trim());

        if (filter.LotePosturaProduccionId.HasValue)
            q = q.Where(s => s.LotePosturaProduccionId == filter.LotePosturaProduccionId.Value);

        if (!string.IsNullOrWhiteSpace(filter.ReproductoraId))
            q = q.Where(s => s.ReproductoraId == filter.ReproductoraId.Trim());

        if (filter.FechaDesde.HasValue)
            q = q.Where(s => s.Fecha >= filter.FechaDesde.Value);

        if (filter.FechaHasta.HasValue)
        {
            var hasta = filter.FechaHasta.Value.Date.AddDays(1);
            q = q.Where(s => s.Fecha < hasta);
        }

        var total = await q.LongCountAsync(ct);

        var orderBy = (filter.OrderBy ?? "Fecha").Trim();
        var asc = filter.OrderAsc;
        q = orderBy.ToUpperInvariant() switch
        {
            "LOTEID" => asc ? q.OrderBy(s => s.LoteId).ThenBy(s => s.Fecha) : q.OrderByDescending(s => s.LoteId).ThenByDescending(s => s.Fecha),
            "TIPO" => asc ? q.OrderBy(s => s.TipoSeguimiento).ThenBy(s => s.Fecha) : q.OrderByDescending(s => s.TipoSeguimiento).ThenByDescending(s => s.Fecha),
            _ => asc ? q.OrderBy(s => s.Fecha) : q.OrderByDescending(s => s.Fecha)
        };

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResultSeguimiento
        {
            Page = page,
            PageSize = pageSize,
            Total = total,
            Items = items.Select(ToDto).ToList()
        };
    }

    public async Task<SeguimientoDiarioDto> CreateAsync(CreateSeguimientoDiarioDto dto, CancellationToken ct = default)
    {
        var tipo = (dto.TipoSeguimiento ?? "").Trim().ToLowerInvariant();
        if (!ValidTipos.Contains(tipo))
            throw new InvalidOperationException($"Tipo de seguimiento inválido. Debe ser: {string.Join(", ", ValidTipos)}.");

        var loteId = (dto.LoteId ?? "").Trim();
        if (string.IsNullOrEmpty(loteId))
            throw new InvalidOperationException("LoteId es requerido.");

        var useLppFlow = tipo == "produccion" && dto.LotePosturaProduccionId.HasValue;

        // Validar que el lote pertenezca a la compañía (salvo flujo LPP, donde validamos LPP)
        Lote? lote = null;
        if (!useLppFlow)
        {
            lote = await _ctx.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId != null && l.LoteId.ToString() == loteId
                    && l.CompanyId == _current.CompanyId && l.DeletedAt == null, ct);
            if (lote is null)
                throw new InvalidOperationException($"El lote '{loteId}' no existe o no pertenece a la compañía.");
        }
        else
        {
            var lpp = await _ctx.LotePosturaProduccion.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == dto.LotePosturaProduccionId.Value
                    && l.CompanyId == _current.CompanyId && l.DeletedAt == null, ct);
            if (lpp is null)
                throw new InvalidOperationException("El lote postura producción no existe o no pertenece a la compañía.");
        }

        // Flujo historial: Levante debe tener registro en lote_etapa_levante (inicio de etapa)
        if (tipo == "levante")
        {
            var loteIdInt = int.TryParse(loteId, out var id) ? id : (int?)null;
            if (loteIdInt.HasValue)
            {
                var existeEtapa = await _ctx.LoteEtapaLevante.AsNoTracking()
                    .AnyAsync(el => el.LoteId == loteIdInt.Value, ct);
                if (!existeEtapa)
                {
                    _ctx.LoteEtapaLevante.Add(new LoteEtapaLevante
                    {
                        LoteId = loteIdInt.Value,
                        AvesInicioHembras = lote.HembrasL ?? 0,
                        AvesInicioMachos = lote.MachosL ?? 0,
                        FechaInicio = lote.FechaEncaset ?? DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                    await _ctx.SaveChangesAsync(ct);
                }
            }
        }

        // Flujo historial: Producción exige que el lote esté en fase Produccion (lote hijo), salvo flujo LPP
        if (tipo == "produccion" && !useLppFlow && lote != null)
        {
            if (lote.Fase != "Produccion")
                throw new InvalidOperationException(
                    "El lote indicado no está en fase Producción. Debe crear primero el lote de producción (desde el lote en Levante) antes de registrar seguimientos diarios.");
        }

        if (tipo == "reproductora")
        {
            var repId = (dto.ReproductoraId ?? "").Trim();
            if (string.IsNullOrEmpty(repId))
                throw new InvalidOperationException("ReproductoraId es requerido cuando el tipo es 'reproductora'.");
            var existeRep = await _ctx.LoteReproductoras.AsNoTracking()
                .AnyAsync(lr => lr.LoteId == loteId && lr.ReproductoraId == repId, ct);
            if (!existeRep)
                throw new InvalidOperationException("La reproductora indicada no existe para ese lote.");
        }

        var fechaNorm = dto.Fecha.Kind == DateTimeKind.Utc ? dto.Fecha : DateTime.SpecifyKind(dto.Fecha.Date, DateTimeKind.Utc);
        var repForUnique = tipo == "reproductora" ? (dto.ReproductoraId ?? "").Trim() : "";
        var duplicado = useLppFlow
            ? await _ctx.SeguimientoDiario.AsNoTracking()
                .AnyAsync(s => s.TipoSeguimiento == tipo && s.LotePosturaProduccionId == dto.LotePosturaProduccionId
                    && s.Fecha.Date == fechaNorm.Date, ct)
            : await _ctx.SeguimientoDiario.AsNoTracking()
                .AnyAsync(s => s.TipoSeguimiento == tipo && s.LoteId == loteId
                    && (s.ReproductoraId ?? "") == repForUnique
                    && s.Fecha.Date == fechaNorm.Date, ct);
        if (duplicado)
            throw new InvalidOperationException("Ya existe un seguimiento para ese tipo, lote, reproductora (si aplica) y fecha.");

        var createdBy = dto.CreatedByUserId ?? _current.UserGuid?.ToString() ?? _current.UserId.ToString();

        var ent = new SeguimientoDiario
        {
            TipoSeguimiento = tipo,
            LoteId = loteId,
            LotePosturaLevanteId = dto.LotePosturaLevanteId,
            LotePosturaProduccionId = dto.LotePosturaProduccionId,
            ReproductoraId = tipo == "reproductora" ? (dto.ReproductoraId ?? "").Trim() : null,
            Fecha = fechaNorm,
            MortalidadHembras = dto.MortalidadHembras,
            MortalidadMachos = dto.MortalidadMachos,
            SelH = dto.SelH,
            SelM = dto.SelM,
            ErrorSexajeHembras = dto.ErrorSexajeHembras,
            ErrorSexajeMachos = dto.ErrorSexajeMachos,
            ConsumoKgHembras = dto.ConsumoKgHembras,
            ConsumoKgMachos = dto.ConsumoKgMachos,
            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,
            Ciclo = dto.Ciclo ?? "Normal",
            PesoPromHembras = dto.PesoPromHembras,
            PesoPromMachos = dto.PesoPromMachos,
            UniformidadHembras = dto.UniformidadHembras,
            UniformidadMachos = dto.UniformidadMachos,
            CvHembras = dto.CvHembras,
            CvMachos = dto.CvMachos,
            ConsumoAguaDiario = dto.ConsumoAguaDiario,
            ConsumoAguaPh = dto.ConsumoAguaPh,
            ConsumoAguaOrp = dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura,
            Metadata = dto.Metadata,
            ItemsAdicionales = dto.ItemsAdicionales,
            PesoInicial = dto.PesoInicial,
            PesoFinal = dto.PesoFinal,
            KcalAlH = dto.KcalAlH,
            ProtAlH = dto.ProtAlH,
            KcalAveH = dto.KcalAveH,
            ProtAveH = dto.ProtAveH,
            HuevoTot = dto.HuevoTot,
            HuevoInc = dto.HuevoInc,
            HuevoLimpio = dto.HuevoLimpio,
            HuevoTratado = dto.HuevoTratado,
            HuevoSucio = dto.HuevoSucio,
            HuevoDeforme = dto.HuevoDeforme,
            HuevoBlanco = dto.HuevoBlanco,
            HuevoDobleYema = dto.HuevoDobleYema,
            HuevoPiso = dto.HuevoPiso,
            HuevoPequeno = dto.HuevoPequeno,
            HuevoRoto = dto.HuevoRoto,
            HuevoDesecho = dto.HuevoDesecho,
            HuevoOtro = dto.HuevoOtro,
            PesoHuevo = dto.PesoHuevo,
            Etapa = dto.Etapa,
            PesoH = dto.PesoH,
            PesoM = dto.PesoM,
            Uniformidad = dto.Uniformidad,
            CoeficienteVariacion = dto.CoeficienteVariacion,
            ObservacionesPesaje = dto.ObservacionesPesaje,
            CreatedByUserId = createdBy,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.SeguimientoDiario.Add(ent);
        await _ctx.SaveChangesAsync(ct);

        if (useLppFlow && dto.LotePosturaProduccionId.HasValue)
        {
            await AplicarDescuentoLppAsync(dto.LotePosturaProduccionId.Value,
                dto.MortalidadHembras ?? 0, dto.MortalidadMachos ?? 0,
                dto.SelH ?? 0, dto.SelM ?? 0,
                dto.ErrorSexajeHembras ?? 0, dto.ErrorSexajeMachos ?? 0,
                resta: true, ct);
        }

        var created = await _ctx.SeguimientoDiario.AsNoTracking().FirstAsync(s => s.Id == ent.Id, ct);
        return ToDto(created);
    }

    /// <summary>
    /// Aplica o revierte descuento de aves en lote_postura_produccion (mortalidad, selección, error sexaje).
    /// resta=true: resta de AvesHActual/AvesMActual; resta=false: suma (revierte).
    /// Si aves_h_actual/aves_m_actual son null, se inicializan desde aves_h_inicial/aves_m_inicial o hembras_iniciales_prod/machos_iniciales_prod.
    /// </summary>
    private async Task AplicarDescuentoLppAsync(int lppId, int mortH, int mortM, int selH, int selM, int errH, int errM, bool resta, CancellationToken ct)
    {
        var deltaH = mortH + selH + errH;
        var deltaM = mortM + selM + errM;
        if (deltaH == 0 && deltaM == 0) return;

        var lpp = await _ctx.LotePosturaProduccion.FindAsync(new object[] { lppId }, ct);
        if (lpp == null) return;

        // Inicializar aves actuales si son null (usar iniciales)
        var avesH = lpp.AvesHActual ?? lpp.HembrasInicialesProd ?? lpp.AvesHInicial ?? 0;
        var avesM = lpp.AvesMActual ?? lpp.MachosInicialesProd ?? lpp.AvesMInicial ?? 0;

        var sign = resta ? -1 : 1;
        lpp.AvesHActual = Math.Max(0, avesH + sign * deltaH);
        lpp.AvesMActual = Math.Max(0, avesM + sign * deltaM);
        lpp.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<SeguimientoDiarioDto?> UpdateAsync(UpdateSeguimientoDiarioDto dto, CancellationToken ct = default)
    {
        var ent = await _ctx.SeguimientoDiario
            .FirstOrDefaultAsync(s => s.Id == dto.Id, ct);
        if (ent is null)
            return null;

        // Verificar que el registro pertenezca a la compañía (vía lote)
        var belongs = await BaseQuery().AnyAsync(s => s.Id == dto.Id, ct);
        if (!belongs)
            return null;

        var tipo = (dto.TipoSeguimiento ?? "").Trim().ToLowerInvariant();
        if (!ValidTipos.Contains(tipo))
            throw new InvalidOperationException($"Tipo de seguimiento inválido. Debe ser: {string.Join(", ", ValidTipos)}.");

        var loteId = (dto.LoteId ?? "").Trim();
        if (string.IsNullOrEmpty(loteId))
            throw new InvalidOperationException("LoteId es requerido.");

        var useLppFlow = tipo == "produccion" && dto.LotePosturaProduccionId.HasValue;
        Lote? lote = null;
        if (!useLppFlow)
        {
            lote = await _ctx.Lotes.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId != null && l.LoteId.ToString() == loteId
                    && l.CompanyId == _current.CompanyId && l.DeletedAt == null, ct);
            if (lote is null)
                throw new InvalidOperationException($"El lote '{loteId}' no existe o no pertenece a la compañía.");
        }
        else
        {
            var lpp = await _ctx.LotePosturaProduccion.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == dto.LotePosturaProduccionId.Value
                    && l.CompanyId == _current.CompanyId && l.DeletedAt == null, ct);
            if (lpp is null)
                throw new InvalidOperationException("El lote postura producción no existe o no pertenece a la compañía.");
        }

        if (tipo == "reproductora")
        {
            var repId = (dto.ReproductoraId ?? "").Trim();
            if (string.IsNullOrEmpty(repId))
                throw new InvalidOperationException("ReproductoraId es requerido cuando el tipo es 'reproductora'.");
            var existeRep = await _ctx.LoteReproductoras.AsNoTracking()
                .AnyAsync(lr => lr.LoteId == loteId && lr.ReproductoraId == repId, ct);
            if (!existeRep)
                throw new InvalidOperationException("La reproductora indicada no existe para ese lote.");
        }

        var fechaNorm = dto.Fecha.Kind == DateTimeKind.Utc ? dto.Fecha : DateTime.SpecifyKind(dto.Fecha.Date, DateTimeKind.Utc);
        var repForUnique = tipo == "reproductora" ? (dto.ReproductoraId ?? "").Trim() : "";
        var duplicado = useLppFlow
            ? await _ctx.SeguimientoDiario.AsNoTracking()
                .AnyAsync(s => s.Id != dto.Id && s.TipoSeguimiento == tipo && s.LotePosturaProduccionId == dto.LotePosturaProduccionId
                    && s.Fecha.Date == fechaNorm.Date, ct)
            : await _ctx.SeguimientoDiario.AsNoTracking()
                .AnyAsync(s => s.Id != dto.Id && s.TipoSeguimiento == tipo && s.LoteId == loteId
                    && (s.ReproductoraId ?? "") == repForUnique
                    && s.Fecha.Date == fechaNorm.Date, ct);
        if (duplicado)
            throw new InvalidOperationException("Ya existe otro seguimiento para ese tipo, lote, reproductora (si aplica) y fecha.");

        // Capturar valores OLD antes de sobrescribir (para descuento LPP)
        var oldMortH = ent.MortalidadHembras ?? 0;
        var oldMortM = ent.MortalidadMachos ?? 0;
        var oldSelH = ent.SelH ?? 0;
        var oldSelM = ent.SelM ?? 0;
        var oldErrH = ent.ErrorSexajeHembras ?? 0;
        var oldErrM = ent.ErrorSexajeMachos ?? 0;

        ent.TipoSeguimiento = tipo;
        ent.LoteId = loteId;
        ent.LotePosturaLevanteId = dto.LotePosturaLevanteId;
        ent.LotePosturaProduccionId = dto.LotePosturaProduccionId;
        ent.ReproductoraId = tipo == "reproductora" ? (dto.ReproductoraId ?? "").Trim() : null;
        ent.Fecha = fechaNorm;
        ent.MortalidadHembras = dto.MortalidadHembras;
        ent.MortalidadMachos = dto.MortalidadMachos;
        ent.SelH = dto.SelH;
        ent.SelM = dto.SelM;
        ent.ErrorSexajeHembras = dto.ErrorSexajeHembras;
        ent.ErrorSexajeMachos = dto.ErrorSexajeMachos;
        ent.ConsumoKgHembras = dto.ConsumoKgHembras;
        ent.ConsumoKgMachos = dto.ConsumoKgMachos;
        ent.TipoAlimento = dto.TipoAlimento;
        ent.Observaciones = dto.Observaciones;
        ent.Ciclo = dto.Ciclo ?? "Normal";
        ent.PesoPromHembras = dto.PesoPromHembras;
        ent.PesoPromMachos = dto.PesoPromMachos;
        ent.UniformidadHembras = dto.UniformidadHembras;
        ent.UniformidadMachos = dto.UniformidadMachos;
        ent.CvHembras = dto.CvHembras;
        ent.CvMachos = dto.CvMachos;
        ent.ConsumoAguaDiario = dto.ConsumoAguaDiario;
        ent.ConsumoAguaPh = dto.ConsumoAguaPh;
        ent.ConsumoAguaOrp = dto.ConsumoAguaOrp;
        ent.ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura;
        ent.Metadata = dto.Metadata;
        ent.ItemsAdicionales = dto.ItemsAdicionales;
        ent.PesoInicial = dto.PesoInicial;
        ent.PesoFinal = dto.PesoFinal;
        ent.KcalAlH = dto.KcalAlH;
        ent.ProtAlH = dto.ProtAlH;
        ent.KcalAveH = dto.KcalAveH;
        ent.ProtAveH = dto.ProtAveH;
        ent.HuevoTot = dto.HuevoTot;
        ent.HuevoInc = dto.HuevoInc;
        ent.HuevoLimpio = dto.HuevoLimpio;
        ent.HuevoTratado = dto.HuevoTratado;
        ent.HuevoSucio = dto.HuevoSucio;
        ent.HuevoDeforme = dto.HuevoDeforme;
        ent.HuevoBlanco = dto.HuevoBlanco;
        ent.HuevoDobleYema = dto.HuevoDobleYema;
        ent.HuevoPiso = dto.HuevoPiso;
        ent.HuevoPequeno = dto.HuevoPequeno;
        ent.HuevoRoto = dto.HuevoRoto;
        ent.HuevoDesecho = dto.HuevoDesecho;
        ent.HuevoOtro = dto.HuevoOtro;
        ent.PesoHuevo = dto.PesoHuevo;
        ent.Etapa = dto.Etapa;
        ent.PesoH = dto.PesoH;
        ent.PesoM = dto.PesoM;
        ent.Uniformidad = dto.Uniformidad;
        ent.CoeficienteVariacion = dto.CoeficienteVariacion;
        ent.ObservacionesPesaje = dto.ObservacionesPesaje;
        ent.UpdatedAt = DateTime.UtcNow;

        if (useLppFlow && ent.LotePosturaProduccionId.HasValue)
        {
            var newMortH = dto.MortalidadHembras ?? 0;
            var newMortM = dto.MortalidadMachos ?? 0;
            var newSelH = dto.SelH ?? 0;
            var newSelM = dto.SelM ?? 0;
            var newErrH = dto.ErrorSexajeHembras ?? 0;
            var newErrM = dto.ErrorSexajeMachos ?? 0;
            await AplicarDescuentoLppAsync(ent.LotePosturaProduccionId.Value, oldMortH, oldMortM, oldSelH, oldSelM, oldErrH, oldErrM, resta: false, ct);
            await AplicarDescuentoLppAsync(ent.LotePosturaProduccionId.Value, newMortH, newMortM, newSelH, newSelM, newErrH, newErrM, resta: true, ct);
        }

        await _ctx.SaveChangesAsync(ct);

        var updated = await _ctx.SeguimientoDiario.AsNoTracking().FirstAsync(s => s.Id == ent.Id, ct);
        return ToDto(updated);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var ent = await _ctx.SeguimientoDiario.FindAsync(new object[] { id }, ct);
        if (ent is null)
            return false;

        var belongs = await BaseQuery().AnyAsync(s => s.Id == id, ct);
        if (!belongs)
            return false;

        if (ent.TipoSeguimiento == "produccion" && ent.LotePosturaProduccionId.HasValue)
        {
            await AplicarDescuentoLppAsync(ent.LotePosturaProduccionId.Value,
                ent.MortalidadHembras ?? 0, ent.MortalidadMachos ?? 0,
                ent.SelH ?? 0, ent.SelM ?? 0,
                ent.ErrorSexajeHembras ?? 0, ent.ErrorSexajeMachos ?? 0,
                resta: false, ct);
        }

        _ctx.SeguimientoDiario.Remove(ent);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
