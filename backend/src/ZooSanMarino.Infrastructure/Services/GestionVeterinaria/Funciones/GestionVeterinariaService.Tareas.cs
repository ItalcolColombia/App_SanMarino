using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.GestionVeterinaria;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class GestionVeterinariaService
{
    public async Task<IReadOnlyList<TareaCampoDto>> GetTareasCreadasAsync(CancellationToken ct = default)
    {
        var rows = await QueryTareasBase()
            .Where(x => x.CreadaPorUserId == CurrentGuid)
            .OrderBy(x => x.Estado == EstadoTareaCampo.Pendiente ? 0 : 1)
            .ThenBy(x => x.FechaFin)
            .ToListAsync(ct);
        return await MapTareasAsync(rows, ct);
    }

    public async Task<IReadOnlyList<TareaCampoDto>> GetMisTareasAsync(bool incluirCerradas, CancellationToken ct = default)
    {
        var farmIds = await GetAssignedFarmIdsAsync(ct);
        var query = QueryTareasBase().Where(x => farmIds.Contains(x.FarmId));
        if (!incluirCerradas) query = query.Where(x => x.Estado == EstadoTareaCampo.Pendiente);
        var candidatas = await query.OrderBy(x => x.FechaFin).ThenBy(x => x.FechaInicio).ToListAsync(ct);
        var visibles = new List<TareaCampo>();
        foreach (var tarea in candidatas)
            if (await PuedeAccederTareaAsync(tarea, ct)) visibles.Add(tarea);
        return await MapTareasAsync(visibles, ct);
    }

    public async Task<GestionVeterinariaInicioDto> GetInicioAsync(CancellationToken ct = default)
    {
        var todas = await GetMisTareasAsync(false, ct);
        var hoy = DateTime.UtcNow.Date;
        var visibles = todas.Where(x => x.FechaInicio.Date <= hoy.AddDays(7))
            .OrderBy(x => x.EstadoTemporal == GestionVeterinariaCalculos.TemporalVencida ? 0 : 1)
            .ThenBy(x => x.FechaFin)
            .Take(8)
            .ToList();
        return new GestionVeterinariaInicioDto(await GetResumenAsync(ct), visibles);
    }

    public async Task<TareaCampoDto?> GetTareaAsync(long id, CancellationToken ct = default)
    {
        var entity = await QueryTareasBase().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return null;
        if (entity.CreadaPorUserId != CurrentGuid && !await PuedeAccederTareaAsync(entity, ct)) return null;
        return (await MapTareasAsync(new[] { entity }, ct)).Single();
    }

    public async Task<TareaCampoDto> CreateTareaAsync(TareaCampoCreateRequest req, CancellationToken ct = default)
    {
        GestionVeterinariaCalculos.ValidarPeriodo(req.FechaInicio, req.FechaFin);
        var ubicacion = await ValidarUbicacionAsync(req.FarmId, req.NucleoId, req.GalponId, req.LoteId, ct);
        if (req.VisitaId.HasValue)
        {
            var visita = await GetVisitaPropiaAsync(req.VisitaId.Value, ct)
                ?? throw new InvalidOperationException("La visita no existe o no pertenece al usuario actual.");
            if (visita.FarmId != req.FarmId)
                throw new InvalidOperationException("La tarea debe pertenecer a la misma granja de la visita.");
        }
        var entity = new TareaCampo
        {
            CompanyId = _current.CompanyId,
            VisitaId = req.VisitaId,
            FarmId = req.FarmId,
            NucleoId = ubicacion.NucleoId,
            GalponId = ubicacion.GalponId,
            LoteId = ubicacion.LoteId,
            Titulo = GestionVeterinariaCalculos.NormalizarTextoRequerido(req.Titulo, "El título", 200),
            Instrucciones = string.IsNullOrWhiteSpace(req.Instrucciones) ? null : req.Instrucciones.Trim(),
            FechaInicio = req.FechaInicio.Date,
            FechaFin = req.FechaFin.Date,
            RequiereObservacion = req.RequiereObservacion,
            RequiereFoto = req.RequiereFoto,
            Estado = EstadoTareaCampo.Pendiente,
            CreadaPorUserId = CurrentGuid,
            CreatedByUserId = _current.UserId,
            CreatedAt = DateTime.UtcNow
        };
        _ctx.TareasCampo.Add(entity);
        await _ctx.SaveChangesAsync(ct);
        return (await GetTareaAsync(entity.Id, ct))!;
    }

    public async Task<TareaCampoDto?> UpdateTareaAsync(long id, TareaCampoUpdateRequest req, CancellationToken ct = default)
    {
        var entity = await GetTareaPropiaAsync(id, ct);
        if (entity is null) return null;
        if (entity.Estado != EstadoTareaCampo.Pendiente)
            throw new InvalidOperationException("Sólo se puede editar una tarea pendiente.");
        GestionVeterinariaCalculos.ValidarPeriodo(req.FechaInicio, req.FechaFin);
        var ubicacion = await ValidarUbicacionAsync(req.FarmId, req.NucleoId, req.GalponId, req.LoteId, ct);
        entity.FarmId = req.FarmId;
        entity.NucleoId = ubicacion.NucleoId;
        entity.GalponId = ubicacion.GalponId;
        entity.LoteId = ubicacion.LoteId;
        entity.Titulo = GestionVeterinariaCalculos.NormalizarTextoRequerido(req.Titulo, "El título", 200);
        entity.Instrucciones = string.IsNullOrWhiteSpace(req.Instrucciones) ? null : req.Instrucciones.Trim();
        entity.FechaInicio = req.FechaInicio.Date;
        entity.FechaFin = req.FechaFin.Date;
        entity.RequiereObservacion = req.RequiereObservacion;
        entity.RequiereFoto = req.RequiereFoto;
        entity.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        return await GetTareaAsync(id, ct);
    }

    public async Task<TareaCampoDto?> CumplirTareaAsync(long id, CumplirTareaCampoRequest req, CancellationToken ct = default)
    {
        var entity = await QueryTareasBaseTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null || !await PuedeAccederTareaAsync(entity, ct)) return null;
        if (entity.Estado == EstadoTareaCampo.Cancelada)
            throw new InvalidOperationException("La tarea está cancelada.");
        if (entity.Estado == EstadoTareaCampo.Realizada) return await GetTareaAsync(id, ct);

        var evidencias = req.Evidencias?.ToList() ?? new List<TareaCampoEvidenciaInput>();
        var error = GestionVeterinariaCalculos.ValidarCumplimiento(
            entity.RequiereObservacion, entity.RequiereFoto, req.Observacion, evidencias.Count);
        if (error is not null) throw new InvalidOperationException(error);
        foreach (var imagen in evidencias)
        {
            error = GestionVeterinariaCalculos.ValidarImagen(imagen.Base64, imagen.ContentType, imagen.SizeBytes);
            if (error is not null) throw new InvalidOperationException(error);
        }

        await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
        entity.Estado = EstadoTareaCampo.Realizada;
        entity.RealizadaPorUserId = CurrentGuid;
        entity.FechaRealizada = DateTime.UtcNow;
        entity.ObservacionCumplimiento = string.IsNullOrWhiteSpace(req.Observacion) ? null : req.Observacion.Trim();
        entity.UpdatedByUserId = _current.UserId;
        foreach (var imagen in evidencias)
            _ctx.TareaCampoEvidencias.Add(new TareaCampoEvidencia
            {
                TareaId = entity.Id,
                ImagenBase64 = imagen.Base64.Trim(),
                FileName = string.IsNullOrWhiteSpace(imagen.FileName) ? null : imagen.FileName.Trim(),
                ContentType = imagen.ContentType.Trim().ToLowerInvariant(),
                SizeBytes = imagen.SizeBytes,
                CreatedByUserId = CurrentGuid,
                CreatedAt = DateTime.UtcNow
            });
        await _ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await GetTareaAsync(id, ct);
    }

    public async Task<TareaCampoDto?> ReabrirTareaAsync(long id, CancellationToken ct = default)
    {
        var entity = await GetTareaPropiaAsync(id, ct);
        if (entity is null) return null;
        if (entity.Estado != EstadoTareaCampo.Realizada)
            throw new InvalidOperationException("Sólo se puede reabrir una tarea realizada.");
        entity.Estado = EstadoTareaCampo.Pendiente;
        entity.RealizadaPorUserId = null;
        entity.FechaRealizada = null;
        entity.ObservacionCumplimiento = null;
        entity.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        return await GetTareaAsync(id, ct);
    }

    public async Task<TareaCampoDto?> CancelarTareaAsync(long id, CancellationToken ct = default)
    {
        var entity = await GetTareaPropiaAsync(id, ct);
        if (entity is null) return null;
        if (entity.Estado == EstadoTareaCampo.Realizada)
            throw new InvalidOperationException("Una tarea realizada debe reabrirse antes de cancelarla.");
        entity.Estado = EstadoTareaCampo.Cancelada;
        entity.UpdatedByUserId = _current.UserId;
        await _ctx.SaveChangesAsync(ct);
        return await GetTareaAsync(id, ct);
    }

    public async Task<IReadOnlyList<TareaCampoEvidenciaMetaDto>> GetEvidenciasAsync(long tareaId, CancellationToken ct = default)
    {
        var tarea = await QueryTareasBase().FirstOrDefaultAsync(x => x.Id == tareaId, ct);
        if (tarea is null || (tarea.CreadaPorUserId != CurrentGuid && !await PuedeAccederTareaAsync(tarea, ct)))
            return Array.Empty<TareaCampoEvidenciaMetaDto>();
        return await _ctx.TareaCampoEvidencias.AsNoTracking()
            .Where(x => x.TareaId == tareaId).OrderBy(x => x.CreatedAt)
            .Select(x => new TareaCampoEvidenciaMetaDto(
                x.Id, x.FileName, x.ContentType, x.SizeBytes, x.CreatedByUserId, x.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<TareaCampoEvidenciaDto?> GetEvidenciaAsync(long tareaId, long evidenciaId, CancellationToken ct = default)
    {
        var tarea = await QueryTareasBase().FirstOrDefaultAsync(x => x.Id == tareaId, ct);
        if (tarea is null || (tarea.CreadaPorUserId != CurrentGuid && !await PuedeAccederTareaAsync(tarea, ct))) return null;
        return await _ctx.TareaCampoEvidencias.AsNoTracking()
            .Where(x => x.Id == evidenciaId && x.TareaId == tareaId)
            .Select(x => new TareaCampoEvidenciaDto(x.Id, x.ImagenBase64, x.FileName, x.ContentType, x.SizeBytes))
            .FirstOrDefaultAsync(ct);
    }

    private IQueryable<TareaCampo> QueryTareasBase() => _ctx.TareasCampo.AsNoTracking()
        .Include(x => x.Farm).Include(x => x.CreadaPorUser).Include(x => x.RealizadaPorUser).Include(x => x.Evidencias)
        .Where(x => x.CompanyId == _current.CompanyId && x.DeletedAt == null);

    private IQueryable<TareaCampo> QueryTareasBaseTracking() => _ctx.TareasCampo
        .Include(x => x.Farm).Include(x => x.CreadaPorUser).Include(x => x.RealizadaPorUser).Include(x => x.Evidencias)
        .Where(x => x.CompanyId == _current.CompanyId && x.DeletedAt == null);

    private Task<TareaCampo?> GetTareaPropiaAsync(long id, CancellationToken ct) =>
        _ctx.TareasCampo.FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _current.CompanyId
            && x.CreadaPorUserId == CurrentGuid && x.DeletedAt == null, ct);

    private async Task<IReadOnlyList<TareaCampoDto>> MapTareasAsync(IEnumerable<TareaCampo> tareas, CancellationToken ct)
    {
        var rows = tareas.ToList();
        var nucleoIds = rows.Where(x => x.NucleoId != null).Select(x => x.NucleoId!).Distinct().ToList();
        var galponIds = rows.Where(x => x.GalponId != null).Select(x => x.GalponId!).Distinct().ToList();
        var loteIds = rows.Where(x => x.LoteId != null).Select(x => x.LoteId!.Value).Distinct().ToList();
        var nucleos = await _ctx.Nucleos.AsNoTracking().Where(x => nucleoIds.Contains(x.NucleoId))
            .ToDictionaryAsync(x => (x.GranjaId, x.NucleoId), x => x.NucleoNombre, ct);
        var galpones = await _ctx.Galpones.AsNoTracking().Where(x => galponIds.Contains(x.GalponId))
            .ToDictionaryAsync(x => x.GalponId, x => x.GalponNombre, ct);
        var lotes = await _ctx.Lotes.AsNoTracking().Where(x => x.LoteId != null && loteIds.Contains(x.LoteId.Value))
            .ToDictionaryAsync(x => x.LoteId!.Value, x => x.LoteNombre, ct);
        var ahora = DateTime.UtcNow;
        var salida = new List<TareaCampoDto>(rows.Count);
        foreach (var x in rows)
        {
            var puedeCumplir = x.Estado == EstadoTareaCampo.Pendiente && await PuedeAccederTareaAsync(x, ct);
            salida.Add(new TareaCampoDto(
                x.Id, x.VisitaId, x.FarmId, x.Farm.Name,
                x.NucleoId, x.NucleoId is null ? null : nucleos.GetValueOrDefault((x.FarmId, x.NucleoId)),
                x.GalponId, x.GalponId is null ? null : galpones.GetValueOrDefault(x.GalponId),
                x.LoteId, x.LoteId.HasValue ? lotes.GetValueOrDefault(x.LoteId.Value) : null,
                x.Titulo, x.Instrucciones, x.FechaInicio, x.FechaFin, x.RequiereObservacion,
                x.RequiereFoto, x.Estado,
                GestionVeterinariaCalculos.EstadoTemporal(x.FechaInicio, x.FechaFin, x.Estado, ahora),
                x.CreadaPorUserId, NombreUsuario(x.CreadaPorUser), x.RealizadaPorUserId,
                x.RealizadaPorUser is null ? null : NombreUsuario(x.RealizadaPorUser),
                x.FechaRealizada, x.ObservacionCumplimiento, x.Evidencias.Count, puedeCumplir, x.CreatedAt));
        }
        return salida;
    }
}
