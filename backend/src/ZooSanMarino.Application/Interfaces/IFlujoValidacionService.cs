using ZooSanMarino.Application.DTOs.FlujosValidacion;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>Modo de validación resuelto para una empresa + proceso (plan §8.1).</summary>
public enum ModoValidacionProceso
{
    /// <summary>Sin flujo ni flag: comportamiento histórico, aplica al guardar.</summary>
    Inmediato,

    /// <summary>Sin flujo publicado, con <c>companies.requiere_validacion_seguimiento_diario</c>.</summary>
    LegacyUnPaso,

    /// <summary>Hay un flujo PUBLICADO vigente para la empresa + proceso.</summary>
    Secuencial,
}

/// <summary>
/// Motor genérico de flujos de validación parametrizables por empresa. No conoce inventario, aves
/// ni huevos: delega en el <see cref="IProcesoValidacionAdapter"/> registrado para cada proceso.
/// </summary>
public interface IFlujoValidacionService
{
    // ── Resolución de modo (integración con la doble validación existente, plan §8.1) ──
    Task<ModoValidacionProceso> ResolverModoAsync(int companyId, string procesoKey, CancellationToken ct = default);

    // ── Configuración: catálogo, borradores, versionado, publicación (plan §9.4) ──
    Task<IReadOnlyList<ProcesoValidacionDto>> ListarProcesosAsync(int companyId, CancellationToken ct = default);
    Task<FlujoValidacionDto?> ObtenerFlujoVigenteAsync(int companyId, string procesoKey, CancellationToken ct = default);
    Task<FlujoValidacionDto> CrearBorradorAsync(CrearBorradorFlujoCommand cmd, CancellationToken ct = default);
    Task<FlujoValidacionDto> ActualizarBorradorAsync(int flujoId, ActualizarBorradorFlujoCommand cmd, CancellationToken ct = default);
    Task<FlujoValidacionDto> ClonarAsync(int flujoId, CancellationToken ct = default);
    Task<PreflightPublicacionDto> PreflightPublicacionAsync(int flujoId, CancellationToken ct = default);
    Task<FlujoValidacionDto> PublicarAsync(int flujoId, CancellationToken ct = default);
    Task<FlujoValidacionDto> RetirarAsync(int flujoId, CancellationToken ct = default);
    Task<IReadOnlyList<AsignableFlujoDto>> ListarAsignablesAsync(int companyId, CancellationToken ct = default);

    // ── Instancias: creación desde el adaptador del proceso al guardar el registro (plan §8.2) ──
    Task<Guid> CrearInstanciaAsync(int companyId, string procesoKey, string recursoId, Guid creadorUserId, CancellationToken ct = default);
    Task CancelarInstanciaDelRecursoAsync(string procesoKey, string recursoId, CancellationToken ct = default);

    // ── Ejecución: aprobar, devolver, corregir, eliminar, consultar (plan §9.4) ──
    Task<EstadoInstanciaFlujoDto?> ObtenerEstadoAsync(string procesoKey, string recursoId, CancellationToken ct = default);
    Task<ResultadoAccionInstanciaDto> AprobarAsync(AprobarInstanciaCommand cmd, CancellationToken ct = default);
    Task<ResultadoAccionInstanciaDto> DevolverAsync(DevolverInstanciaCommand cmd, CancellationToken ct = default);
    Task<ResultadoAccionInstanciaDto> CorregirYReenviarAsync(CorregirYReenviarInstanciaCommand cmd, CancellationToken ct = default);
    Task<ResultadoAccionInstanciaDto> EliminarAsync(EliminarInstanciaCommand cmd, CancellationToken ct = default);
    Task<IReadOnlyList<PendienteFlujoDto>> ObtenerMisPendientesAsync(int companyId, string? procesoKey, CancellationToken ct = default);

    // ── Novedades persistentes del Home (plan §8.4, §10.4) ──
    Task<IReadOnlyList<NovedadValidacionDto>> ObtenerMisNovedadesAsync(CancellationToken ct = default);
    Task MarcarNovedadLeidaAsync(long novedadId, CancellationToken ct = default);
}
