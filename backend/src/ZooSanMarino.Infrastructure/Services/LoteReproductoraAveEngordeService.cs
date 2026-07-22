// src/ZooSanMarino.Infrastructure/Services/LoteReproductoraAveEngordeService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LoteReproductoraAveEngordeService : ILoteReproductoraAveEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ICompanyResolver _companyResolver;

    public LoteReproductoraAveEngordeService(ZooSanMarinoContext ctx, ICurrentUser current, ICompanyResolver companyResolver)
    {
        _ctx = ctx;
        _current = current;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return _current.CompanyId;
    }

    private static LoteReproductoraAveEngordeDto Map(LoteReproductoraAveEngorde x, string estado, int avesActuales, ReproStats? stats = null)
    {
        var avesInicioH = x.AvesInicioHembras ?? x.H ?? 0;
        var avesInicioM = x.AvesInicioMachos ?? x.M ?? 0;
        var mixtas = x.Mixtas ?? 0;
        var saldoApertura = avesInicioH + avesInicioM + mixtas;

        var num = stats?.Num ?? 0;
        var avesActualesH = Math.Max(0, avesInicioH - (stats?.MortH ?? 0) - (stats?.SelH ?? 0) - (stats?.ErrH ?? 0));
        var avesActualesM = Math.Max(0, avesInicioM - (stats?.MortM ?? 0) - (stats?.SelM ?? 0) - (stats?.ErrM ?? 0));
        var edadDias = x.FechaEncasetamiento.HasValue
            ? Math.Max(0, (int)(DateTime.UtcNow.Date - x.FechaEncasetamiento.Value.Date).TotalDays)
            : 0;

        return new LoteReproductoraAveEngordeDto(
            x.Id,
            x.LoteAveEngordeId,
            x.ReproductoraId,
            x.NombreLote,
            x.FechaEncasetamiento,
            x.M, x.H, x.Mixtas,
            x.MortCajaH, x.MortCajaM, x.UnifH, x.UnifM,
            x.PesoInicialM, x.PesoInicialH, x.PesoMixto,
            estado,
            avesActuales,
            saldoApertura,
            avesInicioH,
            avesInicioM,
            num,
            edadDias,
            avesActualesH,
            avesActualesM,
            num >= DiasRecogidaReproductora,
            x.CodigoReproductora,
            x.Reabierto,
            x.NovedadApertura
        );
    }

    private static int AvesEncasetadas(LoteReproductoraAveEngorde x)
    {
        var h = x.AvesInicioHembras ?? x.H ?? 0;
        var m = x.AvesInicioMachos ?? x.M ?? 0;
        var mixtas = x.Mixtas ?? 0;
        if (h + m + mixtas == 0)
            return (x.H ?? 0) + (x.M ?? 0) + (x.Mixtas ?? 0);
        return h + m + mixtas;
    }

    /// <summary>Ventas (Venta/Despacho/Retiro) por lote reproductora. Key = LoteReproductoraAveEngordeId.</summary>
    private async Task<Dictionary<int, int>> GetVentasPorReproductoraAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, int>();
        var q = _ctx.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.Estado != "Cancelado" && m.DeletedAt == null &&
                (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro") &&
                m.LoteReproductoraAveEngordeOrigenId != null && idList.Contains(m.LoteReproductoraAveEngordeOrigenId.Value))
            .GroupBy(m => m.LoteReproductoraAveEngordeOrigenId!.Value)
            .Select(g => new { Id = g.Key, Total = g.Sum(m => m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas) });
        var list = await q.ToListAsync(ct);
        return list.ToDictionary(x => x.Id, x => x.Total);
    }

    /// <summary>Máximo de días de recogida de datos del lote reproductora. Al completarlos pasa a Cerrado.</summary>
    private const int DiasRecogidaReproductora = 7;

    /// <summary>Bajas desglosadas por género + nº de registros (total y confirmados) por lote reproductora.</summary>
    private sealed record ReproStats(int MortH, int MortM, int SelH, int SelM, int ErrH, int ErrM, int Num, int NumConfirmados);

    /// <summary>Mortalidad/selección/error por género + cantidad de registros. Key = LoteReproductoraAveEngordeId.</summary>
    private async Task<Dictionary<int, ReproStats>> GetReproStatsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<int, ReproStats>();
        var q = _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
            .Where(s => idList.Contains(s.LoteReproductoraAveEngordeId))
            .GroupBy(s => s.LoteReproductoraAveEngordeId)
            .Select(g => new
            {
                Id = g.Key,
                MortH = g.Sum(s => s.MortalidadHembras ?? 0),
                MortM = g.Sum(s => s.MortalidadMachos ?? 0),
                SelH = g.Sum(s => s.SelH ?? 0),
                SelM = g.Sum(s => s.SelM ?? 0),
                ErrH = g.Sum(s => s.ErrorSexajeHembras ?? 0),
                ErrM = g.Sum(s => s.ErrorSexajeMachos ?? 0),
                Num = g.Count(),
                NumConfirmados = g.Sum(s => s.Confirmado ? 1 : 0)
            });
        var list = await q.ToListAsync(ct);
        return list.ToDictionary(x => x.Id, x => new ReproStats(x.MortH, x.MortM, x.SelH, x.SelM, x.ErrH, x.ErrM, x.Num, x.NumConfirmados));
    }

    // Cerrado SOLO cuando los 7 días están CONFIRMADOS (la confirmación sincroniza hacia pollo engorde).
    // Lógica pura centralizada en Application/Calculos para poder testearla.
    private static (string Estado, int AvesActuales) CalcularEstado(int avesEncasetadas, int ventas, int mortalidad, int seleccion, int errorSexaje = 0, int numConfirmados = 0)
        => ReproductoraEngordeCalculos.CalcularEstado(avesEncasetadas, ventas, mortalidad, seleccion, errorSexaje, numConfirmados, DiasRecogidaReproductora);

    public async Task<IEnumerable<LoteReproductoraAveEngordeDto>> GetAllAsync(int? loteAveEngordeId = null)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var q = from lrae in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                join l in _ctx.LoteAveEngorde.AsNoTracking() on lrae.LoteAveEngordeId equals l.LoteAveEngordeId!.Value
                where l.CompanyId == companyId && l.DeletedAt == null
                   && (!loteAveEngordeId.HasValue || lrae.LoteAveEngordeId == loteAveEngordeId.Value)
                orderby lrae.LoteAveEngordeId, lrae.ReproductoraId
                select lrae;
        var list = await q.ToListAsync();
        if (list.Count == 0) return Array.Empty<LoteReproductoraAveEngordeDto>();
        var ids = list.Select(x => x.Id).ToList();
        var ventas = await GetVentasPorReproductoraAsync(ids);
        var stats = await GetReproStatsAsync(ids);
        return list.Select(x =>
        {
            var encaset = AvesEncasetadas(x);
            var v = ventas.GetValueOrDefault(x.Id, 0);
            var st = stats.GetValueOrDefault(x.Id);
            var mort = (st?.MortH ?? 0) + (st?.MortM ?? 0);
            var sel  = (st?.SelH ?? 0) + (st?.SelM ?? 0);
            var err  = (st?.ErrH ?? 0) + (st?.ErrM ?? 0);
            var (estado, avesActuales) = CalcularEstado(encaset, v, mort, sel, err, st?.NumConfirmados ?? 0);
            return Map(x, estado, avesActuales, st);
        }).ToList();
    }

    public async Task<LoteReproductoraAveEngordeDto?> GetByIdAsync(int id)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await (from lrae in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on lrae.LoteAveEngordeId equals l.LoteAveEngordeId!.Value
                         where l.CompanyId == companyId && l.DeletedAt == null && lrae.Id == id
                         select lrae).SingleOrDefaultAsync();
        if (ent is null) return null;
        var ventas = (await GetVentasPorReproductoraAsync(new[] { id })).GetValueOrDefault(id, 0);
        var st = (await GetReproStatsAsync(new[] { id })).GetValueOrDefault(id);
        var mort = (st?.MortH ?? 0) + (st?.MortM ?? 0);
        var sel  = (st?.SelH ?? 0) + (st?.SelM ?? 0);
        var err  = (st?.ErrH ?? 0) + (st?.ErrM ?? 0);
        var (estado, avesActuales) = CalcularEstado(AvesEncasetadas(ent), ventas, mort, sel, err, st?.NumConfirmados ?? 0);
        return Map(ent, estado, avesActuales, st);
    }

    public async Task<LoteReproductoraAveEngordeDto> CreateAsync(CreateLoteReproductoraAveEngordeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ReproductoraId))
            throw new InvalidOperationException("ReproductoraId es requerido.");
        if (string.IsNullOrWhiteSpace(dto.NombreLote))
            throw new InvalidOperationException("NombreLote es requerido.");

        await EnsureLoteAveEngordeExistsAsync(dto.LoteAveEngordeId);

        var exists = await _ctx.LoteReproductoraAveEngorde
            .AnyAsync(x => x.LoteAveEngordeId == dto.LoteAveEngordeId && x.ReproductoraId == (dto.ReproductoraId ?? "").Trim());
        if (exists)
            throw new InvalidOperationException($"Ya existe un registro con LoteAveEngordeId={dto.LoteAveEngordeId} y ReproductoraId='{dto.ReproductoraId}'.");

        var ent = new LoteReproductoraAveEngorde
        {
            LoteAveEngordeId = dto.LoteAveEngordeId,
            ReproductoraId = (dto.ReproductoraId ?? "").Trim(),
            NombreLote = (dto.NombreLote ?? "").Trim(),
            CodigoReproductora = string.IsNullOrWhiteSpace(dto.CodigoReproductora) ? null : dto.CodigoReproductora.Trim(),
            FechaEncasetamiento = FechasPuras.AnclarMediodiaUtc(dto.FechaEncasetamiento),
            M = dto.M ?? 0,
            H = dto.H ?? 0,
            AvesInicioHembras = dto.H ?? 0,
            AvesInicioMachos = dto.M ?? 0,
            Mixtas = dto.Mixtas ?? 0,
            MortCajaH = dto.MortCajaH ?? 0,
            MortCajaM = dto.MortCajaM ?? 0,
            UnifH = dto.UnifH ?? 0,
            UnifM = dto.UnifM ?? 0,
            PesoInicialM = dto.PesoInicialM,
            PesoInicialH = dto.PesoInicialH,
            PesoMixto = dto.PesoMixto,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.LoteReproductoraAveEngorde.Add(ent);
        await _ctx.SaveChangesAsync();

        var encaset = AvesEncasetadas(ent);
        var (estado, avesActuales) = CalcularEstado(encaset, 0, 0, 0, 0);
        var result = Map(ent, estado, avesActuales);

        var companyId = await GetEffectiveCompanyIdAsync();
        _ctx.HistorialLotePolloEngorde.Add(new HistorialLotePolloEngorde
            {
            CompanyId = companyId,
            TipoLote = "LoteReproductoraAveEngorde",
            LoteAveEngordeId = null,
            LoteReproductoraAveEngordeId = ent.Id,
            TipoRegistro = "Inicio",
            AvesHembras = ent.H ?? 0,
            AvesMachos = ent.M ?? 0,
            AvesMixtas = ent.Mixtas ?? 0,
            FechaRegistro = DateTime.UtcNow,
            MovimientoId = null,
            CreatedAt = DateTime.UtcNow
        });
        await _ctx.SaveChangesAsync();

        return result;
    }

    public async Task<IEnumerable<LoteReproductoraAveEngordeDto>> CreateBulkAsync(IEnumerable<CreateLoteReproductoraAveEngordeDto> dtos)
    {
        var list = dtos?.ToList() ?? new List<CreateLoteReproductoraAveEngordeDto>();
        if (list.Count == 0) return Array.Empty<LoteReproductoraAveEngordeDto>();

        for (var i = 0; i < list.Count; i++)
        {
            var d = list[i];
            if (string.IsNullOrWhiteSpace(d.ReproductoraId))
                throw new InvalidOperationException($"ReproductoraId es requerido (registro {i + 1}).");
            if (string.IsNullOrWhiteSpace(d.NombreLote))
                throw new InvalidOperationException($"NombreLote es requerido (registro {i + 1}).");
        }

        var distinctLotes = list.Select(x => x.LoteAveEngordeId).Distinct().ToList();
        if (distinctLotes.Count != 1)
            throw new InvalidOperationException("Todos los registros bulk deben pertenecer al mismo LoteAveEngordeId.");

        var loteAveEngordeId = distinctLotes[0];
        await EnsureLoteAveEngordeExistsAsync(loteAveEngordeId);

        var incomingKeys = list.Select(x => (x.ReproductoraId ?? "").Trim()).Distinct().ToList();
        var existingRepIds = await _ctx.LoteReproductoraAveEngorde
            .Where(x => x.LoteAveEngordeId == loteAveEngordeId)
            .Select(x => x.ReproductoraId)
            .ToListAsync();
        var existingSet = existingRepIds.ToHashSet();
        var duplicates = incomingKeys.Where(k => existingSet.Contains(k)).ToList();
        if (duplicates.Count > 0)
            throw new InvalidOperationException($"ReproductoraId ya existentes: {string.Join(", ", duplicates)}.");

        var duplicatesInPayload = list.GroupBy(x => (x.ReproductoraId ?? "").Trim()).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicatesInPayload.Count > 0)
            throw new InvalidOperationException($"Duplicados en el payload: {string.Join(", ", duplicatesInPayload)}.");

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var entities = list.Select(dto => new LoteReproductoraAveEngorde
            {
                LoteAveEngordeId = dto.LoteAveEngordeId,
                ReproductoraId = (dto.ReproductoraId ?? "").Trim(),
                NombreLote = (dto.NombreLote ?? "").Trim(),
                CodigoReproductora = string.IsNullOrWhiteSpace(dto.CodigoReproductora) ? null : dto.CodigoReproductora.Trim(),
                FechaEncasetamiento = FechasPuras.AnclarMediodiaUtc(dto.FechaEncasetamiento),
                M = dto.M ?? 0,
                H = dto.H ?? 0,
                AvesInicioHembras = dto.H ?? 0,
                AvesInicioMachos = dto.M ?? 0,
                Mixtas = dto.Mixtas ?? 0,
                MortCajaH = dto.MortCajaH ?? 0,
                MortCajaM = dto.MortCajaM ?? 0,
                UnifH = dto.UnifH ?? 0,
                UnifM = dto.UnifM ?? 0,
                PesoInicialM = dto.PesoInicialM,
                PesoInicialH = dto.PesoInicialH,
                PesoMixto = dto.PesoMixto,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }).ToList();
            _ctx.LoteReproductoraAveEngorde.AddRange(entities);
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return await GetAllAsync(loteAveEngordeId);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<LoteReproductoraAveEngordeDto?> UpdateAsync(int id, UpdateLoteReproductoraAveEngordeDto dto)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await (from lrae in _ctx.LoteReproductoraAveEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on lrae.LoteAveEngordeId equals l.LoteAveEngordeId!.Value
                         where l.CompanyId == companyId && l.DeletedAt == null && lrae.Id == id
                         select lrae).SingleOrDefaultAsync();
        if (ent is null) return null;

        if (string.IsNullOrWhiteSpace(dto.NombreLote))
            throw new InvalidOperationException("NombreLote es requerido.");
        var reproductoraId = (dto.ReproductoraId ?? "").Trim();
        if (string.IsNullOrEmpty(reproductoraId))
            throw new InvalidOperationException("ReproductoraId es requerido.");

        if (reproductoraId != ent.ReproductoraId)
        {
            var exists = await _ctx.LoteReproductoraAveEngorde
                .AnyAsync(x => x.LoteAveEngordeId == ent.LoteAveEngordeId && x.ReproductoraId == reproductoraId && x.Id != id);
            if (exists)
                throw new InvalidOperationException($"Ya existe otro registro en este lote con ReproductoraId '{reproductoraId}'.");
            ent.ReproductoraId = reproductoraId;
        }

        ent.NombreLote = (dto.NombreLote ?? "").Trim();
        ent.CodigoReproductora = string.IsNullOrWhiteSpace(dto.CodigoReproductora) ? null : dto.CodigoReproductora.Trim();
        ent.FechaEncasetamiento = FechasPuras.AnclarMediodiaUtc(dto.FechaEncasetamiento);
        ent.M = dto.M;
        ent.H = dto.H;
        ent.Mixtas = dto.Mixtas;
        ent.MortCajaH = dto.MortCajaH;
        ent.MortCajaM = dto.MortCajaM;
        ent.UnifH = dto.UnifH;
        ent.UnifM = dto.UnifM;
        ent.PesoInicialM = dto.PesoInicialM;
        ent.PesoInicialH = dto.PesoInicialH;
        ent.PesoMixto = dto.PesoMixto;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        var ventas = (await GetVentasPorReproductoraAsync(new[] { id })).GetValueOrDefault(id, 0);
        var stU = (await GetReproStatsAsync(new[] { id })).GetValueOrDefault(id);
        var mortU = (stU?.MortH ?? 0) + (stU?.MortM ?? 0);
        var selU  = (stU?.SelH ?? 0) + (stU?.SelM ?? 0);
        var errU  = (stU?.ErrH ?? 0) + (stU?.ErrM ?? 0);
        var (estado, avesActuales) = CalcularEstado(AvesEncasetadas(ent), ventas, mortU, selU, errU, stU?.NumConfirmados ?? 0);
        return Map(ent, estado, avesActuales, stU);
    }

    /// <summary>
    /// Reabre un lote reproductora cerrado dejando registrada la novedad (motivo) y la auditoría.
    /// Habilita la eliminación de registros de seguimiento. El estado vuelve a recalcularse
    /// ("recierra solo") cuando se elimina un registro.
    /// </summary>
    public async Task<LoteReproductoraAveEngordeDto?> ReabrirAsync(int id, string novedad)
    {
        if (string.IsNullOrWhiteSpace(novedad))
            throw new InvalidOperationException("La novedad es obligatoria para reabrir el lote.");

        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await (from lrae in _ctx.LoteReproductoraAveEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on lrae.LoteAveEngordeId equals l.LoteAveEngordeId!.Value
                         where l.CompanyId == companyId && l.DeletedAt == null && lrae.Id == id
                         select lrae).SingleOrDefaultAsync();
        if (ent is null) return null;

        ent.Reabierto = true;
        ent.NovedadApertura = novedad.Trim();
        ent.ReabiertoPor = _current.UserId;
        ent.ReabiertoAt = DateTime.UtcNow;
        ent.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();

        var ventas = (await GetVentasPorReproductoraAsync(new[] { id })).GetValueOrDefault(id, 0);
        var st = (await GetReproStatsAsync(new[] { id })).GetValueOrDefault(id);
        var mort = (st?.MortH ?? 0) + (st?.MortM ?? 0);
        var sel  = (st?.SelH ?? 0) + (st?.SelM ?? 0);
        var err  = (st?.ErrH ?? 0) + (st?.ErrM ?? 0);
        var (estado, avesActuales) = CalcularEstado(AvesEncasetadas(ent), ventas, mort, sel, err, st?.NumConfirmados ?? 0);
        return Map(ent, estado, avesActuales, st);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var ent = await (from lrae in _ctx.LoteReproductoraAveEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on lrae.LoteAveEngordeId equals l.LoteAveEngordeId!.Value
                         where l.CompanyId == companyId && l.DeletedAt == null && lrae.Id == id
                         select lrae).SingleOrDefaultAsync();
        if (ent is null) return false;
        _ctx.LoteReproductoraAveEngorde.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<AvesDisponiblesDto?> GetAvesDisponiblesAsync(int loteAveEngordeId)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .Where(l => l.LoteAveEngordeId == loteAveEngordeId && l.CompanyId == companyId && l.DeletedAt == null)
            .Select(l => new { l.HembrasL, l.MachosL, l.MortCajaH, l.MortCajaM })
            .SingleOrDefaultAsync();
        if (lote == null) return null;

        var asignadas = await _ctx.LoteReproductoraAveEngorde
            .AsNoTracking()
            .Where(lr => lr.LoteAveEngordeId == loteAveEngordeId)
            .GroupBy(_ => 1)
            .Select(g => new { AsignadasH = g.Sum(x => x.H ?? 0), AsignadasM = g.Sum(x => x.M ?? 0) })
            .SingleOrDefaultAsync();

        // ── Devolución automática de aves al lote tras los 7 días de los reproductora ──
        // Cuando TODOS los lotes reproductora completaron sus 7 registros, las aves vivas
        // restantes "regresan" al lote pollo engorde para poder seguir el seguimiento allí.
        const int diasSeguimientoReproductora = 7;
        var nLotesRepro = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            .CountAsync(lr => lr.LoteAveEngordeId == loteAveEngordeId);
        // El saldo "regresa" a pollo engorde solo cuando cada lote reproductora tiene sus 7 días
        // CONFIRMADOS (la confirmación es la que sincroniza el cruce diario). Contar confirmados, no registros.
        var nReproCompletos = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            .CountAsync(lr => lr.LoteAveEngordeId == loteAveEngordeId
                && _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                       .Count(s => s.LoteReproductoraAveEngordeId == lr.Id && s.Confirmado) >= diasSeguimientoReproductora);
        bool sieteDiasCompletos = nLotesRepro > 0 && nReproCompletos == nLotesRepro;

        // Mortalidad en caja de los lotes reproductora (no está en los registros de cruce).
        var mortCajaRepro = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            .Where(lr => lr.LoteAveEngordeId == loteAveEngordeId)
            .GroupBy(_ => 1)
            .Select(g => new { H = g.Sum(x => x.MortCajaH ?? 0), M = g.Sum(x => x.MortCajaM ?? 0) })
            .SingleOrDefaultAsync();
        int mortCajaReproH = mortCajaRepro?.H ?? 0;
        int mortCajaReproM = mortCajaRepro?.M ?? 0;

        // Mortalidad y bajas acumuladas del seguimiento diario (mortalidad + selección + error sexaje)
        var segAcum = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteAveEngordeId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MortH = g.Sum(x => x.MortalidadHembras ?? 0),
                MortM = g.Sum(x => x.MortalidadMachos ?? 0),
                SelH = g.Sum(x => x.SelH ?? 0),
                SelM = g.Sum(x => x.SelM ?? 0),
                ErrH = g.Sum(x => x.ErrorSexajeHembras ?? 0),
                ErrM = g.Sum(x => x.ErrorSexajeMachos ?? 0)
            })
            .SingleOrDefaultAsync();

        // Encaset real (mostrar): historial Inicio. La fórmula de disponibles usa el saldo actual del maestro como base de restas (no cambiar sin revisar ventas).
        var inicioHist = await _ctx.HistorialLotePolloEngorde.AsNoTracking()
            .Where(h => h.CompanyId == companyId && h.LoteAveEngordeId == loteAveEngordeId
                && h.TipoLote == "LoteAveEngorde" && h.TipoRegistro == "Inicio")
            .OrderBy(h => h.FechaRegistro).ThenBy(h => h.Id)
            .FirstOrDefaultAsync();
        int hembrasInicialesEncaset = inicioHist?.AvesHembras ?? (lote.HembrasL ?? 0);
        int machosInicialesEncaset = inicioHist?.AvesMachos ?? (lote.MachosL ?? 0);
        int hembrasIniciales = lote.HembrasL ?? 0;
        int machosIniciales = lote.MachosL ?? 0;
        int mortCajaH = lote.MortCajaH ?? 0;
        int mortCajaM = lote.MortCajaM ?? 0;
        int asignadasH = asignadas?.AsignadasH ?? 0;
        int asignadasM = asignadas?.AsignadasM ?? 0;
        int mortSegH = segAcum?.MortH ?? 0;
        int mortSegM = segAcum?.MortM ?? 0;
        int selH = segAcum?.SelH ?? 0;
        int selM = segAcum?.SelM ?? 0;
        int errH = segAcum?.ErrH ?? 0;
        int errM = segAcum?.ErrM ?? 0;

        // Reserva por ventas/despachos Pendientes: descuentan disponibilidad aunque aún no
        // tocan el maestro (mismo criterio que MovimientoPolloEngordeService.ResumenDisponibilidad).
        var pend = await _ctx.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.Estado == "Pendiente" && m.DeletedAt == null
                && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                && m.LoteAveEngordeOrigenId == loteAveEngordeId)
            .GroupBy(_ => 1)
            .Select(g => new { H = g.Sum(x => x.CantidadHembras), M = g.Sum(x => x.CantidadMachos) })
            .SingleOrDefaultAsync();
        int pendH = pend?.H ?? 0;
        int pendM = pend?.M ?? 0;

        int hembrasDisponibles, machosDisponibles;
        if (sieteDiasCompletos)
        {
            // Aves devueltas al lote: NO se restan las asignadas (las aves regresan).
            // Las bajas diarias de los reproductora (días 1-7) ya están en los registros
            // de cruce de seguimiento_diario_aves_engorde → se restan vía mortSeg/sel/err.
            hembrasDisponibles = Math.Max(0, hembrasIniciales - mortCajaH - mortCajaReproH - mortSegH - selH - errH - pendH);
            machosDisponibles  = Math.Max(0, machosIniciales  - mortCajaM - mortCajaReproM - mortSegM - selM - errM - pendM);
        }
        else
        {
            // Aves aún distribuidas en los reproductora (no se devuelven hasta completar 7 días).
            hembrasDisponibles = Math.Max(0, hembrasIniciales - mortCajaH - asignadasH - mortSegH - selH - errH - pendH);
            machosDisponibles  = Math.Max(0, machosIniciales  - mortCajaM - asignadasM - mortSegM - selM - errM - pendM);
        }

        return new AvesDisponiblesDto
        {
            HembrasIniciales = hembrasInicialesEncaset,
            MachosIniciales = machosInicialesEncaset,
            MortalidadAcumuladaHembras = mortSegH,
            MortalidadAcumuladaMachos = mortSegM,
            SeleccionAcumuladaHembras = selH,
            SeleccionAcumuladaMachos = selM,
            MortCajaHembras = mortCajaH,
            MortCajaMachos = mortCajaM,
            AsignadasHembras = asignadasH,
            AsignadasMachos = asignadasM,
            HembrasReservadasPendiente = pendH,
            MachosReservadasPendiente = pendM,
            HembrasDisponibles = hembrasDisponibles,
            MachosDisponibles = machosDisponibles,
            // Total mixto: las aves no se devuelven por género, se suman en un solo valor.
            MixtasDisponibles = hembrasDisponibles + machosDisponibles,
            AvesDevueltas = sieteDiasCompletos
        };
    }

    public async Task<string> GetNewReproductoraCodeAsync(int loteAveEngordeId, IEnumerable<string>? exclude = null)
    {
        await EnsureLoteAveEngordeExistsAsync(loteAveEngordeId);
        var existingList = await _ctx.LoteReproductoraAveEngorde
            .AsNoTracking()
            .Where(x => x.LoteAveEngordeId == loteAveEngordeId)
            .Select(x => x.ReproductoraId)
            .ToListAsync();
        var existing = existingList.ToHashSet();
        if (exclude != null)
            foreach (var s in exclude.Where(x => !string.IsNullOrWhiteSpace(x)))
                existing.Add(s.Trim());
        const int digits = 10;
        const int maxAttempts = 50;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var code = "LR-" + string.Concat(Enumerable.Range(0, digits).Select(_ => Random.Shared.Next(0, 10).ToString()));
            if (!existing.Contains(code))
                return code;
        }
        throw new InvalidOperationException("No se pudo generar un código único. Intente de nuevo.");
    }

    private async Task EnsureLoteAveEngordeExistsAsync(int loteAveEngordeId)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        var exists = await _ctx.LoteAveEngorde.AsNoTracking()
            .AnyAsync(l => l.LoteAveEngordeId == loteAveEngordeId && l.CompanyId == companyId && l.DeletedAt == null);
        if (!exists)
            throw new InvalidOperationException($"Lote Aves de Engorde '{loteAveEngordeId}' no existe o no pertenece a la compañía.");
    }
}
