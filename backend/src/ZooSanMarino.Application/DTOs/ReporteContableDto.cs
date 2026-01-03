// src/ZooSanMarino.Application/DTOs/ReporteContableDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para datos diarios completos en reporte contable
/// </summary>
public record DatoDiarioContableDto
{
    public DateTime Fecha { get; init; }
    public int LoteId { get; init; }
    public string LoteNombre { get; init; } = string.Empty;
    
    // AVES
    public int EntradasHembras { get; init; }
    public int EntradasMachos { get; init; }
    public int MortalidadHembras { get; init; }
    public int MortalidadMachos { get; init; }
    public int SeleccionHembras { get; init; }
    public int SeleccionMachos { get; init; }
    public int VentasHembras { get; init; }
    public int VentasMachos { get; init; }
    public int TrasladosHembras { get; init; }
    public int TrasladosMachos { get; init; }
    public int SaldoHembras { get; init; }
    public int SaldoMachos { get; init; }
    
    // CONSUMO (Kg)
    public decimal ConsumoAlimentoHembras { get; init; }
    public decimal ConsumoAlimentoMachos { get; init; }
    public decimal ConsumoAgua { get; init; }
    public decimal ConsumoMedicamento { get; init; }
    public decimal ConsumoVacuna { get; init; }
    
    // BULTO
    public decimal SaldoBultosAnterior { get; init; }
    public decimal TrasladosBultos { get; init; }
    public decimal EntradasBultos { get; init; }
    public decimal RetirosBultos { get; init; }
    public decimal ConsumoBultosHembras { get; init; }
    public decimal ConsumoBultosMachos { get; init; }
    public decimal SaldoBultos { get; init; }
}

/// <summary>
/// DTO para consumo diario en reporte contable (mantener compatibilidad)
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
    // Información de semana
    public int SemanaContable { get; init; }
    public DateTime FechaInicio { get; init; }
    public DateTime FechaFin { get; init; }
    public int LotePadreId { get; init; }
    public string LotePadreNombre { get; init; } = string.Empty;
    public List<string> Sublotes { get; init; } = new();
    
    // AVES - Saldo Semana Anterior
    public int SaldoAnteriorHembras { get; init; }
    public int SaldoAnteriorMachos { get; init; }
    
    // AVES - Entradas
    public int EntradasHembras { get; init; }
    public int EntradasMachos { get; init; }
    public int TotalEntradas { get; init; }
    
    // AVES - Mortalidad
    public int MortalidadHembrasSemanal { get; init; }
    public int MortalidadMachosSemanal { get; init; }
    public int MortalidadTotalSemanal { get; init; }
    
    // AVES - Selección
    public int SeleccionHembrasSemanal { get; init; }
    public int SeleccionMachosSemanal { get; init; }
    public int TotalSeleccionSemanal { get; init; }
    
    // AVES - Ventas y Traslados
    public int VentasHembrasSemanal { get; init; }
    public int VentasMachosSemanal { get; init; }
    public int TrasladosHembrasSemanal { get; init; }
    public int TrasladosMachosSemanal { get; init; }
    public int TotalVentasSemanal { get; init; }
    public int TotalTrasladosSemanal { get; init; }
    
    // AVES - Saldo Final
    public int SaldoFinHembras { get; init; }
    public int SaldoFinMachos { get; init; }
    public int TotalAvesVivas { get; init; }
    
    // BULTO - Resumen Semanal
    public decimal SaldoBultosAnterior { get; init; }
    public decimal TrasladosBultosSemanal { get; init; }
    public decimal EntradasBultosSemanal { get; init; }
    public decimal RetirosBultosSemanal { get; init; }
    public decimal ConsumoBultosHembrasSemanal { get; init; }
    public decimal ConsumoBultosMachosSemanal { get; init; }
    public decimal SaldoBultosFinal { get; init; }
    
    // CONSUMO (Kg) - Resumen Semanal
    public decimal ConsumoTotalAlimento { get; init; }
    public decimal ConsumoTotalAgua { get; init; }
    public decimal ConsumoTotalMedicamento { get; init; }
    public decimal ConsumoTotalVacuna { get; init; }
    public decimal OtrosConsumos { get; init; }
    public decimal TotalGeneral { get; init; }
    
    // Detalle diario
    public List<DatoDiarioContableDto> DatosDiarios { get; init; } = new();
    
    // Mantener compatibilidad con versión anterior
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
    public string? GalponId { get; init; }
    public string? GalponNombre { get; init; }
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

