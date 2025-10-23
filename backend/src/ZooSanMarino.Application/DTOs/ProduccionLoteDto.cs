using System;

namespace ZooSanMarino.Application.DTOs
{
    public record ProduccionLoteDto(
        int Id,
        int LoteId,
        DateTime FechaInicioProduccion,
        int HembrasIniciales,
        int MachosIniciales,
        int HuevosIniciales,
        string TipoNido,
        string NucleoProduccionId, // es string según tu modelo
        int GranjaId,
        string Ciclo
    );

    public record CreateProduccionLoteDto(
        int LoteId,
        DateTime FechaInicioProduccion,
        int HembrasIniciales,
        int MachosIniciales,
        int HuevosIniciales,
        string TipoNido,
        string NucleoProduccionId,
        int GranjaId,
        string Ciclo
    );

    public record UpdateProduccionLoteDto(
        int Id,
        int LoteId,
        DateTime FechaInicioProduccion,
        int HembrasIniciales,
        int MachosIniciales,
        int HuevosIniciales,
        string TipoNido,
        string NucleoProduccionId,
        int GranjaId,
        string Ciclo
    );

    public record FilterProduccionLoteDto(
        int? LoteId,
        DateTime? Desde,
        DateTime? Hasta
    );

    // ==================== DTOs PARA SEGUIMIENTO DIARIO DE PRODUCCIÓN ====================
    
    public record SeguimientoProduccionDto(
        int Id,
        DateTime Fecha,
        int LoteId,
        int MortalidadH,
        int MortalidadM,
        int SelH,
        decimal ConsKgH,
        decimal ConsKgM,
        int HuevoTot,
        int HuevoInc,
        string TipoAlimento,
        string Observaciones,
        decimal PesoHuevo,
        int Etapa
    );

    public record CreateSeguimientoProduccionDto(
        DateTime Fecha,
        int LoteId,
        int MortalidadH,
        int MortalidadM,
        int SelH,
        decimal ConsKgH,
        decimal ConsKgM,
        int HuevoTot,
        int HuevoInc,
        string TipoAlimento,
        string Observaciones,
        decimal PesoHuevo,
        int Etapa
    );

    public record UpdateSeguimientoProduccionDto(
        int Id,
        DateTime Fecha,
        int LoteId,
        int MortalidadH,
        int MortalidadM,
        int SelH,
        decimal ConsKgH,
        decimal ConsKgM,
        int HuevoTot,
        int HuevoInc,
        string TipoAlimento,
        string Observaciones,
        decimal PesoHuevo,
        int Etapa
    );

    public record FilterSeguimientoProduccionDto(
        int? LoteId,
        DateTime? Desde,
        DateTime? Hasta
    );
}
