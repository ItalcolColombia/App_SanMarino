// src/ZooSanMarino.Application/DTOs/TrasladoNavigationDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para ubicación completa con toda la información de navegación
/// </summary>
public record UbicacionCompletaDto(
    // IDs principales
    int? LoteId,
    int? GranjaId,
    string? NucleoId,
    string? GalponId,
    int? CompanyId,
    
    // Nombres descriptivos
    string? LoteNombre,
    string? GranjaNombre,
    string? NucleoNombre,
    string? GalponNombre,
    string? CompanyNombre,
    
    // Información geográfica
    string? Regional,
    string? Departamento,
    string? Municipio,
    
    // Información del galpón
    string? TipoGalpon,
    string? AnchoGalpon,
    string? LargoGalpon,
    
    // Información del lote
    string? Raza,
    string? Linea,
    string? TipoLinea,
    string? CodigoGuiaGenetica,
    int? AnoTablaGenetica,
    string? Tecnico,
    
    // Información adicional
    string? Status,
    DateTime? FechaEncaset,
    int? EdadInicial
);

/// <summary>
/// DTO mejorado para movimiento de aves con navegación completa
/// </summary>
public record MovimientoAvesCompletoDto(
    // Información básica del movimiento
    int Id,
    string NumeroMovimiento,
    DateTime FechaMovimiento,
    string TipoMovimiento,
    
    // Ubicaciones completas
    UbicacionCompletaDto Origen,
    UbicacionCompletaDto Destino,
    
    // Cantidades trasladadas
    int CantidadHembras,
    int CantidadMachos,
    int CantidadMixtas,
    int TotalAves,
    
    // Estado e información
    string Estado,
    string? MotivoMovimiento,
    string? Observaciones,
    
    // Usuario responsable
    int UsuarioMovimientoId,
    string? UsuarioNombre,
    
    // Fechas de control
    DateTime? FechaProcesamiento,
    DateTime? FechaCancelacion,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    
    // Información calculada
    bool EsMovimientoInterno,
    bool EsMovimientoEntreGranjas,
    string TipoMovimientoDescripcion
);

/// <summary>
/// DTO para búsqueda de movimientos con navegación completa
/// </summary>
public sealed record MovimientoAvesCompletoSearchRequest(
    // Filtros básicos
    string? TipoMovimiento = null,
    string? Estado = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    
    // Filtros por origen
    int? LoteOrigenId = null,
    int? GranjaOrigenId = null,
    string? NucleoOrigenId = null,
    string? GalponOrigenId = null,
    int? CompanyOrigenId = null,
    
    // Filtros por destino
    int? LoteDestinoId = null,
    int? GranjaDestinoId = null,
    string? NucleoDestinoId = null,
    string? GalponDestinoId = null,
    int? CompanyDestinoId = null,
    
    // Filtros por usuario
    int? UsuarioMovimientoId = null,
    
    // Ordenamiento y paginación
    string SortBy = "fecha_movimiento",
    bool SortDesc = true,
    int Page = 1,
    int PageSize = 20
);

/// <summary>
/// DTO para estadísticas de traslados con navegación
/// </summary>
public record EstadisticasTrasladoDto(
    int TotalMovimientos,
    int MovimientosPendientes,
    int MovimientosCompletados,
    int MovimientosCancelados,
    int TotalAvesTrasladadas,
    int MovimientosInternos,
    int MovimientosEntreGranjas,
    DateTime? FechaDesde,
    DateTime? FechaHasta,
    List<EstadisticaPorGranjaDto> PorGranja,
    List<EstadisticaPorTipoDto> PorTipo
);

/// <summary>
/// DTO para estadísticas por granja
/// </summary>
public record EstadisticaPorGranjaDto(
    int GranjaId,
    string GranjaNombre,
    int TotalMovimientos,
    int TotalAves,
    int MovimientosEntrada,
    int MovimientosSalida
);

/// <summary>
/// DTO para estadísticas por tipo de movimiento
/// </summary>
public record EstadisticaPorTipoDto(
    string TipoMovimiento,
    int Cantidad,
    int TotalAves,
    double Porcentaje
);

/// <summary>
/// DTO para resumen de traslado (para dashboard)
/// </summary>
public record ResumenTrasladoDto(
    int Id,
    string NumeroMovimiento,
    DateTime FechaMovimiento,
    string Estado,
    string OrigenResumen,
    string DestinoResumen,
    int TotalAves,
    string? UsuarioNombre
);





