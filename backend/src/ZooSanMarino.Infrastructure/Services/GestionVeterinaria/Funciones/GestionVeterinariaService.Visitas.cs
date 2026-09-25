using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.GestionVeterinaria;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class GestionVeterinariaService
{
    public async Task<IReadOnlyList<VisitaTecnicaDto>> GetVisitasAsync(CancellationToken ct = default)
    {
        var visitas = await _ctx.VisitasTecnicas.AsNoTracking()
            .Include(x => x.Farm).Include(x => x.VeterinarioUser).Include(x => x.Tareas)
            .Where(x => x.CompanyId == _current.CompanyId && x.VeterinarioUserId == CurrentGuid && x.DeletedAt == null)
            .OrderBy(x => x.Estado == EstadoVisitaTecnica.Programada ? 0 : 1)
            .ThenBy(x => x.FechaProgramada)
            .ToListAsync(ct);
        return await MapVisitasAsync(visitas, ct);
    }

    public async Task<VisitaTecnicaDto> CreateVisitaAsync(VisitaTecnicaCreateRequest req, CancellationToken ct = default)
    {
        var ubicacion = await ValidarUbicacionAsync(req.FarmId, req.NucleoId, req.GalponId, req.LoteId, ct);
        var entity = new VisitaTecnica
        {
            CompanyId = _current.CompanyId,
            FarmId = req.FarmId,
            NucleoId = ubicacion.NucleoId,
            GalponId = ubicacion.GalponId,
            LoteId = ubicacion.LoteId,
            Titulo = GestionVeterinariaCalculos.NormalizarTextoRequerido(req.Titulo, "El título", 200),
            Objetivo = string.IsNullOrWhiteSpace(req.Objetivo) ? null : req.Objetivo.Trim(),
            FechaProgramada = req.FechaProgramada,
            Estado = EstadoVisitaTecnica.Programada,
            VeterinarioUserId = CurrentGuid,
            CreatedByUserId = _current.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.VisitasTecnicas.Add(entity);
        await _ctx.SaveChangesAsync(ct);
        return (await GetVisitaDtoAsync(entity.Id, ct))!;
    }

    public async Task<VisitaTecnicaDto?> UpdateVisitaAsync(long id, VisitaTecnicaUpdateRequest req, CancellationToken ct = default)
    {
        var entity = await GetVisitaPropiaAsync(id, ct);
        if (entity is null) return null;
        if (entity.Estado != EstadoVisitaTecnica.Programada)
            throw new InvalidOperationException("Sólo se puede editar una visita programada.");
        var ubicacion = await ValidarUbicacionAsync(req.FarmId, req.NucleoId, req.GalponId, req.LoteId, ct);
        entity.FarmId = req.FarmId;
        entity.NucleoId = ubicacion.NucleoId;
        entity.GalponId = ubicacion.GalponId;
        entity.LoteId = ubicacion.LoteId;
        entity.Titulo = GestionVeterinariaCalculos.NormalizarTextoRequerido(req.Titulo, "El título", 200);
        entity.Objetivo = string.IsNullOrWhiteSpace(req.Objetivo) ? null : req.Objetivo.Trim();
        entity.FechaProgramada = req.FechaProgramada;
        entity.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        return await GetVisitaDtoAsync(id, ct);
    }

    public async Task<VisitaTecnicaDto?> RealizarVisitaAsync(long id, RealizarVisitaRequest req, CancellationToken ct = default)
    {
        var entity = await GetVisitaPropiaAsync(id, ct);
        if (entity is null) return null;
        if (entity.Estado == EstadoVisitaTecnica.Cancelada)
            throw new InvalidOperationException("Una visita cancelada no se puede marcar como realizada.");
        if (entity.Estado == EstadoVisitaTecnica.Realizada) return await GetVisitaDtoAsync(id, ct);
        entity.Estado = EstadoVisitaTecnica.Realizada;
        entity.FechaRealizada = DateTime.UtcNow;
        entity.Observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones.Trim();
        entity.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        return await GetVisitaDtoAsync(id, ct);
    }

    public async Task<VisitaTecnicaDto?> CancelarVisitaAsync(long id, CancellationToken ct = default)
    {
        var entity = await GetVisitaPropiaAsync(id, ct);
        if (entity is null) return null;
        if (entity.Estado == EstadoVisitaTecnica.Realizada)
            throw new InvalidOperationException("Una visita realizada no se puede cancelar.");
        entity.Estado = EstadoVisitaTecnica.Cancelada;
        entity.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        return await GetVisitaDtoAsync(id, ct);
    }

    private Task<VisitaTecnica?> GetVisitaPropiaAsync(long id, CancellationToken ct) =>
        _ctx.VisitasTecnicas.FirstOrDefaultAsync(x => x.Id == id
            && x.CompanyId == _current.CompanyId && x.VeterinarioUserId == CurrentGuid && x.DeletedAt == null, ct);

    private async Task<VisitaTecnicaDto?> GetVisitaDtoAsync(long id, CancellationToken ct)
    {
        var entity = await _ctx.VisitasTecnicas.AsNoTracking()
            .Include(x => x.Farm).Include(x => x.VeterinarioUser).Include(x => x.Tareas)
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _current.CompanyId && x.DeletedAt == null, ct);
        if (entity is null) return null;
        return (await MapVisitasAsync(new[] { entity }, ct)).Single();
    }

    private async Task<IReadOnlyList<VisitaTecnicaDto>> MapVisitasAsync(IEnumerable<VisitaTecnica> visitas, CancellationToken ct)
    {
        var rows = visitas.ToList();
        var nucleoIds = rows.Where(x => x.NucleoId != null).Select(x => x.NucleoId!).Distinct().ToList();
        var galponIds = rows.Where(x => x.GalponId != null).Select(x => x.GalponId!).Distinct().ToList();
        var loteIds = rows.Where(x => x.LoteId != null).Select(x => x.LoteId!.Value).Distinct().ToList();
        var nucleos = await _ctx.Nucleos.AsNoTracking().Where(x => nucleoIds.Contains(x.NucleoId))
            .ToDictionaryAsync(x => (x.GranjaId, x.NucleoId), x => x.NucleoNombre, ct);
        var galpones = await _ctx.Galpones.AsNoTracking().Where(x => galponIds.Contains(x.GalponId))
            .ToDictionaryAsync(x => x.GalponId, x => x.GalponNombre, ct);
        var lotes = await _ctx.Lotes.AsNoTracking().Where(x => x.LoteId != null && loteIds.Contains(x.LoteId.Value))
            .ToDictionaryAsync(x => x.LoteId!.Value, x => x.LoteNombre, ct);
        return rows.Select(x => new VisitaTecnicaDto(
            x.Id, x.FarmId, x.Farm.Name, x.NucleoId,
            x.NucleoId is null ? null : nucleos.GetValueOrDefault((x.FarmId, x.NucleoId)),
            x.GalponId, x.GalponId is null ? null : galpones.GetValueOrDefault(x.GalponId),
            x.LoteId, x.LoteId.HasValue ? lotes.GetValueOrDefault(x.LoteId.Value) : null,
            x.Titulo, x.Objetivo, x.FechaProgramada, x.FechaRealizada, x.Estado, x.Observaciones,
            x.VeterinarioUserId, NombreUsuario(x.VeterinarioUser),
            x.Tareas.Count(t => t.DeletedAt == null),
            x.Tareas.Count(t => t.DeletedAt == null && t.Estado == EstadoTareaCampo.Pendiente),
            x.CreatedAt)).ToList();
    }
}
