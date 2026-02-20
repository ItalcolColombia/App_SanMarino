// src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using PagedResultCommon = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.MovimientoPolloEngordeDto>;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class MovimientoPolloEngordeService : IMovimientoPolloEngordeService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public MovimientoPolloEngordeService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<MovimientoPolloEngordeDto> CreateAsync(CreateMovimientoPolloEngordeDto dto)
    {
        var tieneAveEngordeOrigen = dto.LoteAveEngordeOrigenId.HasValue;
        var tieneReproductoraOrigen = dto.LoteReproductoraAveEngordeOrigenId.HasValue;
        if (tieneAveEngordeOrigen == tieneReproductoraOrigen)
            throw new InvalidOperationException("Debe indicar exactamente un lote de origen: LoteAveEngorde o LoteReproductoraAveEngorde.");

        if (dto.CantidadHembras + dto.CantidadMachos + dto.CantidadMixtas <= 0)
            throw new InvalidOperationException("Las cantidades deben ser mayores a cero.");

        var movimiento = new MovimientoPolloEngorde
        {
            FechaMovimiento = dto.FechaMovimiento,
            TipoMovimiento = dto.TipoMovimiento,
            LoteAveEngordeOrigenId = dto.LoteAveEngordeOrigenId,
            LoteReproductoraAveEngordeOrigenId = dto.LoteReproductoraAveEngordeOrigenId,
            GranjaOrigenId = dto.GranjaOrigenId,
            NucleoOrigenId = dto.NucleoOrigenId,
            GalponOrigenId = dto.GalponOrigenId,
            LoteAveEngordeDestinoId = dto.LoteAveEngordeDestinoId,
            LoteReproductoraAveEngordeDestinoId = dto.LoteReproductoraAveEngordeDestinoId,
            GranjaDestinoId = dto.GranjaDestinoId,
            NucleoDestinoId = dto.NucleoDestinoId,
            GalponDestinoId = dto.GalponDestinoId,
            PlantaDestino = dto.PlantaDestino,
            CantidadHembras = dto.CantidadHembras,
            CantidadMachos = dto.CantidadMachos,
            CantidadMixtas = dto.CantidadMixtas,
            MotivoMovimiento = dto.MotivoMovimiento,
            Descripcion = dto.Descripcion,
            Observaciones = dto.Observaciones,
            Estado = "Pendiente",
            UsuarioMovimientoId = dto.UsuarioMovimientoId > 0 ? dto.UsuarioMovimientoId : _currentUser.UserId,
            NumeroDespacho = dto.NumeroDespacho,
            EdadAves = dto.EdadAves,
            TotalPollosGalpon = dto.TotalPollosGalpon,
            Raza = dto.Raza,
            Placa = dto.Placa,
            HoraSalida = dto.HoraSalida,
            GuiaAgrocalidad = dto.GuiaAgrocalidad,
            Sellos = dto.Sellos,
            Ayuno = dto.Ayuno,
            Conductor = dto.Conductor,
            PesoBruto = dto.PesoBruto,
            PesoTara = dto.PesoTara,
            CompanyId = _currentUser.CompanyId,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.MovimientoPolloEngorde.Add(movimiento);
        await _ctx.SaveChangesAsync();

        movimiento.NumeroMovimiento = $"MPE-{DateTime.UtcNow:yyyyMMdd}-{movimiento.Id:D6}";
        await _ctx.SaveChangesAsync();

        return (await GetByIdAsync(movimiento.Id))!;
    }

    public async Task<MovimientoPolloEngordeDto?> GetByIdAsync(int id)
    {
        var m = await _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Include(x => x.LoteAveEngordeOrigen)
            .Include(x => x.LoteReproductoraAveEngordeOrigen)
            .Include(x => x.LoteAveEngordeDestino)
            .Include(x => x.LoteReproductoraAveEngordeDestino)
            .Include(x => x.GranjaOrigen)
            .Include(x => x.GranjaDestino)
            .Where(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null)
            .FirstOrDefaultAsync();
        if (m == null) return null;
        return ToDto(m);
    }

    private static MovimientoPolloEngordeDto ToDto(MovimientoPolloEngorde m)
    {
        var loteOrigenNombre = m.LoteAveEngordeOrigenId.HasValue
            ? m.LoteAveEngordeOrigen?.LoteNombre
            : m.LoteReproductoraAveEngordeOrigen?.NombreLote;
        var loteDestinoNombre = m.LoteAveEngordeDestinoId.HasValue
            ? m.LoteAveEngordeDestino?.LoteNombre
            : m.LoteReproductoraAveEngordeDestinoId.HasValue ? m.LoteReproductoraAveEngordeDestino?.NombreLote : null;
        return new MovimientoPolloEngordeDto(
            m.Id,
            m.NumeroMovimiento,
            m.FechaMovimiento,
            m.TipoMovimiento,
            m.LoteAveEngordeOrigenId != null ? "AveEngorde" : "ReproductoraAveEngorde",
            m.LoteAveEngordeOrigenId ?? m.LoteReproductoraAveEngordeOrigenId,
            loteOrigenNombre,
            m.LoteAveEngordeDestinoId != null ? "AveEngorde" : (m.LoteReproductoraAveEngordeDestinoId != null ? "ReproductoraAveEngorde" : null),
            m.LoteAveEngordeDestinoId ?? m.LoteReproductoraAveEngordeDestinoId,
            loteDestinoNombre,
            m.GranjaOrigenId,
            m.GranjaOrigen?.Name,
            m.GranjaDestinoId,
            m.GranjaDestino?.Name,
            m.CantidadHembras,
            m.CantidadMachos,
            m.CantidadMixtas,
            m.TotalAves,
            m.Estado,
            m.MotivoMovimiento,
            m.Observaciones,
            m.UsuarioMovimientoId,
            m.UsuarioNombre,
            m.FechaProcesamiento,
            m.FechaCancelacion,
            m.CreatedAt,
            m.NumeroDespacho,
            m.EdadAves,
            m.TotalPollosGalpon,
            m.Raza,
            m.Placa,
            m.HoraSalida,
            m.GuiaAgrocalidad,
            m.Sellos,
            m.Ayuno,
            m.Conductor,
            m.PesoBruto,
            m.PesoTara,
            m.PesoNeto,
            m.PromedioPesoAve
        );
    }

    public async Task<IEnumerable<MovimientoPolloEngordeDto>> GetAllAsync()
    {
        var list = await _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Include(x => x.LoteAveEngordeOrigen)
            .Include(x => x.LoteReproductoraAveEngordeOrigen)
            .Include(x => x.LoteAveEngordeDestino)
            .Include(x => x.LoteReproductoraAveEngordeDestino)
            .Include(x => x.GranjaOrigen)
            .Include(x => x.GranjaDestino)
            .Where(x => x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null)
            .OrderByDescending(x => x.FechaMovimiento)
            .ToListAsync();
        return list.Select(ToDto);
    }

    public async Task<PagedResultCommon> SearchAsync(MovimientoPolloEngordeSearchRequest request)
    {
        var query = _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);

        if (!string.IsNullOrEmpty(request.NumeroMovimiento))
            query = query.Where(x => x.NumeroMovimiento.Contains(request.NumeroMovimiento));
        if (!string.IsNullOrEmpty(request.TipoMovimiento))
            query = query.Where(x => x.TipoMovimiento == request.TipoMovimiento);
        if (!string.IsNullOrEmpty(request.Estado))
            query = query.Where(x => x.Estado == request.Estado);
        if (request.LoteAveEngordeOrigenId.HasValue)
            query = query.Where(x => x.LoteAveEngordeOrigenId == request.LoteAveEngordeOrigenId);
        if (request.LoteReproductoraAveEngordeOrigenId.HasValue)
            query = query.Where(x => x.LoteReproductoraAveEngordeOrigenId == request.LoteReproductoraAveEngordeOrigenId);
        if (request.FechaDesde.HasValue)
            query = query.Where(x => x.FechaMovimiento >= request.FechaDesde.Value);
        if (request.FechaHasta.HasValue)
            query = query.Where(x => x.FechaMovimiento <= request.FechaHasta.Value);

        var total = await query.CountAsync();

        var sortDesc = request.SortDesc;
        query = request.SortBy switch
        {
            "NumeroMovimiento" => sortDesc ? query.OrderByDescending(x => x.NumeroMovimiento) : query.OrderBy(x => x.NumeroMovimiento),
            "Estado" => sortDesc ? query.OrderByDescending(x => x.Estado) : query.OrderBy(x => x.Estado),
            _ => sortDesc ? query.OrderByDescending(x => x.FechaMovimiento) : query.OrderBy(x => x.FechaMovimiento)
        };

        var items = await query
            .Include(x => x.LoteAveEngordeOrigen)
            .Include(x => x.LoteReproductoraAveEngordeOrigen)
            .Include(x => x.LoteAveEngordeDestino)
            .Include(x => x.LoteReproductoraAveEngordeDestino)
            .Include(x => x.GranjaOrigen)
            .Include(x => x.GranjaDestino)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        var dtos = items.Select(ToDto).ToList();

        return new PagedResultCommon
        {
            Page = request.Page,
            PageSize = request.PageSize,
            Total = total,
            Items = dtos
        };
    }

    public async Task<MovimientoPolloEngordeDto?> UpdateAsync(int id, UpdateMovimientoPolloEngordeDto dto)
    {
        var m = await _ctx.MovimientoPolloEngorde
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);
        if (m == null) return null;
        if (m.Estado != "Pendiente")
            throw new InvalidOperationException("Solo se pueden editar movimientos en estado Pendiente.");

        if (dto.FechaMovimiento.HasValue) m.FechaMovimiento = dto.FechaMovimiento.Value;
        if (dto.TipoMovimiento != null) m.TipoMovimiento = dto.TipoMovimiento;
        if (dto.GranjaOrigenId.HasValue) m.GranjaOrigenId = dto.GranjaOrigenId;
        if (dto.NucleoOrigenId != null) m.NucleoOrigenId = dto.NucleoOrigenId;
        if (dto.GalponOrigenId != null) m.GalponOrigenId = dto.GalponOrigenId;
        if (dto.GranjaDestinoId.HasValue) m.GranjaDestinoId = dto.GranjaDestinoId;
        if (dto.NucleoDestinoId != null) m.NucleoDestinoId = dto.NucleoDestinoId;
        if (dto.GalponDestinoId != null) m.GalponDestinoId = dto.GalponDestinoId;
        if (dto.PlantaDestino != null) m.PlantaDestino = dto.PlantaDestino;
        if (dto.CantidadHembras.HasValue) m.CantidadHembras = dto.CantidadHembras.Value;
        if (dto.CantidadMachos.HasValue) m.CantidadMachos = dto.CantidadMachos.Value;
        if (dto.CantidadMixtas.HasValue) m.CantidadMixtas = dto.CantidadMixtas.Value;
        if (dto.MotivoMovimiento != null) m.MotivoMovimiento = dto.MotivoMovimiento;
        if (dto.Observaciones != null) m.Observaciones = dto.Observaciones;
        if (dto.NumeroDespacho != null) m.NumeroDespacho = dto.NumeroDespacho;
        if (dto.EdadAves.HasValue) m.EdadAves = dto.EdadAves;
        if (dto.TotalPollosGalpon.HasValue) m.TotalPollosGalpon = dto.TotalPollosGalpon;
        if (dto.Raza != null) m.Raza = dto.Raza;
        if (dto.Placa != null) m.Placa = dto.Placa;
        if (dto.HoraSalida.HasValue) m.HoraSalida = dto.HoraSalida;
        if (dto.GuiaAgrocalidad != null) m.GuiaAgrocalidad = dto.GuiaAgrocalidad;
        if (dto.Sellos != null) m.Sellos = dto.Sellos;
        if (dto.Ayuno != null) m.Ayuno = dto.Ayuno;
        if (dto.Conductor != null) m.Conductor = dto.Conductor;
        if (dto.PesoBruto.HasValue) m.PesoBruto = dto.PesoBruto;
        if (dto.PesoTara.HasValue) m.PesoTara = dto.PesoTara;
        m.UpdatedByUserId = _currentUser.UserId;
        m.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<bool> CancelAsync(int id, string motivo)
    {
        var m = await _ctx.MovimientoPolloEngorde
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);
        if (m == null) return false;
        if (m.Estado != "Pendiente")
            throw new InvalidOperationException("Solo se pueden cancelar movimientos en estado Pendiente.");
        m.Estado = "Cancelado";
        m.FechaCancelacion = DateTime.UtcNow;
        m.Observaciones = (m.Observaciones ?? "") + " | Cancelado: " + (motivo ?? "");
        m.UpdatedByUserId = _currentUser.UserId;
        m.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return true;
    }

    public async Task<MovimientoPolloEngordeDto?> CompleteAsync(int id)
    {
        var m = await _ctx.MovimientoPolloEngorde
            .Include(x => x.LoteAveEngordeOrigen)
            .Include(x => x.LoteReproductoraAveEngordeOrigen)
            .Include(x => x.LoteAveEngordeDestino)
            .Include(x => x.LoteReproductoraAveEngordeDestino)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);
        if (m == null) return null;
        if (m.Estado != "Pendiente")
            throw new InvalidOperationException("Solo se pueden completar movimientos en estado Pendiente.");

        // Descontar del lote origen
        if (m.LoteAveEngordeOrigenId.HasValue && m.LoteAveEngordeOrigen != null)
        {
            var lote = m.LoteAveEngordeOrigen;
            var h = (lote.HembrasL ?? 0) - m.CantidadHembras;
            var mach = (lote.MachosL ?? 0) - m.CantidadMachos;
            var mix = (lote.Mixtas ?? 0) - m.CantidadMixtas;
            lote.HembrasL = Math.Max(0, h);
            lote.MachosL = Math.Max(0, mach);
            lote.Mixtas = Math.Max(0, mix);
            if ((lote.AvesEncasetadas ?? 0) > 0 && (lote.HembrasL ?? 0) + (lote.MachosL ?? 0) + (lote.Mixtas ?? 0) == 0)
                lote.AvesEncasetadas = 0;
        }
        else if (m.LoteReproductoraAveEngordeOrigenId.HasValue && m.LoteReproductoraAveEngordeOrigen != null)
        {
            var lote = m.LoteReproductoraAveEngordeOrigen;
            var h = (lote.H ?? 0) - m.CantidadHembras;
            var mach = (lote.M ?? 0) - m.CantidadMachos;
            var mix = (lote.Mixtas ?? 0) - m.CantidadMixtas;
            lote.H = Math.Max(0, h);
            lote.M = Math.Max(0, mach);
            lote.Mixtas = Math.Max(0, mix);
        }

        // Sumar al lote destino (si existe)
        if (m.LoteAveEngordeDestinoId.HasValue && m.LoteAveEngordeDestino != null)
        {
            var lote = m.LoteAveEngordeDestino;
            lote.HembrasL = (lote.HembrasL ?? 0) + m.CantidadHembras;
            lote.MachosL = (lote.MachosL ?? 0) + m.CantidadMachos;
            lote.Mixtas = (lote.Mixtas ?? 0) + m.CantidadMixtas;
        }
        else if (m.LoteReproductoraAveEngordeDestinoId.HasValue && m.LoteReproductoraAveEngordeDestino != null)
        {
            var lote = m.LoteReproductoraAveEngordeDestino;
            lote.H = (lote.H ?? 0) + m.CantidadHembras;
            lote.M = (lote.M ?? 0) + m.CantidadMachos;
            lote.Mixtas = (lote.Mixtas ?? 0) + m.CantidadMixtas;
        }

        m.Estado = "Completado";
        m.FechaProcesamiento = DateTime.UtcNow;
        m.UpdatedByUserId = _currentUser.UserId;
        m.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(id);
    }

    public async Task<ResumenAvesLoteDto?> GetResumenAvesLoteAsync(string tipoLote, int loteId)
    {
        var companyId = _currentUser.CompanyId;
        int avesInicioH = 0, avesInicioM = 0, avesInicioX = 0;
        int avesActualesH = 0, avesActualesM = 0, avesActualesX = 0;
        string? nombreLote = null;

        if (tipoLote == "LoteAveEngorde")
        {
            var historial = await _ctx.HistorialLotePolloEngorde
                .AsNoTracking()
                .Where(h => h.CompanyId == companyId && h.TipoLote == "LoteAveEngorde" && h.LoteAveEngordeId == loteId && h.TipoRegistro == "Inicio")
                .OrderBy(h => h.FechaRegistro)
                .FirstOrDefaultAsync();
            if (historial != null)
            {
                avesInicioH = historial.AvesHembras;
                avesInicioM = historial.AvesMachos;
                avesInicioX = historial.AvesMixtas;
            }
            var lote = await _ctx.LoteAveEngorde
                .AsNoTracking()
                .Where(l => l.LoteAveEngordeId == loteId && l.CompanyId == companyId && l.DeletedAt == null)
                .Select(l => new { l.LoteNombre, l.HembrasL, l.MachosL, l.Mixtas, l.AvesEncasetadas })
                .FirstOrDefaultAsync();
            if (lote == null) return null;
            nombreLote = lote.LoteNombre;
            avesActualesH = lote.HembrasL ?? 0;
            avesActualesM = lote.MachosL ?? 0;
            avesActualesX = lote.Mixtas ?? 0;
            if (avesActualesH + avesActualesM + avesActualesX == 0 && (lote.AvesEncasetadas ?? 0) > 0)
                avesActualesX = lote.AvesEncasetadas ?? 0;
        }
        else if (tipoLote == "LoteReproductoraAveEngorde")
        {
            var historial = await _ctx.HistorialLotePolloEngorde
                .AsNoTracking()
                .Where(h => h.CompanyId == companyId && h.TipoLote == "LoteReproductoraAveEngorde" && h.LoteReproductoraAveEngordeId == loteId && h.TipoRegistro == "Inicio")
                .OrderBy(h => h.FechaRegistro)
                .FirstOrDefaultAsync();
            if (historial != null)
            {
                avesInicioH = historial.AvesHembras;
                avesInicioM = historial.AvesMachos;
                avesInicioX = historial.AvesMixtas;
            }
            // Filtrar por compañía: el lote reproductora pertenece a un LoteAveEngorde que tiene CompanyId
            var lote = await (from lrae in _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                             join lae in _ctx.LoteAveEngorde.AsNoTracking() on lrae.LoteAveEngordeId equals lae.LoteAveEngordeId!.Value
                             where lrae.Id == loteId && lae.CompanyId == companyId && lae.DeletedAt == null
                             select new { lrae.NombreLote, lrae.H, lrae.M, lrae.Mixtas })
                .FirstOrDefaultAsync();
            if (lote == null) return null;
            nombreLote = lote.NombreLote;
            avesActualesH = lote.H ?? 0;
            avesActualesM = lote.M ?? 0;
            avesActualesX = lote.Mixtas ?? 0;
        }
        else
            return null;

        var movimientosCompletados = await _ctx.MovimientoPolloEngorde
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DeletedAt == null && x.Estado == "Completado" &&
                (tipoLote == "LoteAveEngorde" && x.LoteAveEngordeOrigenId == loteId || tipoLote == "LoteReproductoraAveEngorde" && x.LoteReproductoraAveEngordeOrigenId == loteId))
            .Select(x => new { x.CantidadHembras, x.CantidadMachos, x.CantidadMixtas, x.TipoMovimiento })
            .ToListAsync();

        var avesSalidasTotal = movimientosCompletados.Sum(x => x.CantidadHembras + x.CantidadMachos + x.CantidadMixtas);
        var avesVendidasTotal = movimientosCompletados.Where(x => x.TipoMovimiento == "Venta").Sum(x => x.CantidadHembras + x.CantidadMachos + x.CantidadMixtas);

        return new ResumenAvesLoteDto(
            TipoLote: tipoLote,
            LoteId: loteId,
            NombreLote: nombreLote,
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
}
