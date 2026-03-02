using ZooSanMarino.Application.DTOs.Mapas;

namespace ZooSanMarino.Application.Interfaces;

public interface IMapaService
{
    Task<IEnumerable<MapaListDto>> GetAllAsync(CancellationToken ct = default);
    Task<MapaDetailDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<MapaDetailDto> CreateAsync(CreateMapaDto dto, CancellationToken ct = default);
    Task<MapaDetailDto?> UpdateAsync(int id, UpdateMapaDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task SavePasosAsync(int mapaId, IEnumerable<MapaPasoDto> pasos, CancellationToken ct = default);

    Task<EjecutarMapaResponse> EjecutarAsync(int mapaId, EjecutarMapaRequest request, CancellationToken ct = default);
    /// <summary>Ejecuta los pasos de una ejecución ya creada (uso interno en background).</summary>
    Task ProcessExecutionAsync(int ejecucionId, CancellationToken ct = default);
    Task<MapaEjecucionEstadoDto?> GetEjecucionEstadoAsync(int ejecucionId, CancellationToken ct = default);
    Task<(Stream? Stream, string FileName)?> GetEjecucionArchivoAsync(int ejecucionId, CancellationToken ct = default);
    Task<IEnumerable<MapaEjecucionHistorialDto>> GetEjecucionesByMapaAsync(int mapaId, int limit = 20, CancellationToken ct = default);
}
