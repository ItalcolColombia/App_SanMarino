namespace ZooSanMarino.Application.DTOs.Lotes;

/// <summary>
/// Datos mostrados en el modal «Cerrar lote» de Seguimiento Diario de Producción.
/// <para>
/// Cerrar un lote de producción no borra nada: bloquea el alta, la edición y el borrado de
/// seguimiento diario para que nadie toque un ciclo ya liquidado. Es reversible con
/// <see cref="AbrirLoteProduccionRequest"/>.
/// </para>
/// </summary>
public sealed record CierreLoteProduccionResumenDto(
    int LotePosturaProduccionId,
    string LoteNombre,
    bool EstaCerrado,
    int AvesHembrasActuales,
    int AvesMachosActuales,
    int RegistrosSeguimiento,
    DateTime? PrimerRegistro,
    DateTime? UltimoRegistro,
    DateTime? FechaInicioProduccion,
    /// <summary>Motivo, fecha y usuario del último cambio de estado (null si nunca se cerró).</summary>
    string? UltimoMotivo = null,
    DateTime? UltimoCambioEstado = null
);

/// <summary>Cierre de un lote de producción. El motivo queda en la auditoría del lote.</summary>
public sealed record CerrarLoteProduccionRequest(
    string Motivo,
    string ClosedByUserId
);

/// <summary>Reapertura de un lote de producción cerrado.</summary>
public sealed record AbrirLoteProduccionRequest(
    string Motivo,
    string OpenedByUserId
);
