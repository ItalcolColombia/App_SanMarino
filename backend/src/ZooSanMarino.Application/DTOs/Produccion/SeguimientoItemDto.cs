// src/ZooSanMarino.Application/DTOs/Produccion/SeguimientoItemDto.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

public record SeguimientoItemDto(
    int Id,
    int ProduccionLoteId,
    DateTime FechaRegistro,
    int MortalidadH,
    int MortalidadM,
    int SelH,
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
    string? ObservacionesPesaje
);



