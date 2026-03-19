using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

public interface IGuiaGeneticaEcuadorService
{
    Task<GuiaGeneticaEcuadorFiltersDto> GetFiltersAsync(CancellationToken ct = default);

    /// <summary>Años disponibles para una raza (solo guías activas del company actual).</summary>
    Task<IEnumerable<int>> GetAnosPorRazaAsync(string raza, CancellationToken ct = default);

    /// <summary>Sexos (mixto/hembra/macho) creados para guía activa (por empresa+raza+año).</summary>
    Task<IEnumerable<string>> GetSexosCreadosAsync(string raza, int anioGuia, CancellationToken ct = default);

    Task<IEnumerable<GuiaGeneticaEcuadorDetalleDto>> GetDatosAsync(string raza, int anioGuia, string sexo, CancellationToken ct = default);

    Task<GuiaGeneticaEcuadorImportResultDto> ImportExcelAsync(IFormFile file, string raza, int anioGuia, string estado, CancellationToken ct = default);

    Task<GuiaGeneticaEcuadorHeaderDto> UpsertManualAsync(GuiaGeneticaEcuadorManualRequestDto request, CancellationToken ct = default);
}

