// Seguimiento diario Aves de Engorde: misma API que Levante, persiste en seguimiento_diario con tipo = 'engorde'.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface ISeguimientoAvesEngordeService
{
    /// <summary>Incluye seguimientos diarios e historial unificado (una sola respuesta).</summary>
    Task<SeguimientoAvesEngordePorLoteResponseDto> GetByLoteAsync(int loteId);
    Task<SeguimientoLoteLevanteDto?> GetByIdAsync(int id);
    Task<IEnumerable<SeguimientoLoteLevanteDto>> FilterAsync(int? loteId, DateTime? desde, DateTime? hasta);
    Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto);
    Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto);
    Task<bool> DeleteAsync(int id);
    Task<ResultadoLevanteResponse> GetResultadoAsync(int loteId, DateTime? desde, DateTime? hasta, bool recalcular = true);

    /// <summary>
    /// Backfill masivo de metadata (Ingreso/Traslado/Documento/Despacho) para registros existentes,
    /// calculado desde lote_registro_historico_unificado. NO aplica consumos ni movimientos de inventario.
    /// </summary>
    Task<SeguimientoAvesEngordeBackfillResultDto> BackfillMetadataAsync(
        int loteId,
        DateTime? desde,
        DateTime? hasta,
        bool onlyIfMissing = true);

    /// <summary>
    /// Historial unificado (inventario + ventas) para el lote, orden cronológico; excluye anulados.
    /// </summary>
    Task<IEnumerable<LoteRegistroHistoricoUnificadoDto>> GetHistoricoUnificadoPorLoteAsync(int loteId);

    /// <summary>Resumen para modal Liquidar lote (ventas, aves inicio, saldo alimento).</summary>
    Task<LiquidacionLoteEngordeResumenDto?> GetLiquidacionResumenAsync(int loteId);

    /// <summary>
    /// Valida las filas del Excel contra el histórico unificado del lote.
    /// Devuelve inconsistencias y acciones de corrección sugeridas (sin modificar datos).
    /// </summary>
    Task<CuadrarSaldosValidarResponseDto> ValidarCuadrarSaldosAsync(
        int loteId,
        IReadOnlyList<FilaExcelCuadrarSaldosDto> filasExcel);

    /// <summary>
    /// Aplica las acciones de corrección sobre lote_registro_historico_unificado:
    /// ajusta fechas, anula registros sobrantes e inserta los faltantes.
    /// No modifica stocks reales de inventario.
    /// </summary>
    Task<CuadrarSaldosAplicarResponseDto> AplicarCuadrarSaldosAsync(
        int loteId,
        IReadOnlyList<AccionCorreccionCuadrarSaldosDto> acciones,
        IReadOnlyList<FilaExcelCuadrarSaldosDto>? filasExcel = null);
}
