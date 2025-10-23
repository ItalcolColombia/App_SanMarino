namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Entidad que representa la relación muchos a muchos entre usuarios y granjas.
/// Permite asociar usuarios específicos con granjas específicas para control de acceso granular.
/// </summary>
public class UserFarm
{
    public Guid UserId { get; set; }
    public int FarmId { get; set; }
    
    /// <summary>
    /// Indica si el usuario tiene permisos de administrador en esta granja
    /// </summary>
    public bool IsAdmin { get; set; } = false;
    
    /// <summary>
    /// Indica si esta es la granja principal del usuario
    /// </summary>
    public bool IsDefault { get; set; } = false;
    
    /// <summary>
    /// Fecha de creación de la asociación
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Usuario que creó esta asociación
    /// </summary>
    public Guid CreatedByUserId { get; set; }

    // Navegación
    public User User { get; set; } = null!;
    public Farm Farm { get; set; } = null!;
}
