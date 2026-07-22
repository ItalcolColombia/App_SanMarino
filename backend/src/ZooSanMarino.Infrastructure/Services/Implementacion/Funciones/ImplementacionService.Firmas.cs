// Implementacion/Funciones/ImplementacionService.Firmas.cs
// Participantes de una tarea (asistentes a la capacitación/entrega) y su respuesta:
// firma digitada del recibido (con nota) o novedad (rechazo con motivo → el front guía a ticket).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ImplementacionService
{
    /// <summary>
    /// Sincroniza la lista de participantes de la tarea: agrega los nuevos (pendientes), revive a
    /// los que se habían quitado y quita (soft delete) solo a los que siguen pendientes — una firma
    /// o novedad ya registrada es auditoría y no se puede quitar.
    /// </summary>
    public async Task<ImplementacionTareaDto?> SetParticipantesAsync(int tareaId, ImplementacionParticipantesRequest req, CancellationToken ct = default)
    {
        var tarea = await GetTareaScopedAsync(tareaId, ct);
        if (tarea is null) return null;
        if (tarea.Plan.Estado == ImplementacionCalculos.PlanCancelado)
            throw new InvalidOperationException("El plan está cancelado; reactivalo para gestionar participantes.");

        var userIds = (req.UserIds ?? new List<Guid>()).Distinct().ToList();

        if (userIds.Count > 0)
        {
            var validos = await _ctx.UserCompanies.AsNoTracking()
                .Where(uc => uc.CompanyId == _current.CompanyId && userIds.Contains(uc.UserId))
                .Select(uc => uc.UserId)
                .Distinct()
                .ToListAsync(ct);
            if (userIds.Except(validos).Any())
                throw new InvalidOperationException("Hay usuarios seleccionados que no pertenecen a la empresa activa.");
        }

        // Todas las filas de la tarea (vivas y soft-deleted) para agregar/revivir/quitar sin duplicar.
        var firmas = await _ctx.ImplementacionTareaFirmas
            .Where(f => f.TareaId == tareaId)
            .ToListAsync(ct);

        foreach (var f in firmas.Where(f => f.DeletedAt == null && !userIds.Contains(f.UserId)))
        {
            if (f.Estado != ImplementacionCalculos.FirmaPendiente)
                throw new InvalidOperationException(
                    "No se puede quitar un participante que ya firmó o registró una novedad; su respuesta queda como auditoría.");
            f.DeletedAt = DateTime.UtcNow;
            f.UpdatedByUserId = _current.UserId;
        }

        foreach (var uid in userIds)
        {
            var existente = firmas.FirstOrDefault(f => f.UserId == uid);
            if (existente is null)
            {
                _ctx.ImplementacionTareaFirmas.Add(new ImplementacionTareaFirma
                {
                    TareaId         = tareaId,
                    CompanyId       = _current.CompanyId,
                    UserId          = uid,
                    Estado          = ImplementacionCalculos.FirmaPendiente,
                    CreatedByUserId = _current.UserId
                });
            }
            else if (existente.DeletedAt != null)
            {
                // Revive conservando id (solo pudo quitarse estando pendiente).
                existente.DeletedAt = null;
                existente.UpdatedByUserId = _current.UserId;
            }
        }

        await _ctx.SaveChangesAsync(ct);
        return await ReloadTareaDtoAsync(tareaId, ct);
    }

    /// <summary>
    /// Firma digitada del participante actual: confirma que estuvo/recibió el punto. Vale desde
    /// pendiente o rechazada (se retracta de la novedad). Solo el propio participante (fail-closed).
    /// </summary>
    public async Task<ImplementacionMiFirmaDto?> FirmarAsync(int tareaId, ImplementacionFirmarRequest req, CancellationToken ct = default)
    {
        var firma = await GetFirmaDelUsuarioActualAsync(tareaId, ct);
        if (firma is null) return null;

        if (firma.Tarea.Plan.Estado == ImplementacionCalculos.PlanCancelado)
            throw new InvalidOperationException("El plan está cancelado; no se pueden firmar sus puntos.");
        if (!ImplementacionCalculos.PuedeFirmar(firma.Estado))
            throw new InvalidOperationException("Ya firmaste este punto.");

        firma.FirmaTexto     = ImplementacionCalculos.ValidarFirmaTexto(req.FirmaTexto);
        firma.Nota           = string.IsNullOrWhiteSpace(req.Nota) ? null : req.Nota.Trim();
        firma.Estado         = ImplementacionCalculos.FirmaFirmada;
        firma.FechaRespuesta = DateTime.UtcNow;
        firma.UpdatedByUserId = _current.UserId;

        await _ctx.SaveChangesAsync(ct);
        return await GetMiFirmaDtoAsync(firma.Id, ct);
    }

    /// <summary>
    /// Novedad del participante actual: registra por qué NO firma (motivo obligatorio). El front lo
    /// guía luego a crear un ticket con ese motivo. Una firma ya digitada no se puede rechazar.
    /// </summary>
    public async Task<ImplementacionMiFirmaDto?> RechazarAsync(int tareaId, ImplementacionRechazarRequest req, CancellationToken ct = default)
    {
        var firma = await GetFirmaDelUsuarioActualAsync(tareaId, ct);
        if (firma is null) return null;

        if (firma.Tarea.Plan.Estado == ImplementacionCalculos.PlanCancelado)
            throw new InvalidOperationException("El plan está cancelado; no se pueden registrar novedades.");
        if (!ImplementacionCalculos.PuedeRechazar(firma.Estado))
            throw new InvalidOperationException(firma.Estado == ImplementacionCalculos.FirmaFirmada
                ? "Ya firmaste este punto; no se puede registrar una novedad sobre una firma."
                : "Ya registraste una novedad para este punto.");

        var motivo = (req.Motivo ?? "").Trim();
        if (motivo.Length < 5)
            throw new InvalidOperationException("Contanos el motivo de la novedad (mínimo 5 caracteres).");
        if (motivo.Length > 2000)
            throw new InvalidOperationException("El motivo supera el máximo de 2000 caracteres.");

        firma.Nota           = motivo;
        firma.FirmaTexto     = null;
        firma.Estado         = ImplementacionCalculos.FirmaRechazada;
        firma.FechaRespuesta = DateTime.UtcNow;
        firma.UpdatedByUserId = _current.UserId;

        await _ctx.SaveChangesAsync(ct);
        return await GetMiFirmaDtoAsync(firma.Id, ct);
    }

    /// <summary>
    /// Puntos donde el usuario actual es participante en la empresa activa (pendientes primero,
    /// luego historial), con el detalle de la tarea y el encargado del plan.
    /// </summary>
    public async Task<List<ImplementacionMiFirmaDto>> GetMisFirmasAsync(CancellationToken ct = default)
    {
        var uid = _current.UserGuid;
        if (uid is null) return new List<ImplementacionMiFirmaDto>();

        return await _ctx.ImplementacionTareaFirmas.AsNoTracking()
            .Where(f => f.DeletedAt == null
                        && f.CompanyId == _current.CompanyId
                        && f.UserId == uid
                        && f.Tarea.DeletedAt == null
                        && f.Tarea.Plan.DeletedAt == null
                        && f.Tarea.Plan.Estado != ImplementacionCalculos.PlanCancelado)
            .OrderBy(f => f.Estado == ImplementacionCalculos.FirmaPendiente ? 0 : 1)
            .ThenBy(f => f.Tarea.FechaProgramada == null)
            .ThenBy(f => f.Tarea.FechaProgramada)
            .ThenBy(f => f.Id)
            .Select(MiFirmaProjection)
            .ToListAsync(ct);
    }

    /// <summary>Firma viva del usuario actual para la tarea, scoped a empresa (con tarea+plan cargados).</summary>
    private async Task<ImplementacionTareaFirma?> GetFirmaDelUsuarioActualAsync(int tareaId, CancellationToken ct)
    {
        var uid = _current.UserGuid;
        if (uid is null) return null;

        return await _ctx.ImplementacionTareaFirmas
            .Include(f => f.Tarea).ThenInclude(t => t.Plan)
            .FirstOrDefaultAsync(f => f.TareaId == tareaId
                                      && f.UserId == uid
                                      && f.DeletedAt == null
                                      && f.CompanyId == _current.CompanyId
                                      && f.Tarea.DeletedAt == null
                                      && f.Tarea.Plan.DeletedAt == null, ct);
    }

    private async Task<ImplementacionMiFirmaDto?> GetMiFirmaDtoAsync(int firmaId, CancellationToken ct)
        => await _ctx.ImplementacionTareaFirmas.AsNoTracking()
            .Where(f => f.Id == firmaId)
            .Select(MiFirmaProjection)
            .FirstOrDefaultAsync(ct);

    /// <summary>Proyección SQL de la vista "mi firma" (compartida entre lista y detalle; se traduce en la BD).</summary>
    private static readonly System.Linq.Expressions.Expression<Func<ImplementacionTareaFirma, ImplementacionMiFirmaDto>> MiFirmaProjection =
        f => new ImplementacionMiFirmaDto(
            f.Id, f.TareaId, f.Tarea.PlanId, f.Tarea.Plan.Nombre, f.Tarea.Plan.Tipo,
            f.Tarea.Categoria, f.Tarea.Titulo, f.Tarea.Descripcion,
            f.Tarea.FechaProgramada, f.Tarea.Estado,
            f.Tarea.FechaCompletada,
            f.Tarea.CompletadaPorUser == null ? null : f.Tarea.CompletadaPorUser.firstName + " " + f.Tarea.CompletadaPorUser.surName,
            f.Tarea.Plan.ImplementadorUser == null ? null : f.Tarea.Plan.ImplementadorUser.firstName + " " + f.Tarea.Plan.ImplementadorUser.surName,
            f.Estado, f.FirmaTexto, f.Nota, f.FechaRespuesta);
}
