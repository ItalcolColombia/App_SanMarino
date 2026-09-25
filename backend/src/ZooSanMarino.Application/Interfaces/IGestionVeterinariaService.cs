using ZooSanMarino.Application.DTOs.GestionVeterinaria;

namespace ZooSanMarino.Application.Interfaces;

public interface IGestionVeterinariaService
{
    Task<IReadOnlyList<VeterinariaGranjaDto>> GetMiMapaAsync(CancellationToken ct = default);
    Task<VeterinariaResumenDto> GetResumenAsync(CancellationToken ct = default);

    Task<IReadOnlyList<VisitaTecnicaDto>> GetVisitasAsync(CancellationToken ct = default);
    Task<VisitaTecnicaDto> CreateVisitaAsync(VisitaTecnicaCreateRequest req, CancellationToken ct = default);
    Task<VisitaTecnicaDto?> UpdateVisitaAsync(long id, VisitaTecnicaUpdateRequest req, CancellationToken ct = default);
    Task<VisitaTecnicaDto?> RealizarVisitaAsync(long id, RealizarVisitaRequest req, CancellationToken ct = default);
    Task<VisitaTecnicaDto?> CancelarVisitaAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<TareaCampoDto>> GetTareasCreadasAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TareaCampoDto>> GetMisTareasAsync(bool incluirCerradas, CancellationToken ct = default);
    Task<GestionVeterinariaInicioDto> GetInicioAsync(CancellationToken ct = default);
    Task<TareaCampoDto?> GetTareaAsync(long id, CancellationToken ct = default);
    Task<TareaCampoDto> CreateTareaAsync(TareaCampoCreateRequest req, CancellationToken ct = default);
    Task<TareaCampoDto?> UpdateTareaAsync(long id, TareaCampoUpdateRequest req, CancellationToken ct = default);
    Task<TareaCampoDto?> CumplirTareaAsync(long id, CumplirTareaCampoRequest req, CancellationToken ct = default);
    Task<TareaCampoDto?> ReabrirTareaAsync(long id, CancellationToken ct = default);
    Task<TareaCampoDto?> CancelarTareaAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<TareaCampoEvidenciaMetaDto>> GetEvidenciasAsync(long tareaId, CancellationToken ct = default);
    Task<TareaCampoEvidenciaDto?> GetEvidenciaAsync(long tareaId, long evidenciaId, CancellationToken ct = default);
}
