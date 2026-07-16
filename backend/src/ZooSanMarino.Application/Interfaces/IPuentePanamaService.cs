using ZooSanMarino.Application.DTOs.PuentePanama;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Orquestador del puente ZooPanamaPollo → módulo de pollo engorde (empresa activa).
/// Recorre el origen (solo lectura), mapea y reutiliza los servicios de negocio existentes
/// (guía genética / granja / núcleo / galpón / lote / seguimiento / reproductora) de forma idempotente.
/// </summary>
public interface IPuentePanamaService
{
    /// <summary>Prueba el login contra el origen (para el front).</summary>
    Task<ConexionResultDto> ProbarConexionAsync(PanamaConexion? origen, CancellationToken ct = default);

    /// <summary>Clientes del origen (para poblar filtros del front).</summary>
    Task<IReadOnlyList<PanamaCliente>> GetClientesOrigenAsync(PanamaConexion? origen, CancellationToken ct = default);

    /// <summary>Granjas del origen (todas o de un cliente) para poblar filtros del front.</summary>
    Task<IReadOnlyList<PanamaGranja>> GetGranjasOrigenAsync(PanamaConexion? origen, int? clienteIdOrigen, CancellationToken ct = default);

    /// <summary>Ejecuta (o previsualiza, si <c>DryRun</c>) la sincronización según los filtros del request.</summary>
    Task<ResultadoSincronizacionDto> SincronizarAsync(SincronizarPanamaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Historial paginado de corridas del puente de la empresa activa (más recientes primero).
    /// <paramref name="incluirValidaciones"/> = false excluye las corridas dry-run.
    /// </summary>
    Task<SincronizacionHistorialPagedDto> GetHistorialAsync(int page, int pageSize, bool incluirValidaciones, CancellationToken ct = default);

    /// <summary>
    /// Detalle de una corrida del historial (metadatos + el resultado completo persistido).
    /// Null si no existe, no es del puente o no pertenece a la empresa activa.
    /// </summary>
    Task<SincronizacionHistorialDetalleDto?> GetHistorialDetalleAsync(int id, CancellationToken ct = default);
}
