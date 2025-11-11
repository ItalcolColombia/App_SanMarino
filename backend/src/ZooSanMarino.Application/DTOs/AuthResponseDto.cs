namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Respuesta con token JWT e información del usuario autenticado.
/// </summary>
public class AuthResponseDto
{
    public Guid UserId { get; set; }

    /// <summary>Nombre de usuario</summary>
    public string Username { get; set; } = null!;

    /// <summary>Nombre completo</summary>
    public string FullName { get; set; } = null!;

    /// <summary>Nombre</summary>
    public string? FirstName { get; set; }

    /// <summary>Apellido</summary>
    public string? SurName { get; set; }

    /// <summary>Token JWT generado</summary>
    public string Token { get; set; } = null!;

    /// <summary>Lista de roles del usuario</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>Lista de empresas asociadas (legacy - mantener para compatibilidad)</summary>
    public List<string> Empresas { get; set; } = new();

    /// <summary>Lista de combinaciones empresa-país del usuario</summary>
    public List<CompanyPaisDto> CompanyPaises { get; set; } = new();

    /// <summary>Lista de permisos asignados por los roles</summary>
    public List<string> Permisos { get; set; } = new();
    
      // NUEVO: ids de menús por rol
    public List<RoleMenusLiteDto> MenusByRole { get; set; } = new();

    // NUEVO: árbol de menú efectivo (filtrado por permisos del usuario)
    public IEnumerable<MenuItemDto> Menu { get; set; } = System.Array.Empty<MenuItemDto>();

    /// <summary>Indica si el correo de bienvenida fue enviado (solo para registro)</summary>
    public bool? EmailSent { get; set; }
    
    /// <summary>ID del correo en la cola (para consultar estado)</summary>
    public int? EmailQueueId { get; set; }
}