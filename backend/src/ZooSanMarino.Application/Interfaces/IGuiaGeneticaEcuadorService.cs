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

    /// <summary>
    /// Agrupa la curva diaria (sexo mixto) en semanas de 7 días y devuelve filas compatibles con <see cref="GuiaGeneticaDto"/> para el tab Indicadores.
    /// Semana s = días [(s-1)*7+1 .. s*7]. Consumo y mortalidad: promedio del período; peso: último día del período.
    /// </summary>
    Task<IEnumerable<GuiaGeneticaDto>> GetIndicadoresRangoSemanasAsync(string raza, int anioGuia, int semanaDesde, int semanaHasta, CancellationToken ct = default);

    Task<GuiaGeneticaEcuadorImportResultDto> ImportExcelAsync(IFormFile file, string raza, int anioGuia, string estado, CancellationToken ct = default);

    Task<GuiaGeneticaEcuadorHeaderDto> UpsertManualAsync(GuiaGeneticaEcuadorManualRequestDto request, CancellationToken ct = default);
}

