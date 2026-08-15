// src/ZooSanMarino.Application/Interfaces/IImplementacionService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Planes de implementación por empresa (cronogramas de entrega con checklist doble-check:
/// completar por el gestor + confirmar por el usuario asignado). Todo scoped a la empresa activa.
/// </summary>
public interface IImplementacionService
{
    // Planes
    Task<List<ImplementacionPlanDto>> GetPlanesAsync(CancellationToken ct = default);
    Task<ImplementacionPlanDetalleDto?> GetPlanDetalleAsync(int planId, CancellationToken ct = default);
    Task<ImplementacionPlanDto> CreatePlanAsync(ImplementacionPlanCreateRequest req, CancellationToken ct = default);
    Task<ImplementacionPlanDto?> UpdatePlanAsync(int planId, ImplementacionPlanUpdateRequest req, CancellationToken ct = default);
    Task<bool> DeletePlanAsync(int planId, CancellationToken ct = default);

    // Tareas del checklist
    Task<ImplementacionTareaDto?> CreateTareaAsync(int planId, ImplementacionTareaCreateRequest req, CancellationToken ct = default);
    Task<ImplementacionTareaDto?> UpdateTareaAsync(int tareaId, ImplementacionTareaUpdateRequest req, CancellationToken ct = default);
    Task<bool> DeleteTareaAsync(int tareaId, CancellationToken ct = default);
    Task<ImplementacionTareaDto?> CompletarTareaAsync(int tareaId, CancellationToken ct = default);
    Task<ImplementacionTareaDto?> ConfirmarTareaAsync(int tareaId, ImplementacionConfirmarRequest req, CancellationToken ct = default);
    Task<ImplementacionTareaDto?> ReabrirTareaAsync(int tareaId, CancellationToken ct = default);

    // Participantes y firmas (asistentes que firman el recibido de cada punto)
    Task<ImplementacionTareaDto?> SetParticipantesAsync(int tareaId, ImplementacionParticipantesRequest req, CancellationToken ct = default);
    Task<ImplementacionMiFirmaDto?> FirmarAsync(int tareaId, ImplementacionFirmarRequest req, CancellationToken ct = default);
    Task<ImplementacionMiFirmaDto?> RechazarAsync(int tareaId, ImplementacionRechazarRequest req, CancellationToken ct = default);
    Task<List<ImplementacionMiFirmaDto>> GetMisFirmasAsync(CancellationToken ct = default);

    /// <summary>
    /// Solo lo que el usuario tiene REALMENTE por firmar hoy: puntos donde es participante, sigue
    /// sin responder y el encargado ya los dio por realizados. Es lo que alimenta el panel de
    /// pendientes del inicio, por eso no arrastra el historial de firmas ya dadas.
    /// </summary>
    Task<List<ImplementacionMiFirmaDto>> GetMisPendientesFirmaAsync(CancellationToken ct = default);

    // Consultas de apoyo
    Task<List<ImplementacionMiTareaDto>> GetMisTareasAsync(CancellationToken ct = default);
    Task<List<ImplementacionUsuarioAsignableDto>> GetUsuariosAsignablesAsync(CancellationToken ct = default);
    Task<List<ImplementacionRolAsignableDto>> GetRolesAsignablesAsync(CancellationToken ct = default);
}
