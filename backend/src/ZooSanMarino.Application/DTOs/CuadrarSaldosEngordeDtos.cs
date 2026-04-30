namespace ZooSanMarino.Application.DTOs;

/// <summary>Fila del Excel cargado para cuadrar saldos de alimento en seguimiento engorde.</summary>
public sealed record FilaExcelCuadrarSaldosDto(
    string Fecha,
    decimal? SaldoAlimentoKg,
    decimal? IngresoAlimentoKg,
    decimal? TrasladoEntradaKg,
    decimal? TrasladoSalidaKg,
    string? Documento,
    decimal? ConsumoKg,
    decimal? ConsumoAcumuladoKg);

/// <summary>Inconsistencia detectada entre Excel y sistema al comparar saldos.</summary>
public sealed record InconsistenciaCuadrarSaldosDto(
    /// <summary>Fecha YYYY-MM-DD de la fila con inconsistencia.</summary>
    string Fecha,
    /// <summary>
    /// INGRESO_FALTANTE | INGRESO_SOBRANTE | INGRESO_MONTO_DIFERENTE |
    /// TRASLADO_ENTRADA_DIFERENTE | TRASLADO_SALIDA_DIFERENTE | SALDO_DIFERENTE
    /// </summary>
    string Tipo,
    string Descripcion,
    decimal? ValorExcel,
    decimal? ValorSistema,
    long? HistoricoId,
    string? DocumentoExcel,
    string? DocumentoSistema);

/// <summary>Acción de corrección sugerida por el sistema o confirmada por el usuario.</summary>
public sealed record AccionCorreccionCuadrarSaldosDto(
    /// <summary>AJUSTAR_FECHA | ANULAR | INSERTAR</summary>
    string TipoAccion,
    long? HistoricoId,
    string? NuevaFecha,
    string? FechaInsertar,
    string? TipoEvento,
    decimal? CantidadKg,
    string? Documento,
    string? Descripcion);

public sealed record CuadrarSaldosValidarRequestDto(
    IReadOnlyList<FilaExcelCuadrarSaldosDto> FilasExcel);

public sealed record CuadrarSaldosValidarResponseDto(
    int LoteId,
    int FilasExcel,
    int InconsistenciasCount,
    IReadOnlyList<InconsistenciaCuadrarSaldosDto> Inconsistencias,
    IReadOnlyList<AccionCorreccionCuadrarSaldosDto> AccionesSugeridas);

public sealed record CuadrarSaldosAplicarRequestDto(
    IReadOnlyList<AccionCorreccionCuadrarSaldosDto> Acciones);

public sealed record CuadrarSaldosAplicarResponseDto(
    int LoteId,
    int FechasAjustadas,
    int RegistrosAnulados,
    int RegistrosInsertados,
    string Mensaje);
