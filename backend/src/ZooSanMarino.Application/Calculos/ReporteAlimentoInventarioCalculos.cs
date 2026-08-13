// src/ZooSanMarino.Application/Calculos/ReporteAlimentoInventarioCalculos.cs
// De qué módulo de inventario leen el ALIMENTO los reportes Contable y Técnico, y cómo se traduce
// un movimiento del módulo nuevo a las tres categorías que el reporte sabe mostrar.
// Puro (sin EF, sin estado): el service resuelve el flag de la empresa y delega.
namespace ZooSanMarino.Application.Calculos;

/// <summary>Las tres categorías de movimiento que muestran los reportes.</summary>
public enum CategoriaMovimientoAlimento
{
    /// <summary>Ni entra ni sale por operación (ajustes y correcciones): el reporte no las muestra.</summary>
    Ninguna = 0,
    /// <summary>Entró alimento a la granja (compra a planta, recepción de otra granja).</summary>
    Entrada = 1,
    /// <summary>Salió alimento hacia otra ubicación (traslado).</summary>
    Traslado = 2,
    /// <summary>Se consumió en la granja (retiro).</summary>
    Retiro = 3
}

/// <summary>
/// Los reportes Contable y Técnico leen el alimento de <c>farm_inventory_movements</c>, el módulo de
/// inventario <b>viejo</b>. Las empresas que ya operan sobre el módulo unificado
/// (<c>inventario_gestion_movimiento</c>) no tienen ni una fila ahí, así que sus columnas de
/// alimento salen en CERO — y el <c>catch { return 0; }</c> del Técnico lo devuelve en silencio,
/// igual que si no hubiera pasado nada.
///
/// <para>
/// <b>Por qué un flag por empresa y no un repunte para todos.</b> Cambiar la fuente de un reporte
/// contable vivo mueve números que alguien ya concilió: con el flag apagado la consulta es
/// exactamente la de siempre, y se enciende empresa por empresa, verificando. Es la misma regla que
/// el resto de las features por empresa: la señal es una columna tipada en <c>companies</c>, nombrada
/// por el COMPORTAMIENTO, nunca por el nombre del tenant.
/// </para>
/// </summary>
public static class ReporteAlimentoInventarioCalculos
{
    /// <summary>El reporte lee el módulo unificado solo si la empresa lo tiene declarado.</summary>
    public static bool LeeInventarioUnificado(bool companyReportesDesdeInventarioUnificado) =>
        companyReportesDesdeInventarioUnificado;

    /// <summary>
    /// Traduce el <c>movement_type</c> del módulo unificado a la categoría del reporte.
    ///
    /// <para>
    /// <b>Ajustes y eliminaciones quedan afuera a propósito</b> (<see cref="CategoriaMovimientoAlimento.Ninguna"/>):
    /// mueven el saldo pero no son ni una entrada de alimento ni un consumo del lote, y el reporte
    /// viejo tampoco los mostraba. Meterlos inflaría las entradas con correcciones de digitación.
    /// </para>
    ///
    /// <para>
    /// El traslado inter-granja cuenta en los dos extremos: la SALIDA es traslado para la granja que
    /// despacha y la ENTRADA es entrada para la que recibe — son dos filas, una por granja, así que
    /// no se duplica nada dentro de un mismo reporte.
    /// </para>
    /// </summary>
    public static CategoriaMovimientoAlimento Categoria(string? movementType) =>
        (movementType ?? string.Empty).Trim() switch
        {
            "Ingreso" => CategoriaMovimientoAlimento.Entrada,
            "TrasladoEntrada" => CategoriaMovimientoAlimento.Entrada,
            "TrasladoInterGranjaEntrada" => CategoriaMovimientoAlimento.Entrada,
            "TrasladoSalida" => CategoriaMovimientoAlimento.Traslado,
            "TrasladoInterGranjaSalida" => CategoriaMovimientoAlimento.Traslado,
            "TrasladoInterGranjaPendiente" => CategoriaMovimientoAlimento.Traslado,
            "Consumo" => CategoriaMovimientoAlimento.Retiro,
            _ => CategoriaMovimientoAlimento.Ninguna
        };

    /// <summary>Movimientos que el reporte considera una ENTRADA de alimento.</summary>
    public static readonly string[] TiposEntrada =
        ["Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada"];

    /// <summary>Movimientos que el reporte considera una SALIDA por traslado.</summary>
    public static readonly string[] TiposTraslado =
        ["TrasladoSalida", "TrasladoInterGranjaSalida", "TrasladoInterGranjaPendiente"];

    /// <summary>Movimientos que el reporte considera un RETIRO (consumo).</summary>
    public static readonly string[] TiposRetiro = ["Consumo"];

    /// <summary>
    /// Cantidad en <b>bultos</b>. El módulo unificado guarda kg (y a veces bultos); el reporte
    /// Contable muestra bultos, con el mismo factor que ya usaba con la tabla vieja.
    /// </summary>
    public static decimal ABultos(decimal cantidad, string? unidad, decimal factorKgPorBulto)
    {
        var u = (unidad ?? string.Empty).Trim().ToLowerInvariant();
        if (u is "bultos" or "bulto") return cantidad;
        return factorKgPorBulto == 0 ? 0 : cantidad / factorKgPorBulto;
    }
}
