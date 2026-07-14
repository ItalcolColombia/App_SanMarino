// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionCumplimientoLoteRow.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Proyección 1:1 de fn_vacunacion_cumplimiento_lote(...). Mapeada por SqlQueryRaw (snake_case → PascalCase).</summary>
public class VacunacionCumplimientoLoteRow
{
    public int LoteId { get; set; }
    public string? LoteNombre { get; set; }
    public string? LineaProductiva { get; set; }
    public int GranjaId { get; set; }
    public string? GranjaNombre { get; set; }
    public int TotalProgramadas { get; set; }
    public int TotalATiempo { get; set; }
    public int TotalTardio1Semana { get; set; }
    public int TotalTardio2MasSemanas { get; set; }
    public int TotalNoAplicado { get; set; }
    public int TotalPendiente { get; set; }
    public decimal? PorcentajeATiempo { get; set; }
    public decimal? PorcentajeTardio { get; set; }
    public decimal? PorcentajeNoAplicado { get; set; }
    public decimal? PromedioDiasAtraso { get; set; }
}
