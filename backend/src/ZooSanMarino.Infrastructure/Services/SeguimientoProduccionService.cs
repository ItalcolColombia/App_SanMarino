using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class SeguimientoProduccionService : ISeguimientoProduccionService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser? _current;

    public SeguimientoProduccionService(ZooSanMarinoContext ctx, ICurrentUser? current = null)
    {
        _ctx = ctx;
        _current = current;
    }

    public async Task<IEnumerable<SeguimientoProduccionDto>> GetAllAsync()
    {
        return await _ctx.SeguimientoProduccion
            .Select(x => new SeguimientoProduccionDto(
                x.Id,
                x.Fecha,
                x.LoteId.ToString(),
                x.MortalidadH,
                x.MortalidadM,
                x.SelH,
                x.SelM,
                x.ConsKgH,
                x.ConsKgM,
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones ?? "",
                x.PesoHuevo,
                x.Etapa,
                x.Metadata
            ))
            .ToListAsync();
    }

    public async Task<SeguimientoProduccionDto?> GetByLoteIdAsync(int loteId)
    {
        var entity = await _ctx.SeguimientoProduccion
            .Where(x => x.LoteId == loteId)
            .OrderByDescending(x => x.Fecha)
            .FirstOrDefaultAsync();

        if (entity == null) return null;

        return new SeguimientoProduccionDto(
            entity.Id,
            entity.Fecha,
            entity.LoteId.ToString(),
            entity.MortalidadH,
            entity.MortalidadM,
            entity.SelH,
            entity.SelM,
            entity.ConsKgH,
            entity.ConsKgM,
            entity.HuevoTot,
            entity.HuevoInc,
            entity.TipoAlimento,
            entity.Observaciones ?? "",
            entity.PesoHuevo,
            entity.Etapa,
            entity.Metadata
        );
    }

    public async Task<SeguimientoProduccionDto> CreateAsync(CreateSeguimientoProduccionDto dto)
    {
        var loteProd = await _ctx.Lotes.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == dto.LoteId && l.Fase == "Produccion" && l.DeletedAt == null);
        if (loteProd == null)
            throw new InvalidOperationException(
                "No existe lote en fase Producción con ese ID. Cree primero el lote de producción desde el lote en Levante.");

        var fechaDate = dto.Fecha.Date;
        var ct = CancellationToken.None;
        int currentUserId = _current?.UserId ?? 0;

        // ── Feature 14: MERGE con fila existente sólo-traslado (canónica) ──
        var existente = await _ctx.SeguimientoProduccion
            .FirstOrDefaultAsync(s => s.LoteId == dto.LoteId && s.Fecha == fechaDate, ct);

        if (existente != null)
        {
            bool teneTraslado = existente.TrasladoIngresoHembras > 0 || existente.TrasladoIngresoMachos > 0
                              || existente.TrasladoSalidaHembras  > 0 || existente.TrasladoSalidaMachos  > 0;
            bool teneManual = existente.MortalidadH > 0 || existente.MortalidadM > 0
                           || existente.SelH > 0 || existente.SelM > 0
                           || existente.ErrorSexajeHembras > 0 || existente.ErrorSexajeMachos > 0
                           || existente.ConsKgH > 0 || existente.ConsKgM > 0
                           || existente.HuevoTot > 0;

            if (teneTraslado && !teneManual)
            {
                // MERGE — añadir campos manuales sin tocar columnas traslado_*
                return await MergearManualSobreTrasladoProdAsync(existente, dto, currentUserId, ct);
            }
            if (teneManual)
            {
                throw new InvalidOperationException(
                    "Ya existe un seguimiento manual para ese lote en esa fecha.");
            }
            // Fila vacía — borramos y dejamos crear nueva
            _ctx.SeguimientoProduccion.Remove(existente);
            await _ctx.SaveChangesAsync(ct);
        }

        var entity = new SeguimientoProduccion
        {
            LoteId = dto.LoteId,
            Fecha = fechaDate,
            MortalidadH = dto.MortalidadH,
            MortalidadM = dto.MortalidadM,
            ConsKgH = dto.ConsKgH + dto.ConsKgM,   // total en ConsKgH (ConsKgM=0), como la deprecada guardaba en consumo_kg
            ConsKgM = 0m,
            HuevoTot = dto.HuevoTot,
            HuevoInc = dto.HuevoInc,
            PesoHuevo = dto.PesoHuevo,
            Observaciones = dto.Observaciones,
            TipoAlimento = "",                     // NOT NULL en la canónica (la deprecada no tenía la columna)
            CompanyId = _current?.CompanyId ?? 0,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.SeguimientoProduccion.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        // Descuento de aves en LPP (mortalidad + sel + err) ─ Feature 14
        await AplicarDescuentoLppAsync(dto.LoteId,
            dto.MortalidadH, dto.MortalidadM,
            dto.SelH, dto.SelM,
            0, 0,
            resta: true, ct);

        return MapToDto(entity);
    }

    public async Task<SeguimientoProduccionDto?> UpdateAsync(UpdateSeguimientoProduccionDto dto)
    {
        var entity = await _ctx.SeguimientoProduccion.FindAsync(dto.Id);
        if (entity == null) return null;

        // Capturar antiguos para calcular delta
        int oldMortH = entity.MortalidadH, oldMortM = entity.MortalidadM;
        int oldSelH = entity.SelH, oldSelM = entity.SelM;

        entity.Fecha = dto.Fecha.Date;
        entity.LoteId = dto.LoteId;
        entity.MortalidadH = dto.MortalidadH;
        entity.MortalidadM = dto.MortalidadM;
        entity.SelH = dto.SelH;
        entity.SelM = dto.SelM;
        entity.ConsKgH = dto.ConsKgH + dto.ConsKgM;
        entity.ConsKgM = 0m;
        entity.HuevoTot = dto.HuevoTot;
        entity.HuevoInc = dto.HuevoInc;
        entity.PesoHuevo = dto.PesoHuevo;
        entity.Observaciones = dto.Observaciones;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedByUserId = _current?.UserId ?? 0;

        await _ctx.SaveChangesAsync();

        // Aplicar delta de descuento: nuevos − antiguos
        int deltaH = (dto.MortalidadH + dto.SelH) - (oldMortH + oldSelH);
        int deltaM = (dto.MortalidadM + dto.SelM) - (oldMortM + oldSelM);
        if (deltaH != 0 || deltaM != 0)
        {
            await AplicarDescuentoLppAsync(dto.LoteId, deltaH, deltaM, 0, 0, 0, 0,
                resta: true, CancellationToken.None);
        }

        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _ctx.SeguimientoProduccion.FindAsync(id);
        if (entity == null) return false;

        // Feature 14: si la fila tiene traslado → revertir AMBOS lotes
        bool tieneTraslado = entity.TrasladoIngresoHembras > 0 || entity.TrasladoIngresoMachos > 0
                          || entity.TrasladoSalidaHembras  > 0 || entity.TrasladoSalidaMachos  > 0;

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            if (tieneTraslado)
                await RevertirTrasladoProduccionAsync(entity, CancellationToken.None);

            // Revertir descuento manual sobre LPP (sumar de vuelta lo restado)
            int hRet = entity.MortalidadH + entity.SelH + entity.ErrorSexajeHembras;
            int mRet = entity.MortalidadM + entity.SelM + entity.ErrorSexajeMachos;
            if (hRet > 0 || mRet > 0)
            {
                await AplicarDescuentoLppAsync(entity.LoteId,
                    entity.MortalidadH, entity.MortalidadM,
                    entity.SelH, entity.SelM,
                    entity.ErrorSexajeHembras, entity.ErrorSexajeMachos,
                    resta: false, CancellationToken.None);
            }

            _ctx.SeguimientoProduccion.Remove(entity);
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<SeguimientoProduccionDto>> FilterAsync(FilterSeguimientoProduccionDto filter)
    {
        var query = _ctx.SeguimientoProduccion.AsQueryable();

        if (filter.LoteId.HasValue)
            query = query.Where(x => x.LoteId == filter.LoteId.Value);

        if (filter.Desde.HasValue)
            query = query.Where(x => x.Fecha >= filter.Desde.Value);

        if (filter.Hasta.HasValue)
            query = query.Where(x => x.Fecha <= filter.Hasta.Value);

        return await query
            .OrderByDescending(x => x.Fecha)
            .Select(x => new SeguimientoProduccionDto(
                x.Id,
                x.Fecha,
                x.LoteId.ToString(),
                x.MortalidadH,
                x.MortalidadM,
                x.SelH,
                x.SelM,
                x.ConsKgH,
                x.ConsKgM,
                x.HuevoTot,
                x.HuevoInc,
                x.TipoAlimento,
                x.Observaciones ?? "",
                x.PesoHuevo,
                x.Etapa,
                x.Metadata
            ))
            .ToListAsync();
    }

    // ════════════════════════════════════════════════════════════════════
    // Helpers Feature 14
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MERGE de seguimiento manual sobre fila con sólo-traslado existente.
    /// Conserva columnas traslado_* y añade datos manuales del seguimiento.
    /// Luego aplica el descuento centralizado (mort + sel + err) sobre LPP.
    /// </summary>
    private async Task<SeguimientoProduccionDto> MergearManualSobreTrasladoProdAsync(
        SeguimientoProduccion existente, CreateSeguimientoProduccionDto dto, int userId, CancellationToken ct)
    {
        existente.MortalidadH    = dto.MortalidadH;
        existente.MortalidadM    = dto.MortalidadM;
        existente.SelH           = dto.SelH;
        existente.SelM           = dto.SelM;
        existente.ConsKgH        = dto.ConsKgH + dto.ConsKgM;
        existente.ConsKgM        = 0m;
        existente.HuevoTot       = dto.HuevoTot;
        existente.HuevoInc       = dto.HuevoInc;
        existente.PesoHuevo      = dto.PesoHuevo;
        existente.Observaciones  = string.IsNullOrWhiteSpace(dto.Observaciones)
            ? existente.Observaciones
            : (string.IsNullOrWhiteSpace(existente.Observaciones)
                ? dto.Observaciones
                : $"{existente.Observaciones} | {dto.Observaciones}");
        existente.UpdatedAt      = DateTime.UtcNow;
        existente.UpdatedByUserId = userId;

        await _ctx.SaveChangesAsync(ct);

        // Descuento centralizado en LPP (mismo flujo que un alta normal)
        await AplicarDescuentoLppAsync(dto.LoteId,
            dto.MortalidadH, dto.MortalidadM,
            dto.SelH, dto.SelM,
            0, 0,
            resta: true, ct);

        return MapToDto(existente);
    }

    /// <summary>
    /// Aplica (resta=true) o revierte (resta=false) el descuento de aves en
    /// lote_postura_produccion por mortalidad + selección + error de sexaje.
    /// </summary>
    private async Task AplicarDescuentoLppAsync(int loteId, int mortH, int mortM,
        int selH, int selM, int errH, int errM, bool resta, CancellationToken ct)
    {
        var deltaH = mortH + selH + errH;
        var deltaM = mortM + selM + errM;
        if (deltaH == 0 && deltaM == 0) return;

        var lpp = await _ctx.LotePosturaProduccion
            .Where(l => l.LoteId == loteId && l.DeletedAt == null)
            .FirstOrDefaultAsync(ct);
        if (lpp == null) return;

        var sign = resta ? -1 : 1;
        lpp.AvesHActual = Math.Max(0, (lpp.AvesHActual ?? 0) + sign * deltaH);
        lpp.AvesMActual = Math.Max(0, (lpp.AvesMActual ?? 0) + sign * deltaM);
        lpp.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Revierte el componente de traslado de una fila de produccion_seguimiento.
    /// Restaura aves y acumulados en LPP origen y destino, y borra/limpia la
    /// contraparte según tenga o no datos manuales (igual lógica que Levante).
    /// </summary>
    private async Task RevertirTrasladoProduccionAsync(SeguimientoProduccion ent, CancellationToken ct)
    {
        int salH = ent.TrasladoSalidaHembras, salM = ent.TrasladoSalidaMachos;
        int ingH = ent.TrasladoIngresoHembras, ingM = ent.TrasladoIngresoMachos;
        if (salH == 0 && salM == 0 && ingH == 0 && ingM == 0) return;

        // Identificar LPP de "este lote" via lote_id base
        var lppEste = await _ctx.LotePosturaProduccion
            .Where(l => l.LoteId == ent.LoteId && l.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

        int lppContraId = ent.TrasladoLoteContraparteId ?? 0;
        var lppContra = lppContraId > 0
            ? await _ctx.LotePosturaProduccion
                .Where(l => l.LotePosturaProduccionId == lppContraId && l.DeletedAt == null)
                .FirstOrDefaultAsync(ct)
            : null;

        // === Caso SALIDA: este lote envió aves ===
        if (salH > 0 || salM > 0)
        {
            if (lppEste != null)
            {
                lppEste.ProduccionTrasladoSalidaHembras = Math.Max(0, lppEste.ProduccionTrasladoSalidaHembras - salH);
                lppEste.ProduccionTrasladoSalidaMachos  = Math.Max(0, lppEste.ProduccionTrasladoSalidaMachos  - salM);
                lppEste.AvesHActual = (lppEste.AvesHActual ?? 0) + salH;
                lppEste.AvesMActual = (lppEste.AvesMActual ?? 0) + salM;
            }
            if (lppContra != null)
            {
                lppContra.ProduccionTrasladoIngresoHembras = Math.Max(0, lppContra.ProduccionTrasladoIngresoHembras - salH);
                lppContra.ProduccionTrasladoIngresoMachos  = Math.Max(0, lppContra.ProduccionTrasladoIngresoMachos  - salM);
                lppContra.AvesHActual = Math.Max(0, (lppContra.AvesHActual ?? 0) - salH);
                lppContra.AvesMActual = Math.Max(0, (lppContra.AvesMActual ?? 0) - salM);

                var sdContra = await _ctx.SeguimientoProduccion
                    .Where(s => s.LoteId == lppContra.LoteId && s.Fecha == ent.Fecha
                             && (s.TrasladoIngresoHembras > 0 || s.TrasladoIngresoMachos > 0))
                    .FirstOrDefaultAsync(ct);
                if (sdContra != null)
                {
                    sdContra.TrasladoIngresoHembras = Math.Max(0, sdContra.TrasladoIngresoHembras - salH);
                    sdContra.TrasladoIngresoMachos  = Math.Max(0, sdContra.TrasladoIngresoMachos  - salM);
                    AjustarContraparteProdSiQuedaVacia(sdContra);
                }
            }
        }

        // === Caso INGRESO: este lote recibió aves ===
        if (ingH > 0 || ingM > 0)
        {
            if (lppEste != null)
            {
                lppEste.ProduccionTrasladoIngresoHembras = Math.Max(0, lppEste.ProduccionTrasladoIngresoHembras - ingH);
                lppEste.ProduccionTrasladoIngresoMachos  = Math.Max(0, lppEste.ProduccionTrasladoIngresoMachos  - ingM);
                lppEste.AvesHActual = Math.Max(0, (lppEste.AvesHActual ?? 0) - ingH);
                lppEste.AvesMActual = Math.Max(0, (lppEste.AvesMActual ?? 0) - ingM);
            }
            if (lppContra != null)
            {
                lppContra.ProduccionTrasladoSalidaHembras = Math.Max(0, lppContra.ProduccionTrasladoSalidaHembras - ingH);
                lppContra.ProduccionTrasladoSalidaMachos  = Math.Max(0, lppContra.ProduccionTrasladoSalidaMachos  - ingM);
                lppContra.AvesHActual = (lppContra.AvesHActual ?? 0) + ingH;
                lppContra.AvesMActual = (lppContra.AvesMActual ?? 0) + ingM;

                var sdContra = await _ctx.SeguimientoProduccion
                    .Where(s => s.LoteId == lppContra.LoteId && s.Fecha == ent.Fecha
                             && (s.TrasladoSalidaHembras > 0 || s.TrasladoSalidaMachos > 0))
                    .FirstOrDefaultAsync(ct);
                if (sdContra != null)
                {
                    sdContra.TrasladoSalidaHembras = Math.Max(0, sdContra.TrasladoSalidaHembras - ingH);
                    sdContra.TrasladoSalidaMachos  = Math.Max(0, sdContra.TrasladoSalidaMachos  - ingM);
                    AjustarContraparteProdSiQuedaVacia(sdContra);
                }
            }
        }
    }

    /// <summary>Si la fila contraparte queda sin contenido, la marca para borrar; si tiene manual, sólo limpia flags.</summary>
    private void AjustarContraparteProdSiQuedaVacia(SeguimientoProduccion s)
    {
        bool sinTraslado = s.TrasladoIngresoHembras == 0 && s.TrasladoIngresoMachos == 0
                        && s.TrasladoSalidaHembras  == 0 && s.TrasladoSalidaMachos  == 0;
        bool sinManual = s.MortalidadH == 0 && s.MortalidadM == 0
                      && s.SelH == 0 && s.SelM == 0
                      && s.ErrorSexajeHembras == 0 && s.ErrorSexajeMachos == 0
                      && s.ConsKgH == 0m && s.ConsKgM == 0m && s.HuevoTot == 0;
        if (sinTraslado && sinManual)
        {
            _ctx.SeguimientoProduccion.Remove(s);
        }
        else if (sinTraslado)
        {
            s.EsTraslado = false;
            s.TrasladoDireccion = null;
            s.TrasladoLoteContraparteId = null;
            s.TrasladoGranjaContraparteId = null;
            s.TrasladoHembras = null;
            s.TrasladoMachos = null;
            s.LoteDestinoId = null;
            s.GranjaDestinoId = null;
            s.FechaTraslado = null;
        }
    }

    private static SeguimientoProduccionDto MapToDto(SeguimientoProduccion e) =>
        new SeguimientoProduccionDto(
            e.Id,
            e.Fecha,
            e.LoteId.ToString(),
            e.MortalidadH,
            e.MortalidadM,
            e.SelH,
            e.SelM,
            0m,  // ConsKgH: contrato preservado (el total va en el slot ConsKgM, como con la deprecada)
            e.ConsKgH,
            e.HuevoTot,
            e.HuevoInc,
            "",
            e.Observaciones ?? "",
            e.PesoHuevo,
            0,
            null
        );
}
