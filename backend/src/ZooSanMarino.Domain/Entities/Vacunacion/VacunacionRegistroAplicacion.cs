// src/ZooSanMarino.Domain/Entities/Vacunacion/VacunacionRegistroAplicacion.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Registro de aplicación de un ítem del cronograma (1:1). Lo arma el operador: confirma aplicado
/// (a tiempo/tardío/adelantado) o no aplicado, siempre con <see cref="FechaAplicacion"/> fijada por el
/// servidor al momento de registrar (nunca editable por el usuario).
/// </summary>
public class VacunacionRegistroAplicacion : AuditableEntity
{
    public int Id { get; set; }
    public int? PaisId { get; set; }

    public int VacunacionCronogramaItemId { get; set; }

    /// <summary>"Pendiente" | "Aplicado" | "AplicadoTardio" | "AplicadoAdelantado" | "NoAplicado".</summary>
    public string Estado { get; set; } = "Pendiente";

    /// <summary>Fecha de sistema al confirmar la aplicación. Null mientras está "Pendiente".</summary>
    public DateTime? FechaAplicacion { get; set; }

    /// <summary>Días de desviación respecto al borde más cercano de la franja (positivo = tarde, negativo = adelantado).</summary>
    public int? DiasDesviacion { get; set; }

    /// <summary>True si <see cref="DiasDesviacion"/> supera el umbral configurable de la empresa (VacunacionConfiguracion).</summary>
    public bool Incumplido { get; set; }

    /// <summary>Obligatorio (validado en el handler) si Estado = NoAplicado o si hay desviación fuera de la franja.</summary>
    public string? MotivoDescripcion { get; set; }

    /// <summary>Usuario logueado que hace el registro (siempre obligatorio).</summary>
    public int UsuarioRegistraId { get; set; }

    /// <summary>Responsable de aplicar la vacuna: FK si tiene usuario del sistema...</summary>
    public int? AplicadoPorUserId { get; set; }
    /// <summary>...o nombre libre si no lo tiene. Exactamente uno de los dos poblado.</summary>
    public string? AplicadoPorNombreLibre { get; set; }

    public VacunacionCronogramaItem VacunacionCronogramaItem { get; set; } = null!;
}
