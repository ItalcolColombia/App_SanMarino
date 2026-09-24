namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Resumen seguro de un recurso para la bandeja/novedades: nunca expone más que ubicación y
/// contexto, y siempre se resuelve por datos (nunca por el header crudo de empresa activa).
/// </summary>
public record ResumenRecursoValidacion(
    int CompanyId,
    string FarmNombre,
    string? NucleoId,
    string? GalponId,
    string? LoteRef,
    DateOnly FechaSeguimiento
);

/// <summary>
/// Conecta un tipo de registro de negocio con el motor genérico de flujos de validación. El motor
/// no conoce inventario, huevos ni aves: cada proceso (Levante, Producción, y a futuro Inventario,
/// Ventas, etc.) implementa este contrato para resolver su empresa, resumir el registro y aplicar
/// o liberar sus efectos reales. Ver §16 del plan para el contrato de extensión a nuevos módulos.
/// </summary>
public interface IProcesoValidacionAdapter
{
    /// <summary>Debe coincidir con <c>validacion_procesos.adapter_key</c>.</summary>
    string AdapterKey { get; }

    /// <summary>Resuelve la empresa dueña del recurso a partir de los datos (fail-closed: null si no existe).</summary>
    Task<int?> ResolverCompanyIdAsync(string recursoId, CancellationToken ct = default);

    Task<ResumenRecursoValidacion?> ResumirAsync(string recursoId, CancellationToken ct = default);

    /// <summary>
    /// Finaliza el recurso de forma transaccional e idempotente: aplica alimento/aves reutilizando
    /// el finalizador existente del proceso. Debe ejecutarse DENTRO de la transacción que abre el
    /// motor al aprobar la última etapa; si falla, todo se revierte y la instancia queda en
    /// ERROR_FINALIZACION sin duplicar descuentos.
    /// </summary>
    Task FinalizarAsync(string recursoId, CancellationToken ct = default);

    /// <summary>Libera reservas/efectos pendientes al cancelar/eliminar el recurso por el flujo.</summary>
    Task LiberarAsync(string recursoId, CancellationToken ct = default);
}
