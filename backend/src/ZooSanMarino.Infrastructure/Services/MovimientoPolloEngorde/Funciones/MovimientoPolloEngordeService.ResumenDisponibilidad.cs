// MovimientoPolloEngorde/Funciones/MovimientoPolloEngordeService.ResumenDisponibilidad.cs
// Resumen de aves por lote y disponibilidad para venta.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using PagedResultCommon = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.MovimientoPolloEngordeDto>;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoPolloEngordeService
{
    public async Task<ResumenAvesLoteDto?> GetResumenAvesLoteAsync(string tipoLote, int loteId)
    {
        if (tipoLote == "LoteAveEngorde")
        {
            var map = await LoadResumenAvesLoteAveEngordeBatchAsync(new[] { loteId });
            return map.GetValueOrDefault(loteId);
        }

        if (tipoLote == "LoteReproductoraAveEngorde")
        {
            var map = await LoadResumenAvesLoteReproductoraBatchAsync(new[] { loteId });
            return map.GetValueOrDefault(loteId);
        }

        return null;
    }

    /// <summary>
    /// Historial inicio + lotes actuales + movimientos completados (origen), agrupado por lote.
    /// Tres consultas a BD en lugar de N×3 por lote.
    /// </summary>
    private async Task<Dictionary<int, ResumenAvesLoteDto?>> LoadResumenAvesLoteAveEngordeBatchAsync(IReadOnlyList<int> loteIds)
    {
        var companyId = _currentUser.CompanyId;
        var ids = loteIds.Distinct().ToList();
        var result = new Dictionary<int, ResumenAvesLoteDto?>();
        if (ids.Count == 0)
            return result;

        var histRows = await _ctx.HistorialLotePolloEngorde
            .AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId
                && h.TipoLote == "LoteAveEngorde"
                && h.TipoRegistro == "Inicio"
                && h.LoteAveEngordeId != null
                && ids.Contains(h.LoteAveEngordeId.Value))
            .Select(h => new { h.LoteAveEngordeId, h.AvesHembras, h.AvesMachos, h.AvesMixtas, h.FechaRegistro })
            .ToListAsync();

        var inicioPorLote = new Dictionary<int, (int H, int M, int X)>();
        foreach (var g in histRows.GroupBy(h => h.LoteAveEngordeId!.Value))
        {
            var first = g.OrderBy(h => h.FechaRegistro).First();
            inicioPorLote[g.Key] = (first.AvesHembras, first.AvesMachos, first.AvesMixtas);
        }

        var lotes = await _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l =>
                l.CompanyId == companyId
                && l.DeletedAt == null
                && l.LoteAveEngordeId != null
                && ids.Contains(l.LoteAveEngordeId.Value))
            .Select(l => new { l.LoteAveEngordeId, l.LoteNombre, l.HembrasL, l.MachosL, l.Mixtas, l.AvesEncasetadas })
            .ToListAsync();
        var lotePorId = lotes.ToDictionary(x => x.LoteAveEngordeId!.Value);

        var movRows = await _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId
                && x.DeletedAt == null
                && x.Estado == "Completado"
                && x.LoteAveEngordeOrigenId != null
                && ids.Contains(x.LoteAveEngordeOrigenId.Value))
            .Select(x => new { x.LoteAveEngordeOrigenId, x.CantidadHembras, x.CantidadMachos, x.CantidadMixtas, x.TipoMovimiento })
            .ToListAsync();

        var salidasPorLote = movRows
            .GroupBy(x => x.LoteAveEngordeOrigenId!.Value)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var salidas = g.Sum(x => x.CantidadHembras + x.CantidadMachos + x.CantidadMixtas);
                    var vendidas = g.Where(x => x.TipoMovimiento == "Venta").Sum(x => x.CantidadHembras + x.CantidadMachos + x.CantidadMixtas);
                    return (salidas, vendidas);
                });

        foreach (var id in ids)
        {
            if (!lotePorId.TryGetValue(id, out var lote))
            {
                result[id] = null;
                continue;
            }

            inicioPorLote.TryGetValue(id, out var ini);
            var avesInicioH = ini.H;
            var avesInicioM = ini.M;
            var avesInicioX = ini.X;

            var avesActualesH = lote.HembrasL ?? 0;
            var avesActualesM = lote.MachosL ?? 0;
            var avesActualesX = lote.Mixtas ?? 0;
            if (avesActualesH + avesActualesM + avesActualesX == 0 && (lote.AvesEncasetadas ?? 0) > 0)
                avesActualesX = lote.AvesEncasetadas ?? 0;

            salidasPorLote.TryGetValue(id, out var sal);
            var avesSalidasTotal = sal.salidas;
            var avesVendidasTotal = sal.vendidas;

            result[id] = new ResumenAvesLoteDto(
                TipoLote: "LoteAveEngorde",
                LoteId: id,
                NombreLote: lote.LoteNombre,
                AvesInicioHembras: avesInicioH,
                AvesInicioMachos: avesInicioM,
                AvesInicioMixtas: avesInicioX,
                AvesInicioTotal: avesInicioH + avesInicioM + avesInicioX,
                AvesSalidasTotal: avesSalidasTotal,
                AvesVendidasTotal: avesVendidasTotal,
                AvesActualesHembras: avesActualesH,
                AvesActualesMachos: avesActualesM,
                AvesActualesMixtas: avesActualesX,
                AvesActualesTotal: avesActualesH + avesActualesM + avesActualesX
            );
        }

        return result;
    }

    private async Task<Dictionary<int, ResumenAvesLoteDto?>> LoadResumenAvesLoteReproductoraBatchAsync(IReadOnlyList<int> loteIds)
    {
        var companyId = _currentUser.CompanyId;
        var ids = loteIds.Distinct().ToList();
        var result = new Dictionary<int, ResumenAvesLoteDto?>();
        if (ids.Count == 0)
            return result;

        var histRows = await _ctx.HistorialLotePolloEngorde
            .AsNoTracking()
            .Where(h =>
                h.CompanyId == companyId
                && h.TipoLote == "LoteReproductoraAveEngorde"
                && h.TipoRegistro == "Inicio"
                && h.LoteReproductoraAveEngordeId != null
                && ids.Contains(h.LoteReproductoraAveEngordeId.Value))
            .Select(h => new { h.LoteReproductoraAveEngordeId, h.AvesHembras, h.AvesMachos, h.AvesMixtas, h.FechaRegistro })
            .ToListAsync();

        var inicioPorLote = new Dictionary<int, (int H, int M, int X)>();
        foreach (var g in histRows.GroupBy(h => h.LoteReproductoraAveEngordeId!.Value))
        {
            var first = g.OrderBy(h => h.FechaRegistro).First();
            inicioPorLote[g.Key] = (first.AvesHembras, first.AvesMachos, first.AvesMixtas);
        }

        var lotes = await (
            from lrae in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
            join lae in _ctx.LoteAveEngorde.AsNoTracking() on lrae.LoteAveEngordeId equals lae.LoteAveEngordeId
            where ids.Contains(lrae.Id) && lae.CompanyId == companyId && lae.DeletedAt == null
            select new { lrae.Id, lrae.NombreLote, lrae.H, lrae.M, lrae.Mixtas }
        ).ToListAsync();
        var lotePorId = lotes.ToDictionary(x => x.Id);

        var movRows = await _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Where(x =>
                x.CompanyId == companyId
                && x.DeletedAt == null
                && x.Estado == "Completado"
                && x.LoteReproductoraAveEngordeOrigenId != null
                && ids.Contains(x.LoteReproductoraAveEngordeOrigenId.Value))
            .Select(x => new { x.LoteReproductoraAveEngordeOrigenId, x.CantidadHembras, x.CantidadMachos, x.CantidadMixtas, x.TipoMovimiento })
            .ToListAsync();

        var salidasPorLote = movRows
            .GroupBy(x => x.LoteReproductoraAveEngordeOrigenId!.Value)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var salidas = g.Sum(x => x.CantidadHembras + x.CantidadMachos + x.CantidadMixtas);
                    var vendidas = g.Where(x => x.TipoMovimiento == "Venta").Sum(x => x.CantidadHembras + x.CantidadMachos + x.CantidadMixtas);
                    return (salidas, vendidas);
                });

        foreach (var id in ids)
        {
            if (!lotePorId.TryGetValue(id, out var lote))
            {
                result[id] = null;
                continue;
            }

            inicioPorLote.TryGetValue(id, out var ini);
            var avesInicioH = ini.H;
            var avesInicioM = ini.M;
            var avesInicioX = ini.X;

            var avesActualesH = lote.H ?? 0;
            var avesActualesM = lote.M ?? 0;
            var avesActualesX = lote.Mixtas ?? 0;

            salidasPorLote.TryGetValue(id, out var sal);
            var avesSalidasTotal = sal.salidas;
            var avesVendidasTotal = sal.vendidas;

            result[id] = new ResumenAvesLoteDto(
                TipoLote: "LoteReproductoraAveEngorde",
                LoteId: id,
                NombreLote: lote.NombreLote,
                AvesInicioHembras: avesInicioH,
                AvesInicioMachos: avesInicioM,
                AvesInicioMixtas: avesInicioX,
                AvesInicioTotal: avesInicioH + avesInicioM + avesInicioX,
                AvesSalidasTotal: avesSalidasTotal,
                AvesVendidasTotal: avesVendidasTotal,
                AvesActualesHembras: avesActualesH,
                AvesActualesMachos: avesActualesM,
                AvesActualesMixtas: avesActualesX,
                AvesActualesTotal: avesActualesH + avesActualesM + avesActualesX
            );
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ResumenAvesLotesResponse> GetResumenAvesLotesAsync(ResumenAvesLotesRequest request)
    {
        var tipo = string.IsNullOrWhiteSpace(request.TipoLote) ? "LoteAveEngorde" : request.TipoLote.Trim();
        var loteIds = request.LoteIds ?? new List<int>();
        if (loteIds.Count == 0)
            return new ResumenAvesLotesResponse { Items = new List<ResumenAvesLotePorIdDto>() };

        Dictionary<int, ResumenAvesLoteDto?> map;
        if (tipo == "LoteAveEngorde")
            map = await LoadResumenAvesLoteAveEngordeBatchAsync(loteIds);
        else if (tipo == "LoteReproductoraAveEngorde")
            map = await LoadResumenAvesLoteReproductoraBatchAsync(loteIds);
        else
        {
            var itemsInvalid = loteIds.Select(id => new ResumenAvesLotePorIdDto(id, null)).ToList();
            return new ResumenAvesLotesResponse { Items = itemsInvalid };
        }

        var items = new List<ResumenAvesLotePorIdDto>(loteIds.Count);
        foreach (var loteId in loteIds)
            items.Add(new ResumenAvesLotePorIdDto(loteId, map.GetValueOrDefault(loteId)));

        return new ResumenAvesLotesResponse { Items = items };
    }

    /// <inheritdoc />
    public async Task<AvesDisponiblesLotesResponse> GetAvesDisponiblesLotesAsync(AvesDisponiblesLotesRequest request)
    {
        var tipo = string.IsNullOrWhiteSpace(request.TipoLote) ? "LoteAveEngorde" : request.TipoLote.Trim();
        var loteIds = request.LoteIds ?? new List<int>();
        if (loteIds.Count == 0)
            return new AvesDisponiblesLotesResponse { Items = new List<AvesDisponiblesLotePorIdDto>() };

        var ids = loteIds.Distinct().ToList();
        var companyId = _currentUser.CompanyId;

        if (tipo != "LoteAveEngorde" && tipo != "LoteReproductoraAveEngorde")
        {
            return new AvesDisponiblesLotesResponse
            {
                Items = loteIds.Select(id => new AvesDisponiblesLotePorIdDto(id, null)).ToList()
            };
        }

        if (tipo == "LoteReproductoraAveEngorde")
        {
            // Por ahora, para reproductora se reporta el "inventario actual" y reservas pendientes por lote reproductora.
            // (El caso reportado por el usuario ocurre en LoteAveEngorde.)
            var lotes = await _ctx.LoteReproductoraAveEngorde
                .AsNoTracking()
                .Where(l => ids.Contains(l.Id))
                .Select(l => new { l.Id, l.NombreLote, H = l.H ?? 0, M = l.M ?? 0, X = l.Mixtas ?? 0 })
                .ToListAsync();
            var byId = lotes.ToDictionary(x => x.Id);

            var pend = await _ctx.MovimientoPolloEngorde
                .AsNoTracking()
                .Where(m =>
                    m.CompanyId == companyId
                    && m.DeletedAt == null
                    && m.Estado == "Pendiente"
                    && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                    && m.LoteReproductoraAveEngordeOrigenId != null
                    && ids.Contains(m.LoteReproductoraAveEngordeOrigenId.Value))
                .Select(m => new { Id = m.LoteReproductoraAveEngordeOrigenId!.Value, m.CantidadHembras, m.CantidadMachos, m.CantidadMixtas })
                .ToListAsync();

            var pendVentaRepById = pend
                .GroupBy(x => x.Id)
                .ToDictionary(
                    g => g.Key,
                    g => (
                        H: g.Sum(x => x.CantidadHembras),
                        M: g.Sum(x => x.CantidadMachos),
                        X: g.Sum(x => x.CantidadMixtas)
                    ));

            var outItems = new List<AvesDisponiblesLotePorIdDto>(loteIds.Count);
            foreach (var id in loteIds)
            {
                if (!byId.TryGetValue(id, out var l))
                {
                    outItems.Add(new AvesDisponiblesLotePorIdDto(id, null));
                    continue;
                }

                pendVentaRepById.TryGetValue(id, out var p);
                var dispH = Math.Max(0, l.H - p.H);
                var dispM = Math.Max(0, l.M - p.M);
                var dispX = Math.Max(0, l.X - p.X);
                outItems.Add(new AvesDisponiblesLotePorIdDto(
                    id,
                    new AvesDisponiblesVentaLoteDto(
                        LoteId: id,
                        TipoLote: "LoteReproductoraAveEngorde",
                        NombreLote: l.NombreLote,
                        HembrasDisponibles: dispH,
                        MachosDisponibles: dispM,
                        MixtasDisponibles: dispX,
                        TotalDisponibles: dispH + dispM + dispX,
                        HembrasReservadasPendiente: p.H,
                        MachosReservadasPendiente: p.M,
                        MixtasReservadasPendiente: p.X,
                        TotalReservadasPendiente: p.H + p.M + p.X
                    )));
            }

            return new AvesDisponiblesLotesResponse { Items = outItems };
        }

        // LoteAveEngorde: misma fórmula que GetAvesDisponiblesAsync + restar reservas Pendiente de ventas.
        var lotesAe = await _ctx.LoteAveEngorde
            .AsNoTracking()
            .Where(l =>
                l.CompanyId == companyId
                && l.DeletedAt == null
                && l.LoteAveEngordeId != null
                && ids.Contains(l.LoteAveEngordeId.Value))
            .Select(l => new
            {
                Id = l.LoteAveEngordeId!.Value,
                l.LoteNombre,
                HembrasL = l.HembrasL ?? 0,
                MachosL = l.MachosL ?? 0,
                Mixtas = l.Mixtas ?? 0,
                Encaset = l.AvesEncasetadas ?? 0,
                MortCajaH = l.MortCajaH ?? 0,
                MortCajaM = l.MortCajaM ?? 0
            })
            .ToListAsync();
        var aeById = lotesAe.ToDictionary(x => x.Id);

        // Reproductoras: asignadas + mortCaja propias + conteo para sieteDiasCompletos
        const int diasSeguimientoReproductora = 7;
        var reproData = await _ctx.LoteReproductoraAveEngorde
            .AsNoTracking()
            .Where(lr => ids.Contains(lr.LoteAveEngordeId))
            .GroupBy(lr => lr.LoteAveEngordeId)
            .Select(g => new
            {
                Id = g.Key,
                AsigH = g.Sum(x => x.H ?? 0),
                AsigM = g.Sum(x => x.M ?? 0),
                MortCajaH = g.Sum(x => x.MortCajaH ?? 0),
                MortCajaM = g.Sum(x => x.MortCajaM ?? 0),
                NTotal = g.Count()
            })
            .ToListAsync();
        var reproById = reproData.ToDictionary(x => x.Id);

        // Contar cuántos reproductora tienen sus 7 días CONFIRMADOS por lote (la confirmación es la que
        // sincroniza el cruce; el saldo "regresa" a pollo engorde solo con los 7 confirmados).
        var reproComplData = await _ctx.LoteReproductoraAveEngorde
            .AsNoTracking()
            .Where(lr => ids.Contains(lr.LoteAveEngordeId))
            .Select(lr => new
            {
                lr.LoteAveEngordeId,
                Completo = _ctx.SeguimientoDiarioLoteReproductoraAvesEngorde
                    .Count(s => s.LoteReproductoraAveEngordeId == lr.Id && s.Confirmado) >= diasSeguimientoReproductora
            })
            .ToListAsync();
        var sieteDiasById = reproComplData
            .GroupBy(x => x.LoteAveEngordeId)
            .ToDictionary(
                g => g.Key,
                g => g.All(x => x.Completo) && g.Any());

        var segAcum = await _ctx.SeguimientoDiarioAvesEngorde
            .AsNoTracking()
            .Where(s => ids.Contains(s.LoteAveEngordeId))
            .GroupBy(s => s.LoteAveEngordeId)
            .Select(g => new
            {
                Id = g.Key,
                MortH = g.Sum(x => x.MortalidadHembras ?? 0),
                MortM = g.Sum(x => x.MortalidadMachos ?? 0),
                SelH = g.Sum(x => x.SelH ?? 0),
                SelM = g.Sum(x => x.SelM ?? 0),
                ErrH = g.Sum(x => x.ErrorSexajeHembras ?? 0),
                ErrM = g.Sum(x => x.ErrorSexajeMachos ?? 0)
            })
            .ToListAsync();
        var segBy = segAcum.ToDictionary(
            x => x.Id,
            x => (
                MortH: x.MortH,
                MortM: x.MortM,
                SelH: x.SelH,
                SelM: x.SelM,
                ErrH: x.ErrH,
                ErrM: x.ErrM
            ));

        var pendientes = await _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Where(m =>
                m.CompanyId == companyId
                && m.DeletedAt == null
                && m.Estado == "Pendiente"
                && (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro")
                && m.LoteAveEngordeOrigenId != null
                && ids.Contains(m.LoteAveEngordeOrigenId.Value))
            .Select(m => new { Id = m.LoteAveEngordeOrigenId!.Value, m.CantidadHembras, m.CantidadMachos, m.CantidadMixtas, m.EsVentaMixta })
            .ToListAsync();
        // Reserva efectiva: siempre H sobre HembrasL y M sobre MachosL (Panama incluido).
        // Para Panama (EsVentaMixta) no hay X reservado porque las aves viven en H/M.
        var pendVentaById = pendientes
            .GroupBy(x => x.Id)
            .ToDictionary(
                g => g.Key,
                g => (
                    H: g.Sum(x => x.CantidadHembras),
                    M: g.Sum(x => x.CantidadMachos),
                    X: g.Sum(x => x.EsVentaMixta ? 0 : x.CantidadMixtas)
                ));

        var items = new List<AvesDisponiblesLotePorIdDto>(loteIds.Count);
        foreach (var id in loteIds)
        {
            if (!aeById.TryGetValue(id, out var l))
            {
                items.Add(new AvesDisponiblesLotePorIdDto(id, null));
                continue;
            }

            var repro = reproById.GetValueOrDefault(id);
            var asigH = repro?.AsigH ?? 0;
            var asigM = repro?.AsigM ?? 0;
            var mortCajaReproH = repro?.MortCajaH ?? 0;
            var mortCajaReproM = repro?.MortCajaM ?? 0;
            var sieteDias = sieteDiasById.GetValueOrDefault(id, false);
            var seg = segBy.GetValueOrDefault(id, (MortH: 0, MortM: 0, SelH: 0, SelM: 0, ErrH: 0, ErrM: 0));
            pendVentaById.TryGetValue(id, out var p);

            int rawH, rawM;
            if (sieteDias)
            {
                // Aves devueltas al lote: no se restan las asignadas a reproductora
                rawH = Math.Max(0, l.HembrasL - l.MortCajaH - mortCajaReproH - seg.MortH - seg.SelH - seg.ErrH);
                rawM = Math.Max(0, l.MachosL - l.MortCajaM - mortCajaReproM - seg.MortM - seg.SelM - seg.ErrM);
            }
            else
            {
                rawH = Math.Max(0, l.HembrasL - l.MortCajaH - asigH - seg.MortH - seg.SelH - seg.ErrH);
                rawM = Math.Max(0, l.MachosL - l.MortCajaM - asigM - seg.MortM - seg.SelM - seg.ErrM);
            }

            var dispH = Math.Max(0, rawH - p.H);
            var dispM = Math.Max(0, rawM - p.M);

            // dispX: lotes con reproductoras (Panama) → mixtas = dispH+dispM (aves en HembrasL/MachosL).
            // Lotes sin reproductoras → usar campo Mixtas explícito o fallback Encaset.
            int dispX;
            if (repro != null)
                dispX = dispH + dispM;
            else if (l.Mixtas > 0)
                dispX = Math.Max(0, l.Mixtas - p.X);
            else if (l.HembrasL + l.MachosL + l.Mixtas == 0 && l.Encaset > 0)
                dispX = Math.Max(0, l.Encaset - p.X);
            else
                dispX = 0;

            items.Add(new AvesDisponiblesLotePorIdDto(
                id,
                new AvesDisponiblesVentaLoteDto(
                    LoteId: id,
                    TipoLote: "LoteAveEngorde",
                    NombreLote: l.LoteNombre,
                    HembrasDisponibles: dispH,
                    MachosDisponibles: dispM,
                    MixtasDisponibles: dispX,
                    TotalDisponibles: dispH + dispM + dispX,
                    HembrasReservadasPendiente: p.H,
                    MachosReservadasPendiente: p.M,
                    MixtasReservadasPendiente: p.X,
                    TotalReservadasPendiente: p.H + p.M + p.X
                )));
        }

        return new AvesDisponiblesLotesResponse { Items = items };
    }
}
