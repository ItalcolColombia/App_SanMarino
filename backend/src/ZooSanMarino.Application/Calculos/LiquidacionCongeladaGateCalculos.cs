// src/ZooSanMarino.Application/Calculos/LiquidacionCongeladaGateCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Operaciones de ESCRITURA sobre un lote de pollo engorde que la liquidación congelada bloquea.
/// Lista CERRADA (plan congelar_liquidacion_lote_engorde): agregar una operación acá implica
/// decidir conscientemente si un lote liquidado la permite o no — no se hereda por accidente.
/// </summary>
public enum OperacionLoteEngordeLiquidado
{
    /// <summary>B1 — editar el maestro del lote (aves encasetadas, fecha encaset…).</summary>
    EditarLote,
    /// <summary>B2 — soft delete del lote.</summary>
    EliminarLote,
    /// <summary>B3 — hard delete (arrastra por FK todo el histórico).</summary>
    EliminarDefinitivoLote,
    /// <summary>B4 — aplicar «Cuadrar Saldos» (escribe seguimiento + anula/inserta histórico).</summary>
    AplicarCuadrarSaldos,
    /// <summary>B5 — backfill de metadata del seguimiento.</summary>
    BackfillMetadata,
    /// <summary>B6 — seguimiento de reproductora (el trigger de cruce reescribe los días 1-7 del lote).</summary>
    SeguimientoReproductora,
    /// <summary>B7 — crear/editar/eliminar lotes reproductora (cambian las aves asignadas).</summary>
    ReproductoraLote,
    /// <summary>B8 — movimientos/ventas de aves (crear, editar, cancelar, eliminar, completar).</summary>
    MovimientoAves,
    /// <summary>B9 — los 6 insumos de la liquidación Panamá después de cerrar.</summary>
    LiquidacionInsumosPanama,
    /// <summary>B10 — sincronización del puente Panamá sobre un lote destino ya liquidado.</summary>
    PuenteSincronizacion
}

/// <summary>
/// Decisión PURA del gate de escritura sobre lotes de engorde liquidados.
/// <para>
/// La señal es <c>estado_operativo_lote == "Cerrado"</c> (OrdinalIgnoreCase), la MISMA que ya usan
/// los 9 gates preexistentes del módulo — no se inventa una segunda noción de «liquidado». La copia
/// congelada es un derivado del estado, garantizado por la BD (único parcial + trigger), no una
/// señal paralela que pueda desincronizarse.
/// </para>
/// <para>
/// ⚠️ <c>liquidado_at</c> NO es señal: la reapertura no la limpia y hay lotes 'Abierto' que la
/// conservan. Jamás decidir por ella.
/// </para>
/// </summary>
public static class LiquidacionCongeladaGateCalculos
{
    /// <summary>Mensaje canónico del bloqueo (dice qué hacer, no solo qué pasó).</summary>
    public const string MensajeBloqueo = "El lote está liquidado. Reabra el lote para modificarlo.";

    /// <summary>Estado que define «liquidado» — el mismo literal de los gates preexistentes.</summary>
    public const string EstadoCerrado = "Cerrado";

    /// <summary>
    /// ¿El lote está liquidado? <c>null</c>/vacío/otros estados ⇒ NO liquidado (la operación pasa,
    /// idéntico al comportamiento previo a esta feature).
    /// </summary>
    public static bool EstaLiquidado(string? estadoOperativoLote) =>
        string.Equals(estadoOperativoLote, EstadoCerrado, StringComparison.OrdinalIgnoreCase);

    /// <summary>Mensaje con el lote identificado (para operaciones multi-lote, ej. ventas).</summary>
    public static string MensajeBloqueoCon(string? loteNombre) =>
        string.IsNullOrWhiteSpace(loteNombre)
            ? MensajeBloqueo
            : $"El lote '{loteNombre.Trim()}' está liquidado. Reabra el lote para modificarlo.";

    /// <summary>
    /// Gate único: lanza <see cref="InvalidOperationException"/> (→ 400) si el lote está liquidado.
    /// </summary>
    /// <param name="estadoOperativoLote">Estado operativo actual del lote.</param>
    /// <param name="operacion">Operación de la lista cerrada (documentación + trazabilidad del gate).</param>
    /// <param name="omitirGateLiquidado">
    /// Bypass EXPLÍCITO para las herramientas de reparación (corrección de aves disponibles), que
    /// existen justamente para arreglar lotes liquidados y re-congelan al terminar.
    /// </param>
    /// <param name="loteNombre">Si viene, el mensaje identifica el lote (operaciones multi-lote).</param>
    public static void ValidarEscritura(
        string? estadoOperativoLote,
        OperacionLoteEngordeLiquidado operacion,
        bool omitirGateLiquidado = false,
        string? loteNombre = null)
    {
        if (omitirGateLiquidado) return;
        if (EstaLiquidado(estadoOperativoLote))
            throw new InvalidOperationException(MensajeBloqueoCon(loteNombre));
    }
}
