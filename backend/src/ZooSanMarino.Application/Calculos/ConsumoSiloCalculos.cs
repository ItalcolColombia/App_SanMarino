// src/ZooSanMarino.Application/Calculos/ConsumoSiloCalculos.cs
// Fase C del plan de silos: de qué silo sale el alimento que descuenta el seguimiento diario.
// Puro (sin EF, sin estado): el service resuelve el flag de la empresa y los silos del lote, y delega.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Valida el silo de las claves de consumo del seguimiento diario (levante y producción).
///
/// <para>
/// <b>Por qué es una clase de cálculo y no un <c>if</c> en el service.</b> Igual que
/// <see cref="InventarioUbicacionSiloCalculos"/>, equivocarse acá no tira una excepción: descuenta el
/// silo equivocado y se descubre cuando el operario no encuentra su saldo. Los tests fijan que con el
/// flag apagado —toda clave con <c>SiloId = null</c>— el resultado sea <c>null</c> (sin error), es
/// decir el comportamiento previo a la Fase C, byte a byte.
/// </para>
///
/// <para>
/// La lista de silos habilitados sale de <c>lote_silos</c> (de qué silos consume ESE lote). Rechazar
/// un silo que no está asignado es lo que impide que un seguimiento descuente el alimento de otro
/// galpón: el silo pertenece a la granja, así que sin esta regla el descuento sería «válido» para el
/// inventario y falso para la operación.
/// </para>
/// </summary>
public static class ConsumoSiloCalculos
{
    /// <summary>Falta el silo en una empresa que ubica el inventario por silo.</summary>
    public const string MensajeSiloRequerido =
        "Debe indicar de qué silo o bodega sale cada alimento del seguimiento.";

    /// <summary>Llegó un silo en una empresa que ubica por núcleo/galpón.</summary>
    public const string MensajeSiloNoAplica =
        "Esta empresa no maneja el inventario por silo: no envíe el silo en los ítems del seguimiento.";

    /// <summary>El silo existe y es de la granja, pero el lote no consume de él.</summary>
    public static string MensajeSiloNoAsignadoAlLote(int siloId) =>
        $"El silo o bodega indicado (id={siloId}) no está asignado a este lote. " +
        "Asígnelo en «Silos de consumo del lote» antes de registrar el consumo.";

    /// <summary>Silos referidos por las claves (sin repetir), para resolverlos en UNA consulta.</summary>
    public static IReadOnlyCollection<int> SilosReferidos(IEnumerable<ItemConsumoKey> claves) =>
        claves.Where(k => k.SiloId is > 0).Select(k => k.SiloId!.Value).Distinct().ToArray();

    /// <summary>
    /// Devuelve el mensaje de error de la PRIMERA clave inválida, o <c>null</c> si todas son válidas.
    ///
    /// <para>
    /// Modo clásico: la única regla es <b>rechazar</b> un silo — dejarlo pasar escribiría un saldo por
    /// silo en una empresa que no lo maneja y lo partiría en dos. Modo por silo: toda clave tiene que
    /// traer un silo, y ese silo tiene que estar entre los del lote.
    /// </para>
    ///
    /// <para>
    /// Sin claves (un seguimiento sin ítems de consumo) no hay nada que validar en ningún modo: el
    /// seguimiento de un día sin alimento se sigue registrando igual.
    /// </para>
    /// </summary>
    /// <param name="silosHabilitados">Ids de <c>farm_silos</c> que el lote tiene asignados.</param>
    public static string? ValidarClaves(
        ModoUbicacionInventario modo,
        IEnumerable<ItemConsumoKey> claves,
        IReadOnlyCollection<int> silosHabilitados)
    {
        foreach (var clave in claves)
        {
            if (modo == ModoUbicacionInventario.Clasico)
            {
                if (clave.SiloId.HasValue) return MensajeSiloNoAplica;
                continue;
            }

            if (clave.SiloId is not > 0) return MensajeSiloRequerido;
            if (!silosHabilitados.Contains(clave.SiloId.Value))
                return MensajeSiloNoAsignadoAlLote(clave.SiloId.Value);
        }

        return null;
    }
}
