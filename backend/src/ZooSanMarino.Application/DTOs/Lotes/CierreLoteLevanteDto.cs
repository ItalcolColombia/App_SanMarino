namespace ZooSanMarino.Application.DTOs.Lotes;

/// <summary>Datos mostrados en el modal «Cerrar lote» (seguimiento diario levante).</summary>
public sealed record CierreLoteLevanteResumenDto(
    int LotePosturaLevanteId,
    string LoteNombre,
    int AvesHembrasDisponibles,
    int AvesMachosDisponibles,
    bool YaExisteLoteProduccion
);

public sealed record CerrarLoteLevanteRequest(
    int HuevosIniciales,
    string ClosedByUserId,
    /// <summary>
    /// Fecha de inicio de producción. Si no se envía, se usa la fecha/hora actual del servidor.
    /// </summary>
    DateTime? FechaInicioProduccion = null,
    /// <summary>
    /// Aves iniciales que pasarán a producción (opcional). Si no se envía, se usan las aves actuales del levante.
    /// </summary>
    int? AvesHInicialProd = null,
    int? AvesMInicialProd = null,
    /// <summary>Motivo del ajuste de aves (solo referencia/auditoría; puede persistirse en el futuro).</summary>
    string? MotivoAjusteAves = null
);

public sealed record AbrirLoteLevanteRequest(
    string Motivo,
    string OpenedByUserId
);
