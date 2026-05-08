// src/ZooSanMarino.Application/DTOs/ObtenerReporteLevanteRequestDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Parámetros de entrada para ObtenerReporteLevanteAsync.
/// Navega desde lote_postura_base → lotes → lote_postura_levante → seguimiento_diario.
/// </summary>
public class ObtenerReporteLevanteRequestDto
{
    /// <summary>
    /// ID del lote maestro de postura. Raíz obligatoria de la consolidación.
    /// </summary>
    public int LotePosturaBaseId { get; set; }

    /// <summary>
    /// ID del lote levante específico (lote_postura_levante_id).
    /// Si es null, se consolidan todos los lotes levante asociados al LotePosturaBaseId.
    /// </summary>
    public int? LoteLevanteId { get; set; }

    /// <summary>
    /// Granularidad de la salida: "Diario" (por fecha) o "Semanal" (agrupado por semana de levante 1-25).
    /// Por defecto: "Semanal".
    /// </summary>
    public string FiltroPeriodicidad { get; set; } = "Semanal";

    /// <summary>Fecha de inicio del rango a reportar (opcional).</summary>
    public DateTime? FechaInicio { get; set; }

    /// <summary>Fecha de fin del rango a reportar (opcional).</summary>
    public DateTime? FechaFin { get; set; }
}
