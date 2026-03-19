// src/ZooSanMarino.Application/DTOs/ItemInventarioEcuadorDtos.cs
namespace ZooSanMarino.Application.DTOs;

public sealed record ItemInventarioEcuadorDto(
    int Id,
    string Codigo,
    string Nombre,
    string TipoItem,
    string Unidad,
    string? Descripcion,
    bool Activo,
    string? Grupo,
    string? TipoInventarioCodigo,
    string? DescripcionTipoInventario,
    string? Referencia,
    string? DescripcionItem,
    string? Concepto,
    int CompanyId,
    int PaisId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record ItemInventarioEcuadorCreateRequest(
    string Codigo,
    string Nombre,
    string TipoItem,
    string Unidad,
    string? Descripcion,
    bool Activo,
    string? Grupo = null,
    string? TipoInventarioCodigo = null,
    string? DescripcionTipoInventario = null,
    string? Referencia = null,
    string? DescripcionItem = null,
    string? Concepto = null
);

public sealed record ItemInventarioEcuadorUpdateRequest(
    string Nombre,
    string TipoItem,
    string Unidad,
    string? Descripcion,
    bool Activo,
    string? Grupo = null,
    string? TipoInventarioCodigo = null,
    string? DescripcionTipoInventario = null,
    string? Referencia = null,
    string? DescripcionItem = null,
    string? Concepto = null
);

/// <summary>Fila para carga masiva de ítems. Columnas: GRUPO, TIPO DE INVENTARIO, Desc. tipo inventario, Tipo inventario, Referencia, Desc. item, Concepto, Unidad de medida.</summary>
public sealed record ItemInventarioEcuadorCargaMasivaRow(
    string? Grupo,
    string? TipoInventarioCodigo,
    string? DescripcionTipoInventario,
    string? TipoItem,
    string? Referencia,
    string? DescripcionItem,
    string? Concepto,
    string? Unidad
);

/// <summary>Resultado de la carga masiva.</summary>
public sealed record ItemInventarioEcuadorCargaMasivaResult(
    int TotalFilas,
    int Creados,
    int Actualizados,
    int Errores,
    IReadOnlyList<string> MensajesError
);
