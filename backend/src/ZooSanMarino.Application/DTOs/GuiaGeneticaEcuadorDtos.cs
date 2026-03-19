using System.Collections.Generic;

namespace ZooSanMarino.Application.DTOs;

public sealed record GuiaGeneticaEcuadorHeaderDto(
    int Id,
    string Raza,
    int AnioGuia,
    string Estado
);

public sealed record GuiaGeneticaEcuadorFiltersDto(
    IEnumerable<string> Razas,
    IEnumerable<int> Anos
);

public sealed record GuiaGeneticaEcuadorDetalleDto(
    string Sexo,
    int Dia,
    decimal PesoCorporalG,
    decimal GananciaDiariaG,
    decimal PromedioGananciaDiariaG,
    decimal CantidadAlimentoDiarioG,
    decimal AlimentoAcumuladoG,
    decimal CA,
    decimal MortalidadSeleccionDiaria
);

public sealed record GuiaGeneticaEcuadorImportResultDto(
    bool Success,
    int TotalFilasProcesadas,
    int TotalDetallesInsertados,
    int ErrorFilas,
    IReadOnlyList<string> Errors
);

public sealed record GuiaGeneticaEcuadorDetalleInputDto(
    int Dia,
    decimal PesoCorporalG,
    decimal GananciaDiariaG,
    decimal PromedioGananciaDiariaG,
    decimal CantidadAlimentoDiarioG,
    decimal AlimentoAcumuladoG,
    decimal CA,
    decimal MortalidadSeleccionDiaria
);

public sealed record GuiaGeneticaEcuadorManualRequestDto(
    string Raza,
    int AnioGuia,
    string Sexo,
    string Estado,
    IReadOnlyList<GuiaGeneticaEcuadorDetalleInputDto> Items
);

