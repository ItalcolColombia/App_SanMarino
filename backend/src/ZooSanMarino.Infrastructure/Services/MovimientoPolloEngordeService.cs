// src/ZooSanMarino.Infrastructure/Services/MovimientoPolloEngordeService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
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

    /// <summary>Must match observaciones column max length in EF configuration (1000).</summary>
    private const int MaxObservacionesLen = 1000;

    /// <summary>Appends text without exceeding DB column length (keeps suffix when truncating).</summary>
    private static string AppendObservaciones(string? existing, string suffix)
    {
        var combined = (existing ?? "") + suffix;
        if (combined.Length <= MaxObservacionesLen) return combined;
        return combined[^MaxObservacionesLen..];
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

        await RellenarOrigenDesdeLoteOrigenSiFaltaAsync(movimiento, dto);

        _ctx.MovimientoPolloEngorde.Add(movimiento);
        await _ctx.SaveChangesAsync();

        movimiento.NumeroMovimiento = $"MPE-{DateTime.UtcNow:yyyyMMdd}-{movimiento.Id:D6}";
        await _ctx.SaveChangesAsync();

        return (await GetByIdAsync(movimiento.Id))!;
    }

    /// <summary>
    /// Si no viene granja/núcleo/galpón en el DTO, los toma del lote de origen (histórico y flujos que solo envían lote).
    /// </summary>
    private async Task RellenarOrigenDesdeLoteOrigenSiFaltaAsync(MovimientoPolloEngorde m, CreateMovimientoPolloEngordeDto dto)
    {
        if (m.GranjaOrigenId.HasValue)
            return;

        if (dto.LoteAveEngordeOrigenId is { } idAe)
        {
            var lae = await _ctx.LoteAveEngorde.AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.LoteAveEngordeId == idAe && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);
            if (lae != null)
            {
                m.GranjaOrigenId = lae.GranjaId;
                if (string.IsNullOrWhiteSpace(m.NucleoOrigenId)) m.NucleoOrigenId = lae.NucleoId;
                if (string.IsNullOrWhiteSpace(m.GalponOrigenId)) m.GalponOrigenId = lae.GalponId;
            }

            return;
        }

        if (dto.LoteReproductoraAveEngordeOrigenId is { } idRa)
        {
            var lrae = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                .Include(r => r.LoteAveEngorde)
                .FirstOrDefaultAsync(r => r.Id == idRa);
            var lae = lrae?.LoteAveEngorde;
            if (lae != null && lae.CompanyId == _currentUser.CompanyId && lae.DeletedAt == null)
            {
                m.GranjaOrigenId = lae.GranjaId;
                if (string.IsNullOrWhiteSpace(m.NucleoOrigenId)) m.NucleoOrigenId = lae.NucleoId;
                if (string.IsNullOrWhiteSpace(m.GalponOrigenId)) m.GalponOrigenId = lae.GalponId;
            }
        }
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

        if (request.GranjaOrigenId.HasValue)
        {
            var farmId = request.GranjaOrigenId.Value;
            query = query.Where(x =>
                x.GranjaOrigenId == farmId
                || (x.LoteAveEngordeOrigen != null && x.LoteAveEngordeOrigen.GranjaId == farmId)
                || (x.LoteReproductoraAveEngordeOrigen != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde.GranjaId == farmId));
        }

        if (!string.IsNullOrWhiteSpace(request.NucleoOrigenId))
        {
            var nid = request.NucleoOrigenId;
            query = query.Where(x =>
                x.NucleoOrigenId == nid
                || (x.LoteAveEngordeOrigen != null && x.LoteAveEngordeOrigen.NucleoId == nid)
                || (x.LoteReproductoraAveEngordeOrigen != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde.NucleoId == nid));
        }

        if (!string.IsNullOrWhiteSpace(request.GalponOrigenId))
        {
            var gpid = request.GalponOrigenId;
            query = query.Where(x =>
                x.GalponOrigenId == gpid
                || (x.LoteAveEngordeOrigen != null && x.LoteAveEngordeOrigen.GalponId == gpid)
                || (x.LoteReproductoraAveEngordeOrigen != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde != null
                    && x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde.GalponId == gpid));
        }

        if (request.GalponOrigenSinAsignar == true)
        {
            query = query.Where(x =>
                string.IsNullOrEmpty(x.GalponOrigenId)
                && (x.LoteAveEngordeOrigen == null || string.IsNullOrEmpty(x.LoteAveEngordeOrigen.GalponId))
                && (x.LoteReproductoraAveEngordeOrigen == null
                    || x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde == null
                    || string.IsNullOrEmpty(x.LoteReproductoraAveEngordeOrigen.LoteAveEngorde.GalponId)));
        }

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
                .ThenInclude(r => r!.LoteAveEngorde)
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
        m.Observaciones = AppendObservaciones(m.Observaciones, " | Cancelado: " + (motivo ?? ""));
        m.UpdatedByUserId = _currentUser.UserId;
        m.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> EliminarAsync(int id, string? motivo)
    {
        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var m = await _ctx.MovimientoPolloEngorde
                .Include(x => x.LoteAveEngordeOrigen)
                .Include(x => x.LoteReproductoraAveEngordeOrigen)
                .Include(x => x.LoteAveEngordeDestino)
                .Include(x => x.LoteReproductoraAveEngordeDestino)
                .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null);
            if (m == null)
            {
                await tx.RollbackAsync();
                return false;
            }

            if (m.Estado == "Completado")
            {
                await EnsureLotesCargadosParaRevertirAsync(m);
                ValidarLotesPresentesParaRevertir(m);
                RevertirEfectoCompletadoEnLotes(m);
            }

            var nota = string.IsNullOrWhiteSpace(motivo) ? "(sin motivo)" : motivo.Trim();
            m.Estado = "Anulado";
            m.Observaciones = AppendObservaciones(m.Observaciones, " | Eliminado: " + nota);
            m.DeletedAt = DateTime.UtcNow;
            m.UpdatedByUserId = _currentUser.UserId;
            m.UpdatedAt = DateTime.UtcNow;
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }
        catch (DbUpdateException ex)
        {
            await RollbackIfNeededAsync(tx);
            throw MapDbUpdateToInvalidOperation(ex);
        }
        catch (InvalidOperationException)
        {
            await RollbackIfNeededAsync(tx);
            throw;
        }
    }

    private static async Task RollbackIfNeededAsync(IDbContextTransaction tx)
    {
        try
        {
            await tx.RollbackAsync();
        }
        catch
        {
            // Evita enmascarar la excepción original si el rollback falla.
        }
    }

    private static InvalidOperationException MapDbUpdateToInvalidOperation(DbUpdateException ex)
    {
        var detail = ex.InnerException is PostgresException pg
            ? $"{pg.SqlState}: {pg.MessageText} ({pg.ConstraintName ?? ""})"
            : (ex.InnerException?.Message ?? ex.Message);
        return new InvalidOperationException(
            "No se pudo guardar la eliminación del movimiento (reversión de aves en lotes o anulación). " +
            "Revise que el lote destino tenga aves suficientes para deshacer el traslado y que no haya restricciones en base de datos. " +
            "Detalle: " + detail,
            ex);
    }

    /// <summary>
    /// Si el Include no trajo el lote (FK inconsistente o filtro), carga explícita por id para poder revertir cantidades.
    /// </summary>
    private async Task EnsureLotesCargadosParaRevertirAsync(MovimientoPolloEngorde m)
    {
        if (m.LoteAveEngordeOrigenId is { } idOae && m.LoteAveEngordeOrigen == null)
        {
            m.LoteAveEngordeOrigen = await _ctx.LoteAveEngorde
                .FirstOrDefaultAsync(l => l.LoteAveEngordeId == idOae);
        }

        if (m.LoteReproductoraAveEngordeOrigenId is { } idOra && m.LoteReproductoraAveEngordeOrigen == null)
        {
            m.LoteReproductoraAveEngordeOrigen = await _ctx.LoteReproductoraAveEngorde
                .FirstOrDefaultAsync(l => l.Id == idOra);
        }

        if (m.LoteAveEngordeDestinoId is { } idDae && m.LoteAveEngordeDestino == null)
        {
            m.LoteAveEngordeDestino = await _ctx.LoteAveEngorde
                .FirstOrDefaultAsync(l => l.LoteAveEngordeId == idDae);
        }

        if (m.LoteReproductoraAveEngordeDestinoId is { } idDra && m.LoteReproductoraAveEngordeDestino == null)
        {
            m.LoteReproductoraAveEngordeDestino = await _ctx.LoteReproductoraAveEngorde
                .FirstOrDefaultAsync(l => l.Id == idDra);
        }
    }

    /// <summary>Evita guardar con reversión incompleta (FK sin fila en lote).</summary>
    private static void ValidarLotesPresentesParaRevertir(MovimientoPolloEngorde m)
    {
        if (m.LoteAveEngordeOrigenId.HasValue && m.LoteAveEngordeOrigen == null)
            throw new InvalidOperationException(
                "No se puede eliminar: no existe el lote ave engorde de origen en la base de datos. Corrija el movimiento o contacte soporte.");
        if (m.LoteReproductoraAveEngordeOrigenId.HasValue && m.LoteReproductoraAveEngordeOrigen == null)
            throw new InvalidOperationException(
                "No se puede eliminar: no existe el lote reproductora de origen en la base de datos.");
        if (m.LoteAveEngordeDestinoId.HasValue && m.LoteAveEngordeDestino == null)
            throw new InvalidOperationException(
                "No se puede eliminar: no existe el lote ave engorde de destino en la base de datos; no se puede revertir el traslado.");
        if (m.LoteReproductoraAveEngordeDestinoId.HasValue && m.LoteReproductoraAveEngordeDestino == null)
            throw new InvalidOperationException(
                "No se puede eliminar: no existe el lote reproductora de destino en la base de datos.");
    }

    /// <summary>Inverso de <see cref="CompleteAsync"/>: devuelve aves al origen y resta del destino.</summary>
    private static void RevertirEfectoCompletadoEnLotes(MovimientoPolloEngorde m)
    {
        // Destino primero (restar lo que se había sumado)
        if (m.LoteAveEngordeDestinoId.HasValue && m.LoteAveEngordeDestino != null)
        {
            var lote = m.LoteAveEngordeDestino;
            var h = (lote.HembrasL ?? 0) - m.CantidadHembras;
            var mach = (lote.MachosL ?? 0) - m.CantidadMachos;
            var mix = (lote.Mixtas ?? 0) - m.CantidadMixtas;
            if (h < 0 || mach < 0 || mix < 0)
                throw new InvalidOperationException(
                    "No se puede eliminar: el lote destino no tiene suficientes aves para revertir el movimiento (pudo haberse registrado otro movimiento).");
            lote.HembrasL = h;
            lote.MachosL = mach;
            lote.Mixtas = mix;
            if ((lote.AvesEncasetadas ?? 0) > 0 && (lote.HembrasL ?? 0) + (lote.MachosL ?? 0) + (lote.Mixtas ?? 0) == 0)
                lote.AvesEncasetadas = 0;
        }
        else if (m.LoteReproductoraAveEngordeDestinoId.HasValue && m.LoteReproductoraAveEngordeDestino != null)
        {
            var lote = m.LoteReproductoraAveEngordeDestino;
            var h = (lote.H ?? 0) - m.CantidadHembras;
            var mach = (lote.M ?? 0) - m.CantidadMachos;
            var mix = (lote.Mixtas ?? 0) - m.CantidadMixtas;
            if (h < 0 || mach < 0 || mix < 0)
                throw new InvalidOperationException(
                    "No se puede eliminar: el lote destino no tiene suficientes aves para revertir el movimiento.");
            lote.H = h;
            lote.M = mach;
            lote.Mixtas = mix;
        }

        // Origen: sumar de vuelta al inventario
        if (m.LoteAveEngordeOrigenId.HasValue && m.LoteAveEngordeOrigen != null)
        {
            var lote = m.LoteAveEngordeOrigen;
            lote.HembrasL = (lote.HembrasL ?? 0) + m.CantidadHembras;
            lote.MachosL = (lote.MachosL ?? 0) + m.CantidadMachos;
            lote.Mixtas = (lote.Mixtas ?? 0) + m.CantidadMixtas;
        }
        else if (m.LoteReproductoraAveEngordeOrigenId.HasValue && m.LoteReproductoraAveEngordeOrigen != null)
        {
            var lote = m.LoteReproductoraAveEngordeOrigen;
            lote.H = (lote.H ?? 0) + m.CantidadHembras;
            lote.M = (lote.M ?? 0) + m.CantidadMachos;
            lote.Mixtas = (lote.Mixtas ?? 0) + m.CantidadMixtas;
        }
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
    public async Task<VentaGranjaDespachoResultDto> CreateVentaGranjaDespachoAsync(CreateVentaGranjaDespachoDto dto)
    {
        if (dto.Lineas == null || dto.Lineas.Count == 0)
            throw new InvalidOperationException("Debe indicar al menos una línea.");

        var lineas = dto.Lineas
            .Where(l => l.CantidadHembras + l.CantidadMachos + l.CantidadMixtas > 0)
            .ToList();
        if (lineas.Count == 0)
            throw new InvalidOperationException("Debe indicar al menos una línea con cantidades mayores a cero.");

        var idsLote = lineas.Select(l => l.LoteAveEngordeOrigenId).ToList();
        if (idsLote.Count != idsLote.Distinct().Count())
            throw new InvalidOperationException("No puede repetirse el mismo lote en más de una línea.");

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var results = new List<MovimientoPolloEngordeDto>();
            foreach (var linea in lineas)
            {
                var single = new CreateMovimientoPolloEngordeDto
                {
                    FechaMovimiento = dto.FechaMovimiento,
                    TipoMovimiento = dto.TipoMovimiento,
                    LoteAveEngordeOrigenId = linea.LoteAveEngordeOrigenId,
                    LoteReproductoraAveEngordeOrigenId = null,
                    GranjaOrigenId = linea.GranjaOrigenId ?? dto.GranjaOrigenId,
                    NucleoOrigenId = linea.NucleoOrigenId,
                    GalponOrigenId = linea.GalponOrigenId,
                    LoteAveEngordeDestinoId = null,
                    LoteReproductoraAveEngordeDestinoId = null,
                    CantidadHembras = linea.CantidadHembras,
                    CantidadMachos = linea.CantidadMachos,
                    CantidadMixtas = linea.CantidadMixtas,
                    MotivoMovimiento = dto.MotivoMovimiento,
                    Descripcion = dto.Descripcion,
                    Observaciones = dto.Observaciones,
                    UsuarioMovimientoId = dto.UsuarioMovimientoId,
                    NumeroDespacho = dto.NumeroDespacho,
                    EdadAves = dto.EdadAves,
                    TotalPollosGalpon = dto.TotalPollosGalpon.HasValue
                        ? (int?)Math.Round(dto.TotalPollosGalpon.Value)
                        : null,
                    Raza = dto.Raza,
                    Placa = dto.Placa,
                    HoraSalida = dto.HoraSalida,
                    GuiaAgrocalidad = dto.GuiaAgrocalidad,
                    Sellos = dto.Sellos,
                    Ayuno = dto.Ayuno,
                    Conductor = dto.Conductor,
                    PesoBruto = dto.PesoBruto,
                    PesoTara = dto.PesoTara
                };
                var created = await CreateAsync(single);
                results.Add(created);
            }

            await tx.CommitAsync();
            return new VentaGranjaDespachoResultDto { Movimientos = results };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MovimientoPolloEngordeDto>> CompletarBatchAsync(IReadOnlyList<int> movimientoIds)
    {
        if (movimientoIds == null || movimientoIds.Count == 0)
            throw new InvalidOperationException("Debe indicar al menos un movimiento.");

        var ids = movimientoIds.Distinct().ToList();
        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var list = new List<MovimientoPolloEngordeDto>();
            foreach (var id in ids)
            {
                var dto = await CompleteAsync(id);
                if (dto == null)
                    throw new InvalidOperationException($"Movimiento {id} no encontrado.");
                list.Add(dto);
            }

            await tx.CommitAsync();
            return list;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
