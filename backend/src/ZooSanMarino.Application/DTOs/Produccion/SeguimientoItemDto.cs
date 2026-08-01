// src/ZooSanMarino.Application/DTOs/Produccion/SeguimientoItemDto.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

public record SeguimientoItemDto(
    int Id,
    int ProduccionLoteId,
    DateTime FechaRegistro,
    int MortalidadH,
    int MortalidadM,
    int SelH,
    int SelM,
    decimal ConsKgH,
    decimal ConsKgM,
    decimal ConsumoKg, // Mantener para compatibilidad (suma de ConsKgH + ConsKgM)
    int HuevosTotales,
    int HuevosIncubables,
    string TipoAlimento,
    decimal PesoHuevo,
    int Etapa,
    string? Observaciones,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Campos de Clasificadora de Huevos
    int HuevoLimpio,
    int HuevoTratado,
    int HuevoSucio,
    int HuevoDeforme,
    int HuevoBlanco,
    int HuevoDobleYema,
    int HuevoPiso,
    int HuevoPequeno,
    int HuevoRoto,
    int HuevoDesecho,
    int HuevoOtro,
    // Campos de Pesaje Semanal
    decimal? PesoH,
    decimal? PesoM,
    decimal? Uniformidad,
    decimal? CoeficienteVariacion,
    string? ObservacionesPesaje,
    // Campos de agua (solo para Ecuador y Panamá)
    // NOTA: Usar double? para coincidir con double precision en PostgreSQL
    double? ConsumoAguaDiario, // Consumo diario de agua en litros
    double? ConsumoAguaPh, // Nivel de PH del agua
    double? ConsumoAguaOrp, // Nivel de ORP (Oxidación-Reducción Potencial) del agua en mV
    double? ConsumoAguaTemperatura, // Temperatura del agua en °C
    int? LotePosturaProduccionId = null,
    /// <summary>Metadata JSON (itemsHembras, itemsMachos, consumo original, etc.). Se serializa como objeto en la API.</summary>
    object? Metadata = null,
    // ── Campos persistidos D2 (antes el backend los descartaba y la UI repintaba 0/vacío) ──
    int ErrorSexajeHembras = 0,
    int ErrorSexajeMachos = 0,
    double? UniformidadHembras = null,
    double? UniformidadMachos = null,
    double? CvHembras = null,
    double? CvMachos = null,
    string? Ciclo = null,
    // ── Traslado de la fila (la grilla ya tenía columnas para esto y siempre veía 0) ──
    bool EsTraslado = false,
    string? TrasladoDireccion = null,
    int TrasladoIngresoHembras = 0,
    int TrasladoIngresoMachos = 0,
    int TrasladoSalidaHembras = 0,
    int TrasladoSalidaMachos = 0,
    int? LoteDestinoId = null,
    int? GranjaDestinoId = null,
    // ── Derivados de fn_seguimiento_diario_produccion (solo el listado de la grilla los
    //    llena; el GET por id devuelve null) ──
    int? EdadDias = null,
    int? Semana = null,
    int? AvesHInicioDia = null,
    int? AvesMInicioDia = null,
    int? SaldoAvesH = null,
    int? SaldoAvesM = null,
    long? HuevoTotAcum = null,
    long? HuevoIncAcum = null,
    double? PctPosturaDia = null
);



