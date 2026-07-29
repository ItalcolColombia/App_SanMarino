namespace ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;

/// <summary>
/// Contratos de la hoja «RESUMEN SEMANAL» del Informe RA Pesadas.
///
/// Es la contracara del Detalle: mientras el Reporte Técnico Semanal muestra
/// N semanas de UN lote, el Resumen muestra UNA semana calendario de TODOS los
/// lotes (una fila por lote), que es como se lee la operación completa.
///
/// La semana del año usa la convención <b>WEEKNUM de Excel</b> (semanas que
/// arrancan en domingo, la semana 1 contiene el 1-ene), NO la semana ISO —
/// verificado contra el archivo fuente (1825/1825 filas coinciden con WEEKNUM
/// y solo 1736 con ISO). Ver la cabecera de
/// backend/sql/fn_resumen_semanal_ra_pesadas_levante.sql.
/// </summary>
public sealed record ResumenSemanalRaPesadasRequest(
    int Anio,
    int SemanaAnio,
    string Etapa,                       // "levante" | "produccion"
    IReadOnlyList<int>? GranjaIds = null,
    string? Regional = null,
    string? Ciclo = null,               // solo producción
    bool ExcluirTrasladados = false);   // solo levante

// ─────────────────────────────────────────────────────────────────────────────
// LEVANTE — fila cruda de fn_resumen_semanal_ra_pesadas_levante.
// Nombres de propiedades = snake_case de la función, mapeados por EF SqlQueryRaw
// (mismo patrón que ReporteSemanalLevanteExtrasRow).
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ResumenSemanalLevanteRow
{
    public int LoteId { get; set; }
    public string LoteNombre { get; set; } = string.Empty;
    public int GranjaId { get; set; }
    public string? GranjaNombre { get; set; }
    public string? NucleoNombre { get; set; }
    public string? Regional { get; set; }
    public string? Raza { get; set; }
    public int? AnioGuia { get; set; }
    public int EdadSemana { get; set; }
    public DateTime FechaFinSemana { get; set; }
    public int DiasConRegistro { get; set; }
    public bool TuvoTraslado { get; set; }

    /// <summary>Participación = saldo hembras del lote / Σ saldo hembras de la selección.</summary>
    public double? Part { get; set; }
    public double SaldoHembras { get; set; }
    public double SaldoMachos { get; set; }

    public double? MortHembrasPct { get; set; }
    public double? RetiroAcumHembrasPct { get; set; }
    public double? RetiroAcumHembrasGuia { get; set; }
    public double? DifConsumoHembrasPct { get; set; }
    public double? DifPesoHembrasPct { get; set; }
    public double? UniformidadHembras { get; set; }
    public double? CvHembras { get; set; }

    public double? MortMachosPct { get; set; }
    public double? RetiroAcumMachosPct { get; set; }
    public double? RetiroAcumMachosGuia { get; set; }
    public double? DifConsumoMachosPct { get; set; }
    public double? DifPesoMachosPct { get; set; }
    public double? UniformidadMachos { get; set; }
    public double? CvMachos { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// PRODUCCIÓN — fila cruda de fn_resumen_semanal_ra_pesadas_produccion.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ResumenSemanalProduccionRow
{
    public int LotePosturaProduccionId { get; set; }
    public int? LoteId { get; set; }
    public string LoteNombre { get; set; } = string.Empty;
    public int GranjaId { get; set; }
    public string? GranjaNombre { get; set; }
    public string? NucleoNombre { get; set; }
    public string? Regional { get; set; }
    public string? Raza { get; set; }
    public int? AnioGuia { get; set; }
    public string? CicloProduccion { get; set; }
    public string? TipoNido { get; set; }
    public int EdadSemana { get; set; }
    public DateTime FechaFinSemana { get; set; }
    public int DiasConRegistro { get; set; }

    public double? Part { get; set; }
    public double SaldoHembras { get; set; }
    public double SaldoMachos { get; set; }

    public double? ProduccionPct { get; set; }
    public double? ProduccionPctGuia { get; set; }
    public double? DifProduccionPct { get; set; }
    public double? Htaa { get; set; }
    public double? HtaaGuia { get; set; }
    public double? DifHtaa { get; set; }
    public double? Hiaa { get; set; }
    public double? HiaaGuia { get; set; }
    public double? DifHiaa { get; set; }
    public double? AprovSemPct { get; set; }
    public double? AprovSemPctGuia { get; set; }
    public double? DifAprovSemPct { get; set; }
    public double? GrHuevoInc { get; set; }

    public double? MortHembrasPct { get; set; }
    public double? RetiroAcumHembrasPct { get; set; }
    public double? RetiroAcumHembrasGuia { get; set; }
    public double? MortMachosPct { get; set; }
    public double? RetiroAcumMachosPct { get; set; }
    public double? RetiroAcumMachosGuia { get; set; }
    public double? PesoMachoSobreHembra { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Respuestas
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Totales del pie de la hoja. Los conteos SUMAN; los indicadores son
/// PROMEDIO PONDERADO por participación (saldo de hembras), no promedio simple:
/// un lote de 56.000 aves no puede pesar lo mismo que uno de 7.000.
/// </summary>
public sealed class ResumenSemanalTotalesDto
{
    public int Lotes { get; set; }
    public double SaldoHembras { get; set; }
    public double SaldoMachos { get; set; }
    /// <summary>Indicador → promedio ponderado por Part (null si ningún lote aporta valor).</summary>
    public Dictionary<string, double?> Ponderados { get; set; } = new();
}

public sealed record ResumenSemanalRaPesadasLevanteResponse(
    int Anio,
    int SemanaAnio,
    DateTime? FechaInicioSemana,
    DateTime? FechaFinSemana,
    IReadOnlyList<ResumenSemanalLevanteRow> Filas,
    ResumenSemanalTotalesDto Totales);

public sealed record ResumenSemanalRaPesadasProduccionResponse(
    int Anio,
    int SemanaAnio,
    DateTime? FechaInicioSemana,
    DateTime? FechaFinSemana,
    IReadOnlyList<ResumenSemanalProduccionRow> Filas,
    ResumenSemanalTotalesDto Totales);

// ─────────────────────────────────────────────────────────────────────────────
// CURVA CONSOLIDADA — bloque «Resumen a semana N» de las hojas de gráficas
// (Gráf LEV Hembras / Gráf LEV Machos / Gráf Producción).
//
// Tercera granularidad del informe: TODOS los lotes a lo LARGO de todas las
// edades (la curva de la operación contra la guía), frente al Resumen —todos
// los lotes en UNA semana— y al Detalle —un lote en todas sus semanas—.
//
// ⚠️ Los números del archivo original NO son reproducibles: sus hojas de
// gráficas y ALIMLev están filtradas por una selección de lotes guardada en el
// Excel (un slicer) que no se deduce de ninguna columna — comprobado, ningún
// filtro por año, regional, guía ni traslado la reproduce. Acá la selección son
// los filtros que elige el usuario, que es lo que además puede auditar.
// ─────────────────────────────────────────────────────────────────────────────

public sealed record CurvaConsolidadaRequest(
    int Anio,
    string Etapa,                       // "levante" | "produccion"
    IReadOnlyList<int>? GranjaIds = null,
    string? Regional = null,
    string? Ciclo = null,
    bool ExcluirTrasladados = false);

/// <summary>
/// Un punto de la curva: una EDAD con todos los lotes que la transitaron.
/// Los saldos SUMAN; los indicadores son promedio PONDERADO por saldo de
/// hembras (un lote de 56.000 aves no puede pesar igual que uno de 7.000).
/// </summary>
public sealed class CurvaConsolidadaPuntoDto
{
    public int EdadSemana { get; set; }
    public int Lotes { get; set; }
    public double SaldoHembras { get; set; }
    public double SaldoMachos { get; set; }
    /// <summary>Indicador → promedio ponderado (null si ningún lote aporta valor).</summary>
    public Dictionary<string, double?> Indicadores { get; set; } = new();
}

public sealed record CurvaConsolidadaResponse(
    int Anio,
    string Etapa,
    int Lotes,
    IReadOnlyList<CurvaConsolidadaPuntoDto> Puntos);
