// src/ZooSanMarino.Application/Calculos/InventarioUbicacionSiloCalculos.cs
// Decisión PURA de dónde vive el inventario: por núcleo/galpón (todas las empresas) o por silo/bodega
// (las que tienen maneja_inventario_por_silo). Sin EF, sin estado: el service resuelve el flag y delega.
namespace ZooSanMarino.Application.Calculos;

/// <summary>Cómo ubica el inventario una empresa.</summary>
public enum ModoUbicacionInventario
{
    /// <summary>Núcleo + galpón (o solo granja). Es el modelo de Ecuador, Panamá y Colombia.</summary>
    Clasico = 0,

    /// <summary>Silo o bodega de la granja. Santa Reyes: el galpón NO mueve alimento.</summary>
    PorSilo = 1
}

/// <summary>
/// Dónde vive el stock de un movimiento, según el flag de la empresa.
///
/// <para>
/// <b>Por qué es una clase de cálculo y no un <c>if</c> adentro del service.</b> La regla decide
/// sobre qué fila de <c>inventario_gestion_stock</c> se suma o se resta: equivocarla no tira una
/// excepción, mueve el saldo equivocado y se descubre semanas después. Acá es determinista y está
/// cubierta por tests que fijan, con el flag apagado, un comportamiento <b>idéntico byte a byte</b>
/// al previo a la Fase B —mensajes de error incluidos—.
/// </para>
///
/// <para>
/// <b>La decisión de fondo</b> (plan §4.4, tomada con el dato del cliente): un silo puede alimentar a
/// <b>varios galpones</b>, así que el saldo no puede llevarse por <c>(galpón, silo)</c> —partiría el
/// contenido físico de un mismo silo en dos—. Por eso en modo <see cref="ModoUbicacionInventario.PorSilo"/>
/// el núcleo y el galpón se anulan: viajan en el request solo para filtrar qué silos ofrecer.
/// </para>
/// </summary>
public static class InventarioUbicacionSiloCalculos
{
    /// <summary>Falta el silo en una empresa que ubica por silo.</summary>
    public const string MensajeSiloRequerido =
        "Debe indicar el silo o la bodega donde queda el movimiento.";

    /// <summary>Llegó un silo en una empresa que ubica por núcleo/galpón.</summary>
    public const string MensajeSiloNoAplica =
        "Esta empresa no maneja el inventario por silo: no envíe el silo en el movimiento.";

    /// <summary>Silo con id no positivo (0 o negativo llegando desde un form a medio llenar).</summary>
    public const string MensajeSiloInvalido =
        "El silo o bodega indicado no es válido.";

    /// <summary>El modo sale del flag de la empresa y de nada más (nunca de un país ni de un nombre).</summary>
    public static ModoUbicacionInventario ResolverModo(bool companyManejaInventarioPorSilo) =>
        companyManejaInventarioPorSilo ? ModoUbicacionInventario.PorSilo : ModoUbicacionInventario.Clasico;

    /// <summary>
    /// Valida la ubicación pedida. Devuelve el mensaje de error, o <c>null</c> si es válida.
    ///
    /// <para>
    /// En modo <see cref="ModoUbicacionInventario.Clasico"/> la única regla nueva es <b>rechazar</b> un
    /// silo: dejarlo pasar mezclaría los dos modelos en la misma tabla y partiría el saldo de una
    /// empresa que no pidió nada. Todas las demás validaciones del modo clásico (alimento exige
    /// núcleo+galpón, etc.) siguen donde estaban, intactas.
    /// </para>
    ///
    /// <para>
    /// En modo <see cref="ModoUbicacionInventario.PorSilo"/> el silo es obligatorio <b>para todo tipo de
    /// ítem</b>, alimento e insumos (decisión del usuario, plan §9.2): en el ERP del cliente hasta la
    /// bodega es una ubicación con stock propio, así que no hay movimiento «a nivel granja».
    /// </para>
    /// </summary>
    /// <param name="esAlimento">
    /// Se recibe para dejar explícito que <b>no cambia la decisión</b>: en modo por silo el trato es el
    /// mismo para alimento e insumos. Sin el parámetro, el próximo lector asumiría que falta el caso.
    /// </param>
    public static string? ValidarUbicacion(
        ModoUbicacionInventario modo,
        int? siloId,
        string? galponId,
        bool esAlimento)
    {
        _ = galponId;   // el galpón no participa de esta decisión: filtra la lista de silos, nada más
        _ = esAlimento;

        if (modo == ModoUbicacionInventario.Clasico)
            return siloId.HasValue ? MensajeSiloNoAplica : null;

        if (!siloId.HasValue) return MensajeSiloRequerido;
        return siloId.Value <= 0 ? MensajeSiloInvalido : null;
    }

    /// <summary>
    /// Ubicación con la que se persiste el stock y el movimiento.
    ///
    /// <para>
    /// Modo clásico: se devuelve lo que entró, sin tocar (incluidos los <c>Trim</c> que ya hacía el
    /// service). Modo por silo: <b>núcleo y galpón se anulan</b> y manda el silo.
    /// </para>
    ///
    /// <para>
    /// Asume que <see cref="ValidarUbicacion"/> ya pasó; si igual llegara un modo por silo sin silo,
    /// devuelve <c>SiloId = null</c> y la ubicación clásica en vez de inventar una: el llamador ya
    /// rechazó, y adivinar acá escribiría un saldo en una ubicación que nadie pidió.
    /// </para>
    /// </summary>
    public static (string? NucleoId, string? GalponId, int? SiloId) NormalizarUbicacion(
        ModoUbicacionInventario modo,
        string? nucleoId,
        string? galponId,
        int? siloId)
    {
        if (modo == ModoUbicacionInventario.Clasico)
            return (nucleoId, galponId, null);

        if (!siloId.HasValue || siloId.Value <= 0)
            return (nucleoId, galponId, null);

        return (null, null, siloId);
    }
}
