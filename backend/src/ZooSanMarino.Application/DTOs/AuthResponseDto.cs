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

    /// <summary>Token JWT generado</summary>
    public string Token { get; set; } = null!;

    /// <summary>Lista de roles del usuario</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>Lista de empresas asociadas</summary>
    public List<string> Empresas { get; set; } = new();

    /// <summary>Lista de permisos asignados por los roles</summary>
    public List<string> Permisos { get; set; } = new();
    
      // NUEVO: ids de menús por rol
    public List<RoleMenusLiteDto> MenusByRole { get; set; } = new();

    // NUEVO: árbol de menú efectivo (filtrado por permisos del usuario)
    public IEnumerable<MenuItemDto> Menu { get; set; } = System.Array.Empty<MenuItemDto>();

}