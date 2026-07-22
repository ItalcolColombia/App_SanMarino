// src/ZooSanMarino.Application/DTOs/Implementacion/ImplementacionDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Plan (cronograma) de implementación/capacitación con resumen de avance, encargado y creador
/// (nombre + correo tomados de la aplicación) para lista y cabecera del detalle.
/// </summary>
public record ImplementacionPlanDto(
    int Id,
    int CompanyId,
    int? PaisId,
    string Nombre,
    string? Descripcion,
    string Tipo,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    string Estado,
    Guid? ImplementadorUserId,
    string? ImplementadorNombre,
    string? ImplementadorEmail,
    Guid? CreadoPorUserGuid,
    string? CreadoPorNombre,
    string? CreadoPorEmail,
    int TotalTareas,
    int TareasCompletadas,
    int TareasConfirmadas,
    decimal PorcentajeAvance,
    decimal PorcentajeConfirmado,
    DateTime CreatedAt);

/// <summary>ImplementadorUserId null → el encargado queda el creador (mismo usuario).</summary>
public record ImplementacionPlanCreateRequest(
    string Nombre,
    string? Descripcion,
    string? Tipo,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    Guid? ImplementadorUserId,
    bool UsarPlantilla);

/// <summary>Estado solo admite "cancelado" (cancelación manual) o null; el resto se deriva de las tareas.</summary>
public record ImplementacionPlanUpdateRequest(
    string Nombre,
    string? Descripcion,
    string? Tipo,
    DateTime? FechaInicio,
    DateTime? FechaFin,
    Guid? ImplementadorUserId,
    string? Estado);

/// <summary>Firma/participación de un asistente en una tarea (quién estuvo, si firmó el recibido o registró novedad).</summary>
public record ImplementacionFirmaDto(
    int Id,
    int TareaId,
    Guid UserId,
    string Nombre,
    string Cedula,
    string? Email,
    string Estado,
    string? FirmaTexto,
    string? Nota,
    DateTime? FechaRespuesta);

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
    string? Observaciones,
    List<ImplementacionFirmaDto> Firmas);

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

/// <summary>Lista completa de participantes de la tarea (sincroniza: agrega nuevos, quita pendientes).</summary>
public record ImplementacionParticipantesRequest(List<Guid> UserIds);

/// <summary>Firma digitada del participante actual (+ observación opcional).</summary>
public record ImplementacionFirmarRequest(string FirmaTexto, string? Nota);

/// <summary>Novedad del participante actual: motivo de por qué no firma (obligatorio).</summary>
public record ImplementacionRechazarRequest(string Motivo);

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

/// <summary>
/// Punto donde el usuario actual es participante (vista "Por firmar" de Mis tareas): detalle de la
/// tarea (qué se realizó, cuándo, quién la completó y quién es el encargado) + su propia firma.
/// </summary>
public record ImplementacionMiFirmaDto(
    int FirmaId,
    int TareaId,
    int PlanId,
    string PlanNombre,
    string PlanTipo,
    string Categoria,
    string TareaTitulo,
    string? TareaDescripcion,
    DateTime? FechaProgramada,
    string TareaEstado,
    DateTime? FechaCompletada,
    string? CompletadaPorNombre,
    string? ImplementadorNombre,
    string MiEstado,
    string? FirmaTexto,
    string? Nota,
    DateTime? FechaRespuesta);

public record ImplementacionUsuarioAsignableDto(Guid Id, string Nombre, string Cedula, string? Email);

public record ImplementacionRolAsignableDto(int Id, string Nombre);
