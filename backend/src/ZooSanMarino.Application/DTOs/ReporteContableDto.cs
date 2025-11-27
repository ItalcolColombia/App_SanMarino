// src/ZooSanMarino.Application/DTOs/ReporteContableDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para consumo diario en reporte contable
/// </summary>
public record ConsumoDiarioContableDto
{
    public DateTime Fecha { get; init; }
    public int LoteId { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    public decimal ConsumoAlimento { get; init; }
    public decimal ConsumoAgua { get; init; }
    public decimal ConsumoMedicamento { get; init; }
    public decimal ConsumoVacuna { get; init; }
    public decimal OtrosConsumos { get; init; }
    public decimal TotalConsumo { get; init; }
}

/// <summary>
/// DTO para reporte contable semanal
/// </summary>
public record ReporteContableSemanalDto
{
    public int SemanaContable { get; init; }
    public DateTime FechaInicio { get; init; }
    public DateTime FechaFin { get; init; }
    public int LotePadreId { get; init; }
    public string LotePadreNombre { get; init; } = string.Empty;
    public List<string> Sublotes { get; init; } = new();
    public decimal ConsumoTotalAlimento { get; init; }
    public decimal ConsumoTotalAgua { get; init; }
    public decimal ConsumoTotalMedicamento { get; init; }
    public decimal ConsumoTotalVacuna { get; init; }
    public decimal OtrosConsumos { get; init; }
    public decimal TotalGeneral { get; init; }
    public List<ConsumoDiarioContableDto> ConsumosDiarios { get; init; } = new();
}

/// <summary>
/// DTO para reporte contable completo
/// </summary>
public record ReporteContableCompletoDto
{
    public int LotePadreId { get; init; }
    public string LotePadreNombre { get; init; } = string.Empty;
    public int GranjaId { get; init; }
    public string GranjaNombre { get; init; } = string.Empty;
    public string? NucleoId { get; init; }
    public string? NucleoNombre { get; init; }
    public DateTime FechaPrimeraLlegada { get; init; }
    public int SemanaContableActual { get; init; }
    public DateTime FechaInicioSemanaActual { get; init; }
    public DateTime FechaFinSemanaActual { get; init; }
    public List<ReporteContableSemanalDto> ReportesSemanales { get; init; } = new();
}

/// <summary>
/// DTO para solicitar generación de reporte contable
/// </summary>
public record GenerarReporteContableRequestDto
{
    public int LotePadreId { get; init; }
    public int? SemanaContable { get; init; }
    public DateTime? FechaInicio { get; init; }
    public DateTime? FechaFin { get; init; }
}

