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
    int? LotePosturaProduccionId = null
);



