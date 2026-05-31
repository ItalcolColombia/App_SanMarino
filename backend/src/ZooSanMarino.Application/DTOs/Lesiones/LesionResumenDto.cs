namespace ZooSanMarino.Application.DTOs.Lesiones;

public sealed record LesionResumenDto(
    string TipoLesion,
    string ModuloOrigen,
    int    Total,
    int    TotalMacho,
    int    TotalHembra,
    int    TotalMixtas
);
