// src/ZooSanMarino.Application/DTOs/UpdateLoteDto.cs
namespace ZooSanMarino.Application.DTOs;

public class UpdateLoteDto
{
    public int LoteId { get; set; } // Requerido para actualización - secuencia numérica
    public string LoteNombre { get; set; } = null!;
    public int    GranjaId { get; set; }
    public string? NucleoId { get; set; }      // ← string? (coincide con Nucleo.NucleoId)
    public string? GalponId { get; set; }      // ← string? (coincide con Galpon.GalponId)
    public string? Regional { get; set; }
    public DateTime? FechaEncaset { get; set; }
    public int?    HembrasL { get; set; }
    public int?    MachosL { get; set; }
    public double? PesoInicialH { get; set; }
    public double? PesoInicialM { get; set; }
    public double? PesoMixto   { get; set; }
    public double? UnifH { get; set; }
    public double? UnifM { get; set; }
    public int?    MortCajaH { get; set; }
    public int?    MortCajaM { get; set; }
    public string? Raza { get; set; }
    public int?    AnoTablaGenetica { get; set; }
    public string? Linea { get; set; }
    public string? TipoLinea { get; set; }
    public string? CodigoGuiaGenetica { get; set; }
    public int?    LineaGeneticaId { get; set; }  // ← NUEVO: ID de la línea genética
    public string? Tecnico { get; set; }
    public int?    Mixtas { get; set; }
    public int?    AvesEncasetadas { get; set; }
    public string? LoteErp { get; set; }
    public string? LineaGenetica { get; set; }
    public int?    EdadInicial { get; set; }
}
