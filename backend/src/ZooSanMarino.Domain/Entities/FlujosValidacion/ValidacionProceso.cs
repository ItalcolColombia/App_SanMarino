namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Catálogo global de procesos de negocio que soportan flujos de validación secuenciales.
/// Solo desarrollo agrega filas aquí (por migración, localizadas por <see cref="Key"/>); una
/// empresa nunca puede "inventar" un proceso sin adaptador backend desplegado.
/// </summary>
public class ValidacionProceso
{
    public int Id { get; set; }

    /// <summary>Clave estable: SEGUIMIENTO_LEVANTE, SEGUIMIENTO_PRODUCCION, etc.</summary>
    public string Key { get; set; } = null!;

    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }

    /// <summary>Clave del <c>IProcesoValidacionAdapter</c> registrado en DI para este proceso.</summary>
    public string AdapterKey { get; set; } = null!;

    /// <summary>Ruta del menú funcional asociado, usada para validar disponibilidad por empresa.</summary>
    public string? MenuRoute { get; set; }

    public bool IsActive { get; set; } = true;
    public int Orden { get; set; }

    public ICollection<ValidacionFlujo> Flujos { get; set; } = new List<ValidacionFlujo>();
}

public static class ValidacionProcesoKeys
{
    public const string SeguimientoLevante = "SEGUIMIENTO_LEVANTE";
    public const string SeguimientoProduccion = "SEGUIMIENTO_PRODUCCION";
}
