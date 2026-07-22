// src/ZooSanMarino.Application/DTOs/LoteBaseEngorde/LoteBaseEngordeDtos.cs
namespace ZooSanMarino.Application.DTOs.LoteBaseEngorde;

/// <summary>Ítem del catálogo de lotes base de engorde (agrupador global por empresa).</summary>
public sealed record LoteBaseEngordeDto(
    int Id,
    string Nombre,
    string? Descripcion,
    string? CodigoErp,
    string? LineaGenetica,
    /// <summary>Fecha de activación (se toma automática al crear). Ya NO controla vigencia por año.</summary>
    DateTime? FechaActivacion,
    /// <summary>Desactivación manual (apagado global): inactivo no aparece en ningún crear-lote.</summary>
    bool Activo,
    /// <summary>Cantidad de lotes de engorde vivos amarrados a este lote base.</summary>
    int LotesAsignados,
    /// <summary>Ids de granjas donde el lote base es visible al crear lote (filtro de visibilidad).</summary>
    IReadOnlyList<int> GranjaIds,
    /// <summary>Nombre del usuario que creó el lote base (resuelto por cédula).</summary>
    string? CreatedByNombre,
    DateTime CreatedAt
);

/// <summary>Creación: solo nombre. La fecha de activación y el usuario se capturan automáticamente.</summary>
public sealed record CreateLoteBaseEngordeDto(
    string Nombre
);

/// <summary>Edición: solo renombra.</summary>
public sealed record UpdateLoteBaseEngordeDto(
    int Id,
    string Nombre
);

/// <summary>Body del toggle manual de activación.</summary>
public sealed record SetActivoLoteBaseEngordeDto(bool Activo);

/// <summary>Granja asignada a un lote base (visibilidad al crear lote).</summary>
public sealed record LoteBaseEngordeGranjaDto(
    int FarmId,
    string FarmName
);

/// <summary>Body para asignar una granja a un lote base.</summary>
public sealed record AssignGranjaLoteBaseDto(int FarmId);
