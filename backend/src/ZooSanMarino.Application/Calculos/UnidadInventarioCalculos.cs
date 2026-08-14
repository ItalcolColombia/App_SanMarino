// src/ZooSanMarino.Application/Calculos/UnidadInventarioCalculos.cs
// Cálculo PURO: qué unidad de medida le corresponde a una fila de inventario.

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Una sola unidad por ítem, y la dueña es el <b>catálogo</b> (<c>item_inventario_ecuador.unidad</c>).
///
/// <para>
/// <b>El defecto que corrige (TK-2026-000019).</b> <c>inventario_gestion_stock</c> tiene su propia
/// columna <c>unit</c> con <c>DEFAULT 'kg'</c>, y ningún camino de escritura la sincronizaba con la
/// unidad que el usuario elige al crear el ítem: el ingreso grababa <c>req.Unit ?? "kg"</c>, el
/// <c>ON CONFLICT</c> del upsert no pisaba la unidad de una fila existente, y el consumo de Colombia
/// manda <c>"kg"</c> fijo. Resultado en producción: 145 de 569 filas de stock mostraban <c>kg</c>
/// para productos que el catálogo tiene en litros, mililitros o unidades — y operación las venía
/// corrigiendo <b>a mano</b> desde el modal de ajuste, que aceptaba texto libre. De ahí que en la
/// misma base convivan <c>LT</c>, <c>UND</c>, <c>GALONES</c>, <c>Gr</c>, <c>Ml</c> y <c>DOSIS</c>
/// con el vocabulario cerrado del catálogo.
/// </para>
///
/// <para>
/// ⚠️ La unidad es una <b>etiqueta</b>: nada convierte cantidades por cambiarla. La aritmética de
/// alimento (<c>fn_seguimiento_diario_engorde</c>, <c>SaldoAlimentoEngordeAplicador</c>) es siempre
/// en kilos y todo ítem de alimento está en <c>kg</c> en el catálogo, así que este resolutor jamás
/// mueve un saldo.
/// </para>
/// </summary>
public static class UnidadInventarioCalculos
{
    /// <summary>Última red: es el <c>DEFAULT</c> histórico de la columna y de la entidad.</summary>
    public const string UnidadPorDefecto = "kg";

    /// <summary>
    /// La unidad que corresponde grabar/mostrar: <b>manda el catálogo</b>. Solo si el ítem no tiene
    /// unidad (imposible hoy: la columna es <c>NOT NULL</c>, pero el resolutor no depende de eso) se
    /// cae a la que pidió el llamador, y recién después a <c>kg</c>.
    /// </summary>
    /// <param name="unidadCatalogo">La del ítem (<c>item_inventario_ecuador.unidad</c>).</param>
    /// <param name="unidadSolicitada">La que traía el request o la fila previa. Es un respaldo, no una opción.</param>
    public static string Resolver(string? unidadCatalogo, string? unidadSolicitada = null)
    {
        if (!string.IsNullOrWhiteSpace(unidadCatalogo))
            return unidadCatalogo.Trim();

        if (!string.IsNullOrWhiteSpace(unidadSolicitada))
            return unidadSolicitada.Trim();

        return UnidadPorDefecto;
    }

    /// <summary>
    /// ¿Esta fila está desalineada del catálogo? Compara sin distinguir mayúsculas ni espacios,
    /// porque las correcciones manuales entraron con la capitalización que tipeó cada persona
    /// (<c>LT</c> vs <c>l</c>).
    /// </summary>
    public static bool EstaDesalineada(string? unidadFila, string? unidadCatalogo) =>
        !string.Equals(
            (unidadFila ?? string.Empty).Trim(),
            Resolver(unidadCatalogo, unidadFila),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Lleva una unidad tipeada a mano al vocabulario del catálogo
    /// (<c>kg, und, l, ml, g, lb, saco, dosis, gal</c>).
    ///
    /// <para>
    /// Lo usa el <b>backfill</b> para promover al catálogo la corrección que operación había escrito
    /// en la fila de stock. No participa de ningún camino de escritura en caliente: ahí la unidad
    /// sale del catálogo, que ya está en el vocabulario correcto.
    /// </para>
    /// </summary>
    /// <returns>La unidad normalizada, o <c>null</c> si viene vacía.</returns>
    public static string? Normalizar(string? unidad)
    {
        if (string.IsNullOrWhiteSpace(unidad)) return null;

        var u = unidad.Trim().ToLowerInvariant();
        return u switch
        {
            "lt" or "lts" or "litro" or "litros" => "l",
            "ml" or "mls" or "mililitro" or "mililitros" => "ml",
            "gr" or "grs" or "gramo" or "gramos" => "g",
            "un" or "unidad" or "unidades" or "und" => "und",
            "galon" or "galón" or "galones" or "gal" => "gal",
            "dosis" or "ds" => "dosis",
            "sacos" => "saco",
            "kilo" or "kilos" or "kgs" => "kg",
            _ => u
        };
    }
}
