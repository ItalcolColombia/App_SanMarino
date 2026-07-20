// src/ZooSanMarino.Application/DTOs/Implementacion/ImplementacionDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Plan de implementación con resumen de avance (para lista y cabecera del detalle).</summary>
public record ImplementacionPlanDto(
    int Id,
    int CompanyId,
    int? PaisId,
    string Nombre,
    string? Descripcion,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    string Estado,
    int TotalTareas,
    int TareasCompletadas,
    int TareasConfirmadas,
    decimal PorcentajeAvance,
    decimal PorcentajeConfirmado,
    DateTime CreatedAt);

public record ImplementacionPlanCreateRequest(
    string Nombre,
    string? Descripcion,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    bool UsarPlantilla);

/// <summary>Estado solo admite "cancelado" (cancelación manual) o null; el resto se deriva de las tareas.</summary>
public record ImplementacionPlanUpdateRequest(
    string Nombre,
    string? Descripcion,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    string? Estado);

public record ImplementacionTareaDto(
    int Id,
    int PlanId,
    string Categoria,
    string Titulo,
    string? Descripcion,
    int Orden,
    DateTime? FechaProgramada,
    int? RoleId,
    string? RoleNombre,
    Guid? AsignadoUserId,
    string? AsignadoNombre,
    string Estado,
    bool Vencida,
    DateTime? FechaCompletada,
    string? CompletadaPorNombre,
    DateTime? FechaConfirmada,
    string? ConfirmadaPorNombre,
    string? Observaciones);

public record ImplementacionPlanDetalleDto(
    ImplementacionPlanDto Plan,
    List<ImplementacionTareaDto> Tareas);

public record ImplementacionTareaCreateRequest(
    string Categoria,
    string Titulo,
    string? Descripcion,
    int? Orden,
    DateTime? FechaProgramada,
    int? RoleId,
    Guid? AsignadoUserId);

public record ImplementacionTareaUpdateRequest(
    string Categoria,
    string Titulo,
    string? Descripcion,
    int? Orden,
    DateTime? FechaProgramada,
    int? RoleId,
    Guid? AsignadoUserId);

public record ImplementacionConfirmarRequest(string? Observaciones);

/// <summary>Tarea asignada al usuario actual (vista "Mis tareas"), aplanada con datos del plan.</summary>
public record ImplementacionMiTareaDto(
    int Id,
    int PlanId,
    string PlanNombre,
    string Categoria,
    string Titulo,
    string? Descripcion,
    DateTime? FechaProgramada,
    string Estado,
    bool Vencida,
    DateTime? FechaCompletada,
    string? CompletadaPorNombre,
    DateTime? FechaConfirmada,
    string? Observaciones);

public record ImplementacionUsuarioAsignableDto(Guid Id, string Nombre, string Cedula);

public record ImplementacionRolAsignableDto(int Id, string Nombre);
