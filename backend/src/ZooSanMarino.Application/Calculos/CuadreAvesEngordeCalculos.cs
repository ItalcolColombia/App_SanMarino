namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Regla PURA que decide si el encaset de un lote de engorde debe alinearse a su registro
/// <c>Inicio</c> del historial. Es el espejo ejecutable del SQL de la migración
/// <c>20260818050000_CorreccionEncasetLoteSinReferenciaConfiable</c>: acá se puede probar sin BD.
///
/// <para>
/// El detector <c>fn_cuadre_aves_engorde</c> marca <c>referencia_confiable = false</c> cuando el
/// total del <c>Inicio</c> no empata con <c>aves_encasetadas</c>. Esa desalineación deja al lote
/// fuera de toda auditoría de conservación, pero <b>no cualquier desalineación se corrige sola</b>:
/// hace falta que el gap del encaset se explique EXACTAMENTE por el desfase del maestro. Si no
/// coincide, la causa es otra y el lote no se toca.
/// </para>
/// </summary>
public static class CuadreAvesEngordeCalculos
{
    /// <summary>Lo que el detector ve de un lote.</summary>
    /// <param name="TieneInicio">Si existe registro <c>Inicio</c> en el historial.</param>
    /// <param name="InicioTotal">Hembras + machos + mixtas del <c>Inicio</c>.</param>
    /// <param name="AvesEncasetadas">El campo del maestro.</param>
    /// <param name="DesfaseH">Maestro menos esperado, en hembras.</param>
    /// <param name="DesfaseM">Maestro menos esperado, en machos.</param>
    public readonly record struct EstadoLote(
        bool TieneInicio,
        int InicioTotal,
        int AvesEncasetadas,
        int DesfaseH,
        int DesfaseM);

    /// <summary>Qué escribir. <c>RestaH</c>/<c>RestaM</c> se descuentan del maestro.</summary>
    public readonly record struct Correccion(int AvesEncasetadas, int RestaH, int RestaM);

    /// <summary>
    /// Devuelve la corrección a aplicar, o <c>null</c> si el lote no entra en la regla.
    /// Es idempotente: un lote ya alineado devuelve <c>null</c>.
    /// </summary>
    public static Correccion? Resolver(EstadoLote e)
    {
        // Sin Inicio no hay referencia contra la cual corregir.
        if (!e.TieneInicio || e.InicioTotal <= 0) return null;

        // Un desfase negativo significa que el maestro tiene MENOS de lo esperado: es otra causa
        // (bajas de más, traslados sin registrar), y restarle al maestro lo empeoraría.
        if (e.DesfaseH < 0 || e.DesfaseM < 0) return null;

        // El corazón de la guarda: el sobrante del encaset tiene que ser exactamente el sobrante
        // del maestro. Si no, el lote está descuadrado por algo que esta regla no explica.
        if (e.AvesEncasetadas - e.InicioTotal != e.DesfaseH + e.DesfaseM) return null;

        // Ya alineado ⇒ nada que hacer (idempotencia).
        if (e.AvesEncasetadas == e.InicioTotal && e.DesfaseH == 0 && e.DesfaseM == 0) return null;

        return new Correccion(e.InicioTotal, e.DesfaseH, e.DesfaseM);
    }
}
