// src/ZooSanMarino.Application/DTOs/Produccion/CrearProduccionLoteRequest.cs
using System.ComponentModel.DataAnnotations;

namespace ZooSanMarino.Application.DTOs.Produccion;

public record CrearProduccionLoteRequest(
    [Required] int LoteId,
    [Required] DateTime FechaInicio,
    [Required] [Range(0, int.MaxValue)] int AvesInicialesH,
    [Required] [Range(0, int.MaxValue)] int AvesInicialesM,
    string? Observaciones
);



