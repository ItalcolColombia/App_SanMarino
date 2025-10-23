// src/ZooSanMarino.Application/DTOs/Produccion/CrearSeguimientoRequest.cs
using System.ComponentModel.DataAnnotations;

namespace ZooSanMarino.Application.DTOs.Produccion;

public record CrearSeguimientoRequest(
    [Required] int ProduccionLoteId,
    [Required] DateTime FechaRegistro,
    [Required] [Range(0, int.MaxValue)] int MortalidadH,
    [Required] [Range(0, int.MaxValue)] int MortalidadM,
    [Required] [Range(0, double.MaxValue)] decimal ConsumoKg,
    [Required] [Range(0, int.MaxValue)] int HuevosTotales,
    [Required] [Range(0, int.MaxValue)] int HuevosIncubables,
    [Required] [Range(0, double.MaxValue)] decimal PesoHuevo,
    string? Observaciones
);



