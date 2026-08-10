namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Reglas PURAS del descuento y la acumulación de stock de inventario (items A1 y A2 de
/// <c>fase_de_desarrollo/f0a_stock_atomico_plan.md</c>).
///
/// <para>
/// <b>Por qué estas cuatro líneas viven acá y no adentro del service.</b> El defecto que arreglan es
/// una condición de carrera: dos consumos concurrentes sobre el mismo stock pasaban los dos la
/// validación <c>if (stock.Quantity &lt; req.Quantity) throw</c> y el saldo terminaba en negativo. La
/// corrección mueve la decisión a la base (<c>UPDATE ... WHERE quantity &gt;= @q</c>), y lo que queda
/// del lado de C# es <b>interpretar el resultado</b>: cero filas afectadas significa rechazo. Esa
/// interpretación es exactamente donde un futuro refactor puede equivocarse en silencio —tratar el 0
/// como "no pasó nada" en vez de "no había saldo"— y por eso tiene tests propios.
/// </para>
/// </summary>
public static class StockAtomicoCalculos
{
    /// <summary>
    /// Mensaje de rechazo por saldo insuficiente. <b>No cambiar el texto:</b> el front lo muestra tal
    /// cual al usuario y hay flujos (carga masiva, seguimientos) que lo comparan para decidir si
    /// degradan el error o lo propagan.
    /// </summary>
    public const string MensajeStockInsuficiente = "No hay stock suficiente para el consumo.";

    /// <summary>Mensaje de rechazo por cantidad no positiva, tal como estaba antes del cambio.</summary>
    public const string MensajeCantidadNoPositiva = "La cantidad de consumo debe ser positiva.";

    /// <summary>
    /// ¿La cantidad pedida es válida para operar? Se evalúa <b>antes</b> de tocar la base: un
    /// <c>UPDATE ... quantity - 0</c> afectaría una fila y se leería como éxito, y una cantidad
    /// negativa <i>sumaría</i> stock por un camino que dice "descontar".
    /// </summary>
    public static bool EsCantidadOperable(decimal cantidad) => cantidad > 0m;

    /// <summary>
    /// Interpreta el resultado del <c>UPDATE</c> condicional de descuento.
    ///
    /// <para>
    /// <c>UPDATE ... SET quantity = quantity - @q WHERE id = @id AND quantity &gt;= @q</c> afecta
    /// <b>1</b> fila si había saldo y <b>0</b> si no lo había —o si la fila ya no existe—. Los dos
    /// casos de 0 se tratan igual a propósito: para el usuario, "el stock desapareció mientras
    /// operabas" y "no alcanzaba" son el mismo hecho y la misma acción correctiva.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> si el descuento se aplicó.</returns>
    public static bool DescuentoAplicado(int filasAfectadas) => filasAfectadas > 0;

    /// <summary>
    /// Normaliza un componente de ubicación (núcleo o galpón) a la forma que usa la clave natural.
    ///
    /// <para>
    /// <b>Esta función es el espejo en C# del <c>COALESCE(nucleo_id,'')</c> del índice único</b>, y
    /// existe porque la discrepancia entre las dos formas es justo la que rompe. En Postgres, dentro
    /// de un índice único, <c>NULL</c> nunca es igual a otro <c>NULL</c>: sin el <c>COALESCE</c>, dos
    /// filas de stock a nivel granja (núcleo y galpón nulos — todo el modelo de Colombia y de las
    /// granjas con <c>maneja_alimento_por_galpon = false</c>) se podrían duplicar igual, que es
    /// exactamente el bug que A1 viene a cerrar. Y del lado de C#, <c>null</c> y <c>""</c> tienen que
    /// colapsar a la misma clave o el código creería que son ubicaciones distintas mientras la base
    /// dice que son la misma.
    /// </para>
    /// </summary>
    public static string NormalizarComponenteUbicacion(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();

    /// <summary>
    /// Clave natural del stock, en la misma forma que la del índice único de la base.
    /// Sirve para agrupar/deduplicar en memoria con el mismo criterio que aplica Postgres.
    /// </summary>
    public static (int FarmId, int ItemId, string Nucleo, string Galpon) ClaveNatural(
        int farmId,
        int itemInventarioId,
        string? nucleoId,
        string? galponId) =>
        (farmId,
         itemInventarioId,
         NormalizarComponenteUbicacion(nucleoId),
         NormalizarComponenteUbicacion(galponId));
}
