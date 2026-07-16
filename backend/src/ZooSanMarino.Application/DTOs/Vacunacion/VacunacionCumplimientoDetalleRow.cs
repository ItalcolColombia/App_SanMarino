// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionCumplimientoDetalleRow.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Proyección 1:1 de fn_vacunacion_cumplimiento_detalle(...). Mapeada por SqlQueryRaw (snake_case → PascalCase).</summary>
public class VacunacionCumplimientoDetalleRow
{
    public int ItemId { get; set; }
    public int GranjaId { get; set; }
    public string? GranjaNombre { get; set; }
    public int LoteId { get; set; }
    public string? LoteNombre { get; set; }
    public string? LineaProductiva { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public string? VacunaNombre { get; set; }
    public string? UnidadObjetivo { get; set; }
    public int? ValorObjetivo { get; set; }
    public DateTime? FechaObjetivoEfectiva { get; set; }
    public DateTime? FechaInicioFranja { get; set; }
    public DateTime? FechaFinFranja { get; set; }
    public string? Estado { get; set; }
    public DateTime? FechaAplicacion { get; set; }
    public int? DiasDesviacion { get; set; }
    public bool Incumplido { get; set; }
    public string? Motivo { get; set; }
    public string? AplicadoPor { get; set; }
    public string? RegistradoPor { get; set; }
    public string? Notas { get; set; }
}
