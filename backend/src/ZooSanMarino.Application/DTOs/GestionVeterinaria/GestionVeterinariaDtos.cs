namespace ZooSanMarino.Application.DTOs.GestionVeterinaria;

public record VeterinariaLoteDto(int Id, string Nombre, string? Fase);
public record VeterinariaGalponDto(string Id, string Nombre, IReadOnlyList<VeterinariaLoteDto> Lotes);
public record VeterinariaNucleoDto(
    string Id, string Nombre, IReadOnlyList<VeterinariaGalponDto> Galpones,
    IReadOnlyList<VeterinariaLoteDto> LotesSinGalpon);
public record VeterinariaGranjaDto(
    int Id, string Nombre, decimal? Latitud, decimal? Longitud,
    IReadOnlyList<VeterinariaNucleoDto> Nucleos,
    IReadOnlyList<VeterinariaLoteDto> LotesSinUbicacion,
    int TotalGalpones, int TotalLotes);

public record VisitaTecnicaDto(
    long Id, int FarmId, string FarmNombre, string? NucleoId, string? NucleoNombre,
    string? GalponId, string? GalponNombre, int? LoteId, string? LoteNombre,
    string Titulo, string? Objetivo, DateTime FechaProgramada, DateTime? FechaRealizada,
    string Estado, string? Observaciones, Guid VeterinarioUserId, string VeterinarioNombre,
    int TotalTareas, int TareasPendientes, DateTime CreatedAt);

public record VisitaTecnicaCreateRequest(
    int FarmId, string? NucleoId, string? GalponId, int? LoteId,
    string Titulo, string? Objetivo, DateTime FechaProgramada);
public record VisitaTecnicaUpdateRequest(
    int FarmId, string? NucleoId, string? GalponId, int? LoteId,
    string Titulo, string? Objetivo, DateTime FechaProgramada);
public record RealizarVisitaRequest(string? Observaciones);

public record TareaCampoEvidenciaInput(string Base64, string? FileName, string ContentType, int SizeBytes);
public record TareaCampoEvidenciaMetaDto(
    long Id, string? FileName, string ContentType, int SizeBytes, Guid CreatedByUserId, DateTime CreatedAt);
public record TareaCampoEvidenciaDto(
    long Id, string ImagenBase64, string? FileName, string ContentType, int SizeBytes);

public record TareaCampoDto(
    long Id, long? VisitaId, int FarmId, string FarmNombre,
    string? NucleoId, string? NucleoNombre, string? GalponId, string? GalponNombre,
    int? LoteId, string? LoteNombre, string Titulo, string? Instrucciones,
    DateTime FechaInicio, DateTime FechaFin, bool RequiereObservacion, bool RequiereFoto,
    string Estado, string EstadoTemporal, Guid CreadaPorUserId, string CreadaPorNombre,
    Guid? RealizadaPorUserId, string? RealizadaPorNombre, DateTime? FechaRealizada,
    string? ObservacionCumplimiento, int CantidadEvidencias, bool PuedeCumplir, DateTime CreatedAt);

public record TareaCampoCreateRequest(
    long? VisitaId, int FarmId, string? NucleoId, string? GalponId, int? LoteId,
    string Titulo, string? Instrucciones, DateTime FechaInicio, DateTime FechaFin,
    bool RequiereObservacion, bool RequiereFoto);
public record TareaCampoUpdateRequest(
    int FarmId, string? NucleoId, string? GalponId, int? LoteId,
    string Titulo, string? Instrucciones, DateTime FechaInicio, DateTime FechaFin,
    bool RequiereObservacion, bool RequiereFoto);
public record CumplirTareaCampoRequest(
    string? Observacion, IReadOnlyList<TareaCampoEvidenciaInput>? Evidencias);

public record VeterinariaResumenDto(
    int GranjasAsignadas, int VisitasProximas, int TareasPendientes, int TareasVencidas);
public record GestionVeterinariaInicioDto(
    VeterinariaResumenDto Resumen,
    IReadOnlyList<TareaCampoDto> Tareas);
