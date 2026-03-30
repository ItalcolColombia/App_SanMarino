using System.Text.Json;

/// file: backend/src/ZooSanMarino.Application/DTOs/SeguimientoLoteLevanteDto.cs
namespace ZooSanMarino.Application.DTOs;

public record SeguimientoLoteLevanteDto(
    int Id, int LoteId, int? LotePosturaLevanteId, DateTime FechaRegistro,
    int MortalidadHembras, int MortalidadMachos,
    int SelH, int SelM,
    int ErrorSexajeHembras, int ErrorSexajeMachos,
    double ConsumoKgHembras, string TipoAlimento, string? Observaciones,
    double? KcalAlH, double? ProtAlH, double? KcalAveH, double? ProtAveH, string Ciclo,
    // NUEVOS (mantenidos para compatibilidad con otros servicios)
    double? ConsumoKgMachos, double? PesoPromH, double? PesoPromM,
    double? UniformidadH, double? UniformidadM, double? CvH, double? CvM,
    // Metadata JSONB para campos adicionales/extras (consumo original con unidad, etc.)
    JsonDocument? Metadata,
    // Items adicionales JSONB para otros tipos de ítems (vacunas, medicamentos, etc.)
    // que NO son alimentos. Los alimentos se mantienen en campos tradicionales.
    JsonDocument? ItemsAdicionales,
    // Campos de agua (solo para Ecuador y Panamá)
    // NOTA: Usar double? para coincidir con double precision en PostgreSQL
    double? ConsumoAguaDiario, // Consumo diario de agua en litros
    double? ConsumoAguaPh, // Nivel de PH del agua
    double? ConsumoAguaOrp, // Nivel de ORP (Oxidación-Reducción Potencial) del agua en mV
    double? ConsumoAguaTemperatura, // Temperatura del agua en °C
    string? CreatedByUserId = null, // ID del usuario que crea (para tabla unificada seguimiento_diario)
    double? SaldoAlimentoKg = null // Saldo alimento (kg) al cierre del día; aves de engorde (inventario unificado)
);