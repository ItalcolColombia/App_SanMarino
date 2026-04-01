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
    string ClosedByUserId
);

public sealed record AbrirLoteLevanteRequest(
    string Motivo,
    string OpenedByUserId
);
