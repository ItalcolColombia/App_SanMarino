namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Un candidato (rol o usuario) habilitado para firmar una etapa. Varios candidatos en la misma
/// etapa funcionan como alternativos: ANY, una sola firma requerida (Fase 1).
/// </summary>
public class ValidacionFlujoAsignado
{
    public int Id { get; set; }
    public int PasoId { get; set; }

    /// <summary>ROL | USUARIO — ver <see cref="TipoAsignadoFlujo"/>.</summary>
    public string Tipo { get; set; } = null!;

    /// <summary>Obligatorio si Tipo = ROL; null si Tipo = USUARIO.</summary>
    public int? RoleId { get; set; }

    /// <summary>Obligatorio si Tipo = USUARIO; null si Tipo = ROL.</summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Solo auditoría: si el usuario se eligió filtrando primero por un rol, ese rol queda acá.
    /// Un cambio de rol posterior NO convierte silenciosamente a otra persona en responsable.
    /// </summary>
    public int? RolFiltroOrigenId { get; set; }

    public ValidacionFlujoPaso Paso { get; set; } = null!;
    public Role? Role { get; set; }
}

public static class TipoAsignadoFlujo
{
    public const string Rol = "ROL";
    public const string Usuario = "USUARIO";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Rol, Usuario };

    public static bool EsValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Todos.Contains(tipo);
}
