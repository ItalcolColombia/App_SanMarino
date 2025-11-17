// src/ZooSanMarino.Application/DTOs/Lotes/TrasladoLoteDto.cs
using System.ComponentModel.DataAnnotations;

namespace ZooSanMarino.Application.DTOs.Lotes;

/// <summary>
/// DTO para solicitar traslado de un lote a otra granja
/// </summary>
public class TrasladoLoteRequestDto
{
    /// <summary>ID del lote a trasladar</summary>
    [Required(ErrorMessage = "El ID del lote es obligatorio")]
    public int LoteId { get; set; }

    /// <summary>ID de la granja destino</summary>
    [Required(ErrorMessage = "La granja destino es obligatoria")]
    public int GranjaDestinoId { get; set; }

    /// <summary>ID del núcleo destino (opcional)</summary>
    public string? NucleoDestinoId { get; set; }

    /// <summary>ID del galpón destino (opcional)</summary>
    public string? GalponDestinoId { get; set; }

    /// <summary>Observaciones del traslado (opcional)</summary>
    [MaxLength(500, ErrorMessage = "Las observaciones no pueden exceder 500 caracteres")]
    public string? Observaciones { get; set; }
}

/// <summary>
/// DTO para respuesta del traslado de lote
/// </summary>
public class TrasladoLoteResponseDto
{
    /// <summary>Indica si el traslado fue exitoso</summary>
    public bool Success { get; set; }

    /// <summary>Mensaje descriptivo del resultado</summary>
    public string Message { get; set; } = null!;

    /// <summary>ID del lote original (actualizado)</summary>
    public int? LoteOriginalId { get; set; }

    /// <summary>ID del nuevo lote creado en la granja destino</summary>
    public int? LoteNuevoId { get; set; }

    /// <summary>Nombre del lote trasladado</summary>
    public string? LoteNombre { get; set; }

    /// <summary>Nombre de la granja origen</summary>
    public string? GranjaOrigen { get; set; }

    /// <summary>Nombre de la granja destino</summary>
    public string? GranjaDestino { get; set; }
}

