using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ILiquidacionCierreLoteLevanteService
{
    /// <summary>
    /// Calcula las variables de liquidación técnica a la semana 25 sin guardar.
    /// Usa SeguimientoDiario (tipo='levante') y la guía genética del lote.
    /// </summary>
    Task<LiquidacionCierreLoteLevanteDto> CalcularAsync(int lotePosturaLevanteId, CancellationToken ct = default);

    /// <summary>
    /// Guarda la liquidación de cierre en la tabla liquidacion_cierre_lote_levante.
    /// Si ya existe un registro, lo actualiza.
    /// </summary>
    Task<LiquidacionCierreGuardadaDto> GuardarAsync(int lotePosturaLevanteId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene la liquidación guardada de un lote, o null si no existe.
    /// </summary>
    Task<LiquidacionCierreGuardadaDto?> ObtenerPorLoteAsync(int lotePosturaLevanteId, CancellationToken ct = default);
}
