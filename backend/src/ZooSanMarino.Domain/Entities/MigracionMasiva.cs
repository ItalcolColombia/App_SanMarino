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

    /// <summary>Filas no procesadas por idempotencia (ya existían en BD); no son error (F2).</summary>
    public int FilasOmitidas { get; set; }

    /// <summary>Duración total de la corrida en milisegundos (F2).</summary>
    public long? DuracionMs { get; set; }

    /// <summary>Si la corrida fue una validación (dry-run, /validar) en vez de una importación real (F2).</summary>
    public bool FueDryRun { get; set; }

    /// <summary>Validado | Procesado | ProcesadoParcial | ConErrores | Fallido.</summary>
    public string Estado { get; set; } = default!;

    /// <summary>Errores serializados (jsonb) — lista de <c>MigracionErrorDto</c>.</summary>
    public string? ErroresJson { get; set; }

    /// <summary>
    /// Detalle completo de la corrida serializado (jsonb), específico de cada tipo de migración.
    /// Hoy lo usa el puente Panamá (Tipo="SincronizacionPanamaEngorde": ResultadoSincronizacionDto podado)
    /// para reconstruir en el historial los mismos contadores/mensajes de la previsualización.
    /// Null en los tipos que no lo persisten (contrato del historial genérico intacto).
    /// </summary>
    public string? DetalleJson { get; set; }

    public DateTime FechaProceso { get; set; } = DateTime.UtcNow;
}
