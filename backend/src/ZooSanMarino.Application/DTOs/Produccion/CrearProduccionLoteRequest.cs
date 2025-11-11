// src/ZooSanMarino.Application/DTOs/Produccion/CrearProduccionLoteRequest.cs
using System.ComponentModel.DataAnnotations;

namespace ZooSanMarino.Application.DTOs.Produccion;

public record CrearProduccionLoteRequest(
    [Required] int LoteId,
    [Required] DateTime FechaInicio,
    [Required] [Range(0, int.MaxValue)] int AvesInicialesH,
    [Required] [Range(0, int.MaxValue)] int AvesInicialesM,
    [Required] [Range(0, int.MaxValue)] int HuevosIniciales,
    [Required] string TipoNido,  // Jansen, Manual, Vencomatic
    [Required] string Ciclo,  // normal, 2 Replume, D: Depopulación
    string? NucleoP  // Núcleo de Producción
);



