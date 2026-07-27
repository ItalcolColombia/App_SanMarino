namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Permiso de ubicación DENTRO de una granja asignada a un usuario (una fila = un grant en un nivel).
/// Solo tiene efecto cuando <see cref="UserFarm.RestrictLocations"/> = true; con el flag apagado el
/// usuario conserva acceso global a la granja (comportamiento previo intacto).
/// Exactamente UNO de <see cref="NucleoId"/>, <see cref="GalponId"/>, <see cref="LoteId"/> va no nulo:
///  - Núcleo  ⇒ todos sus galpones y lotes.
///  - Galpón  ⇒ todos sus lotes (el núcleo padre queda visible para navegación).
///  - Lote    ⇒ ese lote de la tabla <c>lotes</c> (galpón/núcleo padres visibles). Los módulos con
///    tablas de lote propias (engorde/postura) se gobiernan por los niveles núcleo/galpón.
/// Las FKs son ON DELETE CASCADE: si el lugar se elimina o se re-keyea (fn_rekey_*), el grant cae y
/// el usuario queda MÁS restringido (fail-closed) hasta que un admin re-asigne.
/// </summary>
public class UserFarmScope
{
    public int Id { get; set; }

    public Guid UserId { get; set; }
    public int  FarmId { get; set; }

    /// <summary>Grant a nivel núcleo (PK compuesta con FarmId contra nucleos).</summary>
    public string? NucleoId { get; set; }

    /// <summary>Grant a nivel galpón (PK global de galpones).</summary>
    public string? GalponId { get; set; }

    /// <summary>Grant a nivel lote (tabla lotes — reproductora levante/producción).</summary>
    public int? LoteId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }

    // Navegación
    public UserFarm UserFarm { get; set; } = null!;
}
