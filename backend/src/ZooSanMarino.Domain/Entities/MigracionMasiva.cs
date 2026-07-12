// src/ZooSanMarino.Domain/Entities/MigracionMasiva.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Registro de auditoría de una corrida de migración masiva por Excel.
/// No participa en la lógica de negocio de los módulos; solo deja rastro de cada carga
/// (quién, qué tipo, cuántas filas, con qué resultado).
/// </summary>
public class MigracionMasiva : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Tipo de migración (valor de <c>TipoMigracion</c> serializado como string).</summary>
    public string Tipo { get; set; } = default!;

    public string NombreArchivo { get; set; } = default!;

    public int FilasTotales { get; set; }
    public int FilasProcesadas { get; set; }
    public int FilasError { get; set; }

    /// <summary>Validado | Procesado | ConErrores | Fallido.</summary>
    public string Estado { get; set; } = default!;

    /// <summary>Errores serializados (jsonb) — lista de <c>MigracionErrorDto</c>.</summary>
    public string? ErroresJson { get; set; }

    public DateTime FechaProceso { get; set; } = DateTime.UtcNow;
}
