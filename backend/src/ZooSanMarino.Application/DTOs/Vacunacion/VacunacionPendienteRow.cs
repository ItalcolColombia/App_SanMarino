// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionPendienteRow.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Fila cruda de <c>fn_vacunacion_pendientes</c>. Las propiedades se llaman como las columnas de la
/// función porque <c>SqlQueryRaw</c> mapea por nombre (con EFCore.NamingConventions en snake_case);
/// cambiar una acá obliga a cambiar la función y su migración.
/// </summary>
public class VacunacionPendienteRow
{
    public int CronogramaItemId { get; set; }
    public string LineaProductiva { get; set; } = "";
    public int LoteId { get; set; }
    public string LoteNombre { get; set; } = "";
    public int GranjaId { get; set; }
    public string? GranjaNombre { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int ItemInventarioId { get; set; }
    public string ItemInventarioNombre { get; set; } = "";
    public string UnidadObjetivo { get; set; } = "";
    public int? ValorObjetivo { get; set; }
    public DateTime FechaInicioFranja { get; set; }
    public DateTime FechaFinFranja { get; set; }
    public string Situacion { get; set; } = "";
    public int Dias { get; set; }
}
