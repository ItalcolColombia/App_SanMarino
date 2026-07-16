using ZooSanMarino.Application.DTOs.PuentePanama;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Cliente de SOLO LECTURA contra el API externo ZooPanamaPollo. Encapsula el login JWT
/// (cacheando el token) y los GET de la jerarquía. Por regla del proyecto NUNCA expone ni
/// realiza operaciones de escritura (POST de negocio, PUT, DELETE) hacia el sistema origen.
/// </summary>
public interface IPuentePanamaApiClient
{
    /// <summary>Fija la conexión activa para las llamadas siguientes (null = usa la config del backend). Resetea el token cacheado.</summary>
    void UsarConexion(PanamaConexion? conexion);

    /// <summary>Prueba el login con la conexión activa; devuelve ok + expiración del token (sin exponer el token).</summary>
    Task<ConexionResultDto> ProbarConexionAsync(CancellationToken ct = default);

    Task<IReadOnlyList<PanamaCliente>> GetClientesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PanamaGranja>> GetGranjasByClienteAsync(int idCliente, CancellationToken ct = default);
    Task<IReadOnlyList<PanamaGalpon>> GetGalponesByGranjaAsync(int idGranja, CancellationToken ct = default);
    Task<IReadOnlyList<PanamaLote>> GetLotesByGalponAsync(int idGalpon, CancellationToken ct = default);
    Task<IReadOnlyList<PanamaInfoProductiva>> GetInfoProductivaByLoteAsync(int idLote, CancellationToken ct = default);
    Task<IReadOnlyList<PanamaLoteReproductora>> GetLoteReproductoraByLoteAsync(int idLote, CancellationToken ct = default);
    Task<IReadOnlyList<PanamaInfoProductivaRepro>> GetInfoProductivaReproByLoteReproAsync(int idLoteReproductora, CancellationToken ct = default);

    /// <summary>Lesiones registradas sobre un lote reproductora del origen (solo lectura).</summary>
    Task<IReadOnlyList<PanamaLesion>> GetLesionesByReproAsync(int idLoteReproductora, CancellationToken ct = default);

    /// <summary>Guía genética (estándar de consumo gramoDiaQq por edad) del origen.</summary>
    Task<IReadOnlyList<PanamaGuiaGenetica>> GetGuiaGeneticaAsync(CancellationToken ct = default);

    /// <summary>Catálogo de Listas del origen (tipo 1 = líneas genéticas) para resolver nombres por id.</summary>
    Task<IReadOnlyList<PanamaListaItem>> GetListasAsync(CancellationToken ct = default);
}
