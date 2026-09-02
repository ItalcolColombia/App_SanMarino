using System.Collections.Generic;

namespace ZooSanMarino.Application.DTOs;

public sealed record GuiaGeneticaEngordeHeaderDto(
    int Id,
    string Raza,
    int AnioGuia,
    string Estado
);

public sealed record GuiaGeneticaEngordeFiltersDto(
    IEnumerable<string> Razas,
    IEnumerable<int> Anos
);

public sealed record GuiaGeneticaEngordeDetalleDto(
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

public sealed record GuiaGeneticaEngordeImportResultDto(
    bool Success,
    int TotalFilasProcesadas,
    int TotalDetallesInsertados,
    int ErrorFilas,
    IReadOnlyList<string> Errors
);

public sealed record GuiaGeneticaEngordeDetalleInputDto(
    int Dia,
    decimal PesoCorporalG,
    decimal GananciaDiariaG,
    decimal PromedioGananciaDiariaG,
    decimal CantidadAlimentoDiarioG,
    decimal AlimentoAcumuladoG,
    decimal CA,
    decimal MortalidadSeleccionDiaria
);

public sealed record GuiaGeneticaEngordeManualRequestDto(
    string Raza,
    int AnioGuia,
    string Sexo,
    string Estado,
    IReadOnlyList<GuiaGeneticaEngordeDetalleInputDto> Items
);

