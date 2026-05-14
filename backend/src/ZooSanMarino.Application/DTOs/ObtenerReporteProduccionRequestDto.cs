// src/ZooSanMarino.Application/DTOs/ObtenerReporteProduccionRequestDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Solicitud para generar reporte técnico de producción navegando desde LotePosturaBase.
/// Si LotePosturaProduccionId está presente, genera reporte individual; si no, consolida todos los LPP del base.
/// </summary>
public record ObtenerReporteProduccionRequestDto(
    /// <summary>ID de lote_postura_base (raíz de la cascada).</summary>
    int LotePosturaBaseId,
    /// <summary>ID de lote_postura_produccion. Si se omite, se consolidan todos los LPP del base.</summary>
    int? LotePosturaProduccionId,
    /// <summary>"Semanal" — agrupa por semana productiva | "Diario" — retorna registros por fecha.</summary>
    string FiltroPeriodicidad,
    DateTime? FechaInicio,
    DateTime? FechaFin
);
