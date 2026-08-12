// src/ZooSanMarino.Application/DTOs/SiloDtos.cs
namespace ZooSanMarino.Application.DTOs;

// ─── LISTA MAESTRA (silo_catalogo) ───────────────────────────────────────────

/// <summary>Entrada de la lista maestra de silos de la empresa (1..100).</summary>
public sealed record SiloCatalogoDto(
    int Id,
    int CompanyId,
    int Numero,
    string Nombre,
    string? Descripcion,
    bool Activo,
    /// <summary>Cuántas granjas de la empresa tienen asignado este silo (para no borrarlo a ciegas).</summary>
    int GranjasAsignadas = 0
);

public sealed record CreateSiloCatalogoDto(
    int Numero,
    string? Nombre = null,
    string? Descripcion = null,
    bool Activo = true
);

public sealed record UpdateSiloCatalogoDto(
    string? Nombre = null,
    string? Descripcion = null,
    bool? Activo = null
);

/// <summary>
/// Genera de una sola vez el rango <c>Desde..Hasta</c> de la lista maestra (el «voy a crear una lista
/// de silos del 1 al 100»). Idempotente: los números que ya existen se omiten, no se duplican.
/// </summary>
public sealed record GenerarRangoSilosDto(
    int Desde,
    int Hasta,
    /// <summary>Patrón del nombre; <c>{n}</c> se reemplaza por el número. Default "Silo {n}".</summary>
    string? PatronNombre = null
);

/// <summary>Resultado de generar un rango: cuántos se crearon y cuántos ya estaban.</summary>
public sealed record GenerarRangoSilosResultDto(int Creados, int Omitidos, IReadOnlyList<SiloCatalogoDto> Silos);

// ─── SILOS DE UNA GRANJA (farm_silos) ────────────────────────────────────────

/// <summary>Silo o bodega de una granja: la ubicación REAL del inventario cuando el flag está activo.</summary>
public sealed record FarmSiloDto(
    int Id,
    int CompanyId,
    int GranjaId,
    string? GranjaNombre,
    int? SiloCatalogoId,
    /// <summary>Número del catálogo (null en bodegas), para ordenar y mostrar.</summary>
    int? Numero,
    string Nombre,
    /// <summary><c>Silo</c> | <c>Bodega</c>.</summary>
    string Tipo,
    string? CodigoErpUbicacion,
    string? Descripcion,
    string? CentroOperacion,
    string? CodigoBodega,
    bool Activo,
    /// <summary>Galpones que declaran alimentarse de este silo (informativo).</summary>
    int GalponesAsignados = 0,
    /// <summary>Lotes que declaran consumir de este silo (informativo).</summary>
    int LotesAsignados = 0
);

public sealed record CreateFarmSiloDto(
    int GranjaId,
    string Tipo,
    /// <summary>Requerido si <c>Tipo = Silo</c>: la entrada del catálogo de la que sale.</summary>
    int? SiloCatalogoId = null,
    /// <summary>Requerido si <c>Tipo = Bodega</c> (los silos heredan el nombre del catálogo).</summary>
    string? Nombre = null,
    string? CodigoErpUbicacion = null,
    string? Descripcion = null,
    string? CentroOperacion = null,
    string? CodigoBodega = null,
    bool Activo = true
);

public sealed record UpdateFarmSiloDto(
    string? Nombre = null,
    string? CodigoErpUbicacion = null,
    string? Descripcion = null,
    string? CentroOperacion = null,
    string? CodigoBodega = null,
    bool? Activo = null
);

/// <summary>
/// Asigna a una granja el conjunto de silos del catálogo indicado («le digo a esa granja cuántos
/// silos tiene»). Es un SET: los que no vengan se dan de baja lógica si no tienen movimientos.
/// </summary>
public sealed record AsignarSilosGranjaDto(
    int GranjaId,
    IReadOnlyList<int> SiloCatalogoIds,
    /// <summary>Crear también la bodega de la granja si todavía no existe.</summary>
    bool CrearBodega = false,
    string? NombreBodega = null
);

// ─── ASIGNACIONES N:M ────────────────────────────────────────────────────────

/// <summary>Silo asignado a un galpón (qué silos lo alimentan).</summary>
public sealed record GalponSiloDto(
    int Id,
    int GranjaId,
    string NucleoId,
    string GalponId,
    int FarmSiloId,
    string SiloNombre,
    string SiloTipo,
    int? SiloNumero,
    bool Activo
);

/// <summary>Silo del que consume un lote.</summary>
public sealed record LoteSiloDto(
    int Id,
    int LoteId,
    int FarmSiloId,
    string SiloNombre,
    string SiloTipo,
    int? SiloNumero,
    bool Activo
);

/// <summary>
/// Reemplaza el conjunto de silos de un galpón o de un lote. Es un SET completo: lo que no venga se
/// quita. Lista vacía = sin silos asignados.
/// </summary>
public sealed record AsignarSilosDto(IReadOnlyList<int> FarmSiloIds);
