namespace ZooSanMarino.Application.DTOs.Tickets;

// ───────────────────────── Entrada ─────────────────────────

/// <summary>Upsert completo del perfil de tickets de un usuario (nivel de apertura + resolutores).</summary>
public record UpsertTicketPerfilRequest(
    /// <summary>Nivel del solicitante: NORMAL | IMPLEMENTADOR</summary>
    string Nivel,
    /// <summary>Lista de perfiles de resolutor que tendrá el usuario.</summary>
    List<ResolutorItemRequest> Resolutores,
    /// <summary>
    /// Empresa donde se guarda. Null = la del usuario (ver <c>TicketPerfilEmpresaCalculos</c>). Otra
    /// empresa que no sea la activa: solo el admin global.
    /// </summary>
    int? CompanyId = null
);

/// <summary>
/// Un perfil de resolutor: tipo + país opcional (null = todos los países de la empresa) + alcance.
/// </summary>
/// <param name="Alcance">
/// EMPRESA | GLOBAL. Null = conservar el de la fila existente (pantallas que no conocen el alcance
/// reenvían lo que cargaron sin pisarlo); si no existe, EMPRESA.
/// </param>
public record ResolutorItemRequest(
    string Tipo,
    int? PaisId,
    string? Alcance = null
);

/// <summary>Upsert de la configuración de tickets de un ROL: qué ATIENDE y qué puede ABRIR.</summary>
public record UpsertTicketResolutorRolRequest(
    List<ResolutorItemRequest> Resolutores,
    /// <summary>
    /// Qué puede ABRIR quien tenga el rol: NORMAL | IMPLEMENTADOR. Null = no tocar; "" = el rol deja de
    /// definirlo (vuelve a NORMAL por defecto).
    /// </summary>
    string? NivelCreacion = null,
    /// <summary>Empresa de la plantilla. Null = la del rol. Otra que no sea la activa: solo el admin global.</summary>
    int? CompanyId = null
);

// ───────────────────────── Salida ─────────────────────────

/// <param name="Nivel">Nivel del perfil PERSONAL (la excepción por persona).</param>
/// <param name="CompanyId">Empresa donde vive este perfil (la del usuario, no la del que edita).</param>
/// <param name="NivelPorRol">Lo que le dan sus roles (NORMAL | IMPLEMENTADOR), o null si ninguno lo define.</param>
/// <param name="PuedeElegirGlobal">¿La sesión puede marcar alcance GLOBAL?</param>
public record TicketPerfilDto(
    Guid UserId,
    string Nivel,
    IReadOnlyList<ResolutorItemDto> Resolutores,
    bool HasProfile = false,
    int CompanyId = 0,
    string? CompanyName = null,
    string? NivelPorRol = null,
    bool PuedeElegirGlobal = false
);

public record ResolutorItemDto(
    long Id,
    string Tipo,
    int? PaisId,
    bool Activo,
    string Alcance = "EMPRESA",
    int CompanyId = 0
);

/// <summary>Usuario asignable para un tipo+país dado (para el select de asignado).</summary>
/// <param name="PaisLabel">
/// Etiqueta que acompaña al nombre: «Global» (atiende todas las empresas) o el nombre de la empresa.
/// Conserva el nombre del campo por compatibilidad; hasta el 19-sep-2026 decía «Global» para toda fila
/// con <c>pais_id NULL</c>.
/// </param>
public record AsignableDto(
    Guid UserId,
    string NombreCompleto,
    string? PaisLabel
);

/// <summary>Tipo permitido con la lista de usuarios asignables disponibles.</summary>
public record TipoPermitidoDto(
    string Tipo,
    string Label,
    IReadOnlyList<AsignableDto> Asignables
);

/// <summary>Configuración de tickets de un rol.</summary>
/// <param name="NivelCreacion">Qué puede ABRIR quien tenga el rol (null = no define ⇒ NORMAL).</param>
/// <param name="CompanyId">Empresa de la plantilla que se muestra.</param>
public record TicketResolutorRolDto(
    int RoleId,
    IReadOnlyList<ResolutorItemDto> Resolutores,
    string? NivelCreacion = null,
    int CompanyId = 0,
    string? CompanyName = null,
    bool PuedeElegirGlobal = false
);
