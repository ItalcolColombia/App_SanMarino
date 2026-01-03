using System.Text.Json;

/// file: backend/src/ZooSanMarino.Application/DTOs/SeguimientoLoteLevanteDto.cs
namespace ZooSanMarino.Application.DTOs;

public record SeguimientoLoteLevanteDto(
    int Id, int LoteId, DateTime FechaRegistro,
    int MortalidadHembras, int MortalidadMachos,
    int SelH, int SelM,
    int ErrorSexajeHembras, int ErrorSexajeMachos,
    double ConsumoKgHembras, string TipoAlimento, string? Observaciones,
    double? KcalAlH, double? ProtAlH, double? KcalAveH, double? ProtAveH, string Ciclo,
    // NUEVOS (mantenidos para compatibilidad con otros servicios)
    double? ConsumoKgMachos, double? PesoPromH, double? PesoPromM,
    double? UniformidadH, double? UniformidadM, double? CvH, double? CvM,
    // Metadata JSONB para campos adicionales/extras (consumo original con unidad, etc.)
    JsonDocument? Metadata
    // gestiones de administrativo  
    // int? IdAdministrativo, string? NombreAdministrativo, string? ApellidoAdministrativo, string? DireccionAdministrativo, string? TelefonoAdministrativo, string? EmailAdministrativo, string? FechaNacimientoAdministrativo, string? GeneroAdministrativo, string? EstadoAdministrativo, string? CodigoAdministrativo, string? TipoAdministrativo, string? RolAdministrativo, string? ClaveAdministrativo, string? ClaveAdministrativoConfirmada, string? ClaveAdministrativoConfirmadaConfirmada, string? ClaveAdministrativoConfirmadaConfirmadaConfirmada, string? ClaveAdministrativoConfirmada
    
);