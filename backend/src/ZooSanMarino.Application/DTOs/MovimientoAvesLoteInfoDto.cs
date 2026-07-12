// src/ZooSanMarino.Application/DTOs/MovimientoAvesLoteInfoDto.cs
// DTOs de apoyo a los endpoints de contexto de lote del módulo de Movimiento de Aves
// (información del lote para movimientos, validación de fecha de seguimiento y último
// número de despacho). La lógica de datos vive en el servicio; el controller solo mapea.
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Información del lote para movimientos: ubicación, etapa, aves iniciales/actuales y
/// datos genéticos. Sustituye el objeto anónimo previo del controller (mismos campos).
/// </summary>
public sealed record InformacionLoteMovimientoDto(
    int? LoteId,
    string? LoteNombre,
    int GranjaId,
    string? GranjaNombre,
    string? NucleoId,
    string? NucleoNombre,
    string? GalponId,
    string? GalponNombre,
    int Etapa,
    string TipoLote,
    int? LotePosturaLevanteId,
    int? LotePosturaProduccionId,
    int HembrasIniciales,
    int MachosIniciales,
    int CantidadHembras,
    int CantidadMachos,
    int CantidadMixtas,
    int TotalAves,
    DateTime? FechaEncasetamiento,
    DateTime? FechaInicioProduccion,
    int DiasDesdeEncasetamiento,
    string? Raza,
    int? AnoTablaGenetica
);

/// <summary>
/// Resultado de validar la existencia de un Seguimiento Diario para un lote/fecha.
/// El controller decide el status code (200 si <see cref="Existe"/>, 422 si no) y arma
/// la respuesta con estos datos, preservando el contrato previo.
/// </summary>
public sealed class ValidacionFechaSeguimientoResultado
{
    /// <summary>Existe registro de seguimiento para el lote en la fecha.</summary>
    public bool Existe { get; init; }

    /// <summary>Fecha normalizada (solo día) usada en la búsqueda.</summary>
    public DateTime FechaNormalizada { get; init; }

    /// <summary>Tipo de lote resuelto: "Levante" | "Produccion".</summary>
    public string TipoLote { get; init; } = null!;

    // Presentes solo cuando Existe == true (el registro encontrado)
    public long? SeguimientoId { get; init; }
    public string? TipoSeguimiento { get; init; }
    public int? LotePosturaLevanteId { get; init; }
    public int? LotePosturaProduccionId { get; init; }
    public DateTime? Fecha { get; init; }
    public int? TrasladoAvesEntrante { get; init; }
    public int? TrasladoAvesSalida { get; init; }
    public int? VentaAvesCantidad { get; init; }
    public string? VentaAvesMotivo { get; init; }
}

/// <summary>
/// Último número de despacho generado y el siguiente sugerido (para Ecuador).
/// </summary>
public sealed record UltimoNumeroDespachoDto(int UltimoId, int SiguienteNumero);
