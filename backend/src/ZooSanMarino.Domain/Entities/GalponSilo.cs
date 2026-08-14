// src/ZooSanMarino.Domain/Entities/GalponSilo.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Qué silos/bodegas alimentan a un galpón. Relación <b>N:M</b>: un galpón puede recibir de varios
/// silos y <b>un mismo silo puede alimentar a varios galpones</b>.
///
/// <para>
/// Ojo con lo que esta tabla NO es: no es contención. El alimento no «está dentro del galpón»; está
/// en el silo, que es una ubicación de la GRANJA. Esta tabla solo dice qué silos ofrecerle al usuario
/// cuando filtra por un galpón. Por eso el stock jamás se lleva por (galpón, silo): el saldo de un
/// silo compartido quedaría partido en dos y ninguno sería el real.
/// </para>
///
/// <para>
/// ⚠️ Guarda el trío <c>(GranjaId, NucleoId, GalponId)</c>, así que cuando un galpón cambia de núcleo
/// las funciones <c>fn_mover_galpon</c> / <c>fn_rekey_nucleo</c> tienen que reescribir también estas
/// filas — si no, el galpón se queda sin silos que ofrecer.
/// </para>
/// </summary>
public class GalponSilo
{
    public int Id { get; set; }

    /// <summary>Empresa dueña del vínculo (scoping multi-empresa).</summary>
    public int CompanyId { get; set; }

    public int GranjaId { get; set; }
    public string NucleoId { get; set; } = null!;
    public string GalponId { get; set; } = null!;

    /// <summary>Silo o bodega asignado (FK a <c>farm_silos.id</c>). Debe ser de la MISMA granja.</summary>
    public int FarmSiloId { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    // Navegación
    public FarmSilo FarmSilo { get; set; } = null!;
}
