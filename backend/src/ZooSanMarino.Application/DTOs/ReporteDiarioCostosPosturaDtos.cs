// src/ZooSanMarino.Application/DTOs/ReporteDiarioCostosPosturaDtos.cs
namespace ZooSanMarino.Application.DTOs.ReporteDiarioCostosPostura;

/// <summary>
/// Filtros del Reporte Diario Área de Costos de POSTURA (levante + producción).
/// Todos opcionales: el alcance mínimo siempre es la empresa efectiva + las granjas
/// asignadas al usuario (lo resuelve el servicio, fail-closed).
/// </summary>
public sealed record ReporteDiarioCostosPosturaRequest(
    /// <summary>NULL = todas las granjas visibles del usuario.</summary>
    int? GranjaId = null,
    /// <summary>Nombre de la regional (opción de master list). NULL/vacío = todas.</summary>
    string? Regional = null,
    /// <summary>Lote base de postura. NULL = todos los lotes del alcance (decisión D2).</summary>
    int? LotePosturaBaseId = null,
    /// <summary>"Levante" | "Produccion" | NULL = ambas (decisión D3).</summary>
    string? Fase = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null
);

/// <summary>
/// Un ítem de alimento del día para un sexo (decisión D4: una entrada por ítem, no por día).
/// <paramref name="Origen"/>: <c>metadata</c> = desglose real del seguimiento;
/// <c>tipo_alimento</c> = fallback de filas viejas sin desglose.
/// </summary>
public sealed record ReporteDiarioCostosPosturaAlimentoDto(
    string Sexo,
    string Nombre,
    double CantidadKg,
    string Origen
);

/// <summary>
/// Huevo del día ya clasificado (decisión D1). <c>Fertil + Comercial + Inservible == Total</c>
/// salvo defecto de datos, que el reporte muestra tal cual (ver <c>ParticionCuadra</c>).
/// </summary>
public sealed record ReporteDiarioCostosPosturaHuevoDto(
    int Fertil,
    int Comercial,
    int Inservible,
    int Total,
    int Venta,
    int TrasladoPlanta
)
{
    /// <summary>True si los tres grupos suman exacto el total (invariante esperado).</summary>
    public bool ParticionCuadra => Fertil + Comercial + Inservible == Total;
}

/// <summary>Una fila del reporte = un día de un lote en una fase.</summary>
public sealed record ReporteDiarioCostosPosturaFilaDto(
    DateTime Fecha,
    string Fase,
    int LoteId,
    string LoteNombre,
    string GalponId,
    string GalponNombre,
    /// <summary>Etiqueta "lote : galpón" que pide el diseño.</summary>
    string LoteGalpon,
    string NucleoId,
    int GranjaId,
    string GranjaNombre,
    string Regional,
    int? LotePosturaBaseId,
    string LoteBaseNombre,
    int? EdadDias,
    int? Semana,
    // Pestaña 1 — Aves
    int MortalidadH,
    int MortalidadM,
    int SeleccionH,
    int SeleccionM,
    int ErrorSexajeH,
    int ErrorSexajeM,
    int VentaAvesH,
    int VentaAvesM,
    // Pestaña 2 — Alimento
    double ConsumoKgH,
    double ConsumoKgM,
    IReadOnlyList<ReporteDiarioCostosPosturaAlimentoDto> Alimentos,
    // Pestaña 3 — Huevos (todo en 0 en filas de levante)
    ReporteDiarioCostosPosturaHuevoDto Huevo
);

/// <summary>Totales de la pestaña de aves (footer).</summary>
public sealed record ReporteDiarioCostosPosturaTotalesAvesDto(
    int MortalidadH, int MortalidadM,
    int SeleccionH, int SeleccionM,
    int ErrorSexajeH, int ErrorSexajeM,
    int VentaAvesH, int VentaAvesM,
    int TotalH, int TotalM, int Total
);

/// <summary>Total consumido de un alimento por sexo en todo el rango.</summary>
public sealed record ReporteDiarioCostosPosturaTotalAlimentoDto(
    string Sexo, string Nombre, double CantidadKg
);

public sealed record ReporteDiarioCostosPosturaTotalesDto(
    ReporteDiarioCostosPosturaTotalesAvesDto Aves,
    double ConsumoKgH,
    double ConsumoKgM,
    double ConsumoKgTotal,
    IReadOnlyList<ReporteDiarioCostosPosturaTotalAlimentoDto> Alimentos,
    ReporteDiarioCostosPosturaHuevoDto Huevo
);

/// <summary>Lote presente en el reporte (cabecera / selector de la vista).</summary>
public sealed record ReporteDiarioCostosPosturaLoteDto(
    int LoteId,
    string LoteNombre,
    string GalponId,
    string GalponNombre,
    string LoteGalpon,
    int GranjaId,
    string GranjaNombre,
    int? LotePosturaBaseId,
    string LoteBaseNombre
);

public sealed record ReporteDiarioCostosPosturaReporteDto(
    ReporteDiarioCostosPosturaRequest FiltrosAplicados,
    DateTime? FechaDesdeEfectiva,
    DateTime? FechaHastaEfectiva,
    /// <summary>Fases realmente presentes en el resultado ("Levante" y/o "Produccion").</summary>
    IReadOnlyList<string> Fases,
    IReadOnlyList<ReporteDiarioCostosPosturaLoteDto> Lotes,
    IReadOnlyList<ReporteDiarioCostosPosturaFilaDto> Filas,
    ReporteDiarioCostosPosturaTotalesDto Totales
);

/// <summary>
/// Fila cruda de <c>fn_reporte_diario_costos_postura</c> (SqlQueryRaw). Props en PascalCase
/// que mapean 1:1 a las columnas snake_case de la función. <c>Alimentos</c> llega como JSON (text).
/// Las 11 categorías de huevo llegan CRUDAS: la clasificación la hace
/// <c>ReporteDiarioCostosPosturaCalculos.ClasificarHuevo</c> (único dueño de esa fórmula).
/// </summary>
public sealed class ReporteDiarioCostosPosturaRow
{
    public DateTime Fecha { get; set; }
    public string Fase { get; set; } = string.Empty;
    public int LoteId { get; set; }
    public string? LoteNombre { get; set; }
    public string? GalponId { get; set; }
    public string? GalponNombre { get; set; }
    public string? NucleoId { get; set; }
    public int GranjaId { get; set; }
    public string? GranjaNombre { get; set; }
    public string? Regional { get; set; }
    public int? LotePosturaBaseId { get; set; }
    public string? LoteBaseNombre { get; set; }
    public int? EdadDias { get; set; }
    public int? Semana { get; set; }

    public int MortalidadH { get; set; }
    public int MortalidadM { get; set; }
    public int SeleccionH { get; set; }
    public int SeleccionM { get; set; }
    public int ErrorSexajeH { get; set; }
    public int ErrorSexajeM { get; set; }
    public int VentaAvesH { get; set; }
    public int VentaAvesM { get; set; }

    public double ConsumoKgH { get; set; }
    public double ConsumoKgM { get; set; }
    public string? Alimentos { get; set; }

    public int HuevoTot { get; set; }
    public int HuevoInc { get; set; }
    public int HuevoLimpio { get; set; }
    public int HuevoTratado { get; set; }
    public int HuevoSucio { get; set; }
    public int HuevoDeforme { get; set; }
    public int HuevoBlanco { get; set; }
    public int HuevoDobleYema { get; set; }
    public int HuevoPiso { get; set; }
    public int HuevoPequeno { get; set; }
    public int HuevoRoto { get; set; }
    public int HuevoDesecho { get; set; }
    public int HuevoOtro { get; set; }
    public int HuevoVenta { get; set; }
    public int HuevoTrasladoPlanta { get; set; }
}

/// <summary>
/// Las 11 categorías crudas de huevo de un día. Entrada de
/// <c>ReporteDiarioCostosPosturaCalculos.ClasificarHuevo</c>; sin dependencias de EF.
/// </summary>
public readonly record struct HuevoCrudo(
    int Tot,
    int Inc,
    int Limpio,
    int Tratado,
    int Sucio,
    int Deforme,
    int Blanco,
    int DobleYema,
    int Piso,
    int Pequeno,
    int Roto,
    int Desecho,
    int Otro
);
