namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Decide si el nombre de un lote choca con otro ya existente. Lógica PURA (sin EF): el service trae
/// los homónimos activos de la granja y acá se resuelve el alcance real de la unicidad.
///
/// <para><b>Por qué existe.</b> La guarda original (<c>REQ-009c</c>, 17-jul-2026) rechazaba el nombre
/// repetido dentro de <c>compañía + granja</c>. Pero la regla del negocio es más fina: un mismo nombre
/// de sublote <b>sí puede repetirse en galpones distintos</b> de la misma granja — es el patrón vivo en
/// producción (<c>A374A</c> en los galpones <c>G0326</c> y <c>G0324</c> de LA ESMERALDA, y <c>A374B</c>
/// en <c>G0325</c>/<c>G0323</c>; en la empresa 4, <c>LOTE 235A</c> en dos galpones). Con el alcance por
/// granja, ninguno de esos lotes podría volver a crearse. El selector de letra
/// (<c>GetLetrasDisponiblesAsync</c>) siempre trabajó por galpón: era la guarda la que estaba fuera de
/// fase con él.</para>
///
/// <para><b>Alcance de unicidad:</b> <c>compañía + granja + galpón + nombre</c>, entre lotes activos.
/// Los lotes sin galpón forman su propio grupo (no colisionan con los que sí lo tienen).</para>
/// </summary>
public static class LoteNombreDuplicadoCalculos
{
    /// <summary>Nombre listo para comparar/persistir: sin espacios en los extremos.</summary>
    public static string NormalizarNombre(string? nombre) => (nombre ?? string.Empty).Trim();

    /// <summary>
    /// Galpón normalizado para comparar: <c>Trim</c> y vacío ⇒ <c>null</c>. Un lote «sin galpón»
    /// llega indistintamente como <c>null</c>, <c>""</c> o <c>"   "</c> según el cliente.
    /// </summary>
    public static string? NormalizarGalpon(string? galponId)
    {
        var g = (galponId ?? string.Empty).Trim();
        return g.Length == 0 ? null : g;
    }

    /// <summary>
    /// <c>true</c> si dos lotes comparten galpón (ambos el mismo, o ambos sin galpón). Case-insensitive,
    /// igual que el resto de las comparaciones de <c>galpon_id</c> del módulo.
    /// </summary>
    public static bool MismoGalpon(string? galponA, string? galponB) =>
        string.Equals(NormalizarGalpon(galponA), NormalizarGalpon(galponB), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>true</c> si alguno de los homónimos activos ocupa el mismo galpón que el lote entrante.
    /// </summary>
    /// <param name="galponIdNuevo">Galpón del lote que se está creando o editando.</param>
    /// <param name="galponesHomonimosActivos">
    /// Galpones de los lotes activos de la misma compañía+granja que YA tienen ese nombre, excluido el
    /// propio lote cuando se está editando. Puede traer <c>null</c> (lotes sin galpón).
    /// </param>
    public static bool HayDuplicado(string? galponIdNuevo, IEnumerable<string?> galponesHomonimosActivos) =>
        galponesHomonimosActivos.Any(g => MismoGalpon(g, galponIdNuevo));

    /// <summary>
    /// Mensaje de rechazo. Nombra el alcance real del choque para que el operario sepa que la salida es
    /// usar otro galpón (o cambiar el nombre), no que el nombre esté «tomado» en toda la granja.
    /// </summary>
    public static string MensajeDuplicado(string? nombre, string? galponIdNuevo) =>
        NormalizarGalpon(galponIdNuevo) is null
            ? $"Ya existe un lote activo sin galpón con el nombre '{NormalizarNombre(nombre)}' en esta granja."
            : $"Ya existe un lote activo con el nombre '{NormalizarNombre(nombre)}' en este galpón.";
}
