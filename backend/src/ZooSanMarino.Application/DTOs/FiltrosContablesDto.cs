// src/ZooSanMarino.Application/DTOs/FiltrosContablesDto.cs
namespace ZooSanMarino.Application.DTOs;

public class FiltrosContablesDto
{
    public List<GranjaFiltroContableDto> Granjas { get; set; } = new();
}

public class GranjaFiltroContableDto
{
    public int GranjaId { get; set; }
    public string GranjaNombre { get; set; } = null!;
    public List<NucleoFiltroContableDto> Nucleos { get; set; } = new();
}

public class NucleoFiltroContableDto
{
    public string? NucleoId { get; set; }
    public string NucleoNombre { get; set; } = null!;
    public List<GalponFiltroContableDto> Galpones { get; set; } = new();
}

public class GalponFiltroContableDto
{
    public string? GalponId { get; set; }
    public string GalponNombre { get; set; } = null!;
    public List<LoteBaseFiltroContableDto> LotesBase { get; set; } = new();
}

public class LoteBaseFiltroContableDto
{
    public int LoteId { get; set; }
    public string LoteNombre { get; set; } = null!;
    public int? LotePosturaBaseId { get; set; }
    public string? CodigoErp { get; set; }
}
