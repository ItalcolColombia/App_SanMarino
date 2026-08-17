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
    DateTime CreatedAt,
    /// <summary>
    /// Historia (épica) de ItalJira donde se ejecuta este plan. Null = el plan no está enlazado y el
    /// front ofrece el botón para enlazarlo.
    /// </summary>
    long? HistoriaId = null);

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
/// <param name="FirmaImagen">PNG base64 del trazo manuscrito; null si firmó digitando su nombre.</param>
/// <param name="FirmaTipo">"digitada" | "manuscrita" | null (firmas anteriores a la firma manuscrita).</param>
public record ImplementacionFirmaDto(
    int Id,
    int TareaId,
    Guid UserId,
    string Nombre,
    string Cedula,
    string? Email,
    string Estado,
    string? FirmaTexto,
    string? FirmaImagen,
    string? FirmaTipo,
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
    List<ImplementacionFirmaDto> Firmas,
    /// <summary>
    /// Tarea de ItalJira que ejecuta este punto. Null = el punto no se sigue en el tablero y su
    /// estado se maneja a mano desde acá.
    /// </summary>
    long? TicketTareaId = null);

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

/// <summary>
/// Firma del participante actual. <paramref name="FirmaTexto"/> sigue siendo obligatoria (queda como
/// el nombre en claro de quien acepta, y es el camino accesible); <paramref name="FirmaImagen"/> es
/// el trazo manuscrito opcional en data URL PNG (dedo en celular o mouse en escritorio).
/// </summary>
public record ImplementacionFirmarRequest(string FirmaTexto, string? Nota, string? FirmaImagen = null);

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
/// <param name="HabilitadaParaFirmar">
/// El encargado ya dio por terminado el punto (tarea completada/confirmada) y por eso se puede
/// firmar. En false el participante lo ve como "programado": se firma lo realizado, no lo pendiente.
/// </param>
/// <param name="ContenidoCambio">
/// True cuando el punto se editó DESPUÉS de firmado (el hash guardado ya no coincide con el texto
/// actual). No invalida la firma: la marca para que se vea que lo aceptado no es lo que hoy se lee.
/// </param>
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
    string? FirmaImagen,
    string? FirmaTipo,
    string? Nota,
    DateTime? FechaRespuesta,
    bool HabilitadaParaFirmar,
    bool ContenidoCambio);

/// <summary>
/// Usuario que se puede asignar o poner como participante.
///
/// <para>
/// <paramref name="RolIds"/> son sus roles <b>en la empresa activa</b> — no todos los que tenga en el
/// sistema—, y es lo que permite elegir participantes por rol («todos los de Auxiliar de Granja»)
/// sin una segunda consulta ni un endpoint aparte. Un usuario sin roles en esta empresa viene con la
/// lista vacía y sólo se puede elegir a mano.
/// </para>
/// </summary>
public record ImplementacionUsuarioAsignableDto(
    Guid Id, string Nombre, string Cedula, string? Email, List<int> RolIds);

public record ImplementacionRolAsignableDto(int Id, string Nombre);

/// <summary>
/// Resultado de enlazar un plan de implementación con ItalJira.
///
/// <para>
/// Devuelve los conteos separados —lo que ya estaba enlazado y lo que se creó en esta corrida—
/// porque la operación es idempotente y sin ese desglose no se distingue «no hizo falta hacer nada»
/// de «no hizo nada».
/// </para>
/// </summary>
public record ImplementacionItalJiraDto(
    long HistoriaId,
    string HistoriaCodigo,
    bool HistoriaCreada,
    int PuntosYaEnlazados,
    int PuntosEnlazadosAhora);
