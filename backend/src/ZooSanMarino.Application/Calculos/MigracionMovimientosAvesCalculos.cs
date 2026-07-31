// src/ZooSanMarino.Application/Calculos/MigracionMovimientosAvesCalculos.cs
// Reglas PURAS de la hoja "Movimientos Aves" de la carga masiva de Seguimiento LEVANTE.
//
// La hoja registra traslados de aves UNILATERALES sobre el lote del archivo (decisión del usuario,
// jul-2026): una SALIDA descuenta a ESTE lote y solo VALIDA que el lote contraparte exista (no lo
// acredita — ese lote carga su propio Ingreso en su propio archivo); un INGRESO acredita a ESTE lote
// sin tocar al lote origen (aves "en tránsito"). Así dos archivos, uno por lote, nunca doble-cuentan.
namespace ZooSanMarino.Application.Calculos;

/// <summary>Tipo de movimiento de aves aceptado por la hoja "Movimientos Aves" (levante).</summary>
public enum MovimientoAvesMigracion
{
    /// <summary>Aves que SALEN de este lote hacia otro (descuenta acá; al destino no lo toca).</summary>
    Salida,
    /// <summary>Aves RECIBIDAS en tránsito (acredita acá; al origen no lo toca).</summary>
    Ingreso
}

/// <summary>
/// Saldo de aves del lote proyectado tras aplicar el archivo completo. Es INFORMATIVO (el descuento
/// real satura en 0 igual que la fn de seguimiento): un negativo se reporta como Advertencia, no
/// bloquea la carga de un histórico.
/// </summary>
public sealed record SaldoAvesProyectado(int Hembras, int Machos)
{
    public bool AlgunoNegativo => Hembras < 0 || Machos < 0;
}

public static class MigracionMovimientosAvesCalculos
{
    /// <summary>
    /// Interpreta la celda "Tipo" con sinónimos, sin acentos ni mayúsculas
    /// (<see cref="MigracionCalculos.NormalizarClave"/>). Celda vacía o texto no reconocido ⇒ false
    /// (a diferencia de la hoja Alimento no hay default: acá el tipo cambia el SIGNO del movimiento).
    /// </summary>
    public static bool TryMovimiento(string? texto, out MovimientoAvesMigracion tipo)
    {
        tipo = default;
        switch (MigracionCalculos.NormalizarClave(texto))
        {
            case "salida":
            case "salidas":
            case "traslado salida":
            case "salida traslado":
            case "salida de aves":
            case "envio":
                tipo = MovimientoAvesMigracion.Salida;
                return true;

            case "ingreso":
            case "ingresos":
            case "entrada":
            case "entradas":
            case "traslado ingreso":
            case "ingreso traslado":
            case "ingreso de aves":
            case "ingreso en transito":
            case "en transito":
            case "transito":
                tipo = MovimientoAvesMigracion.Ingreso;
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Proyección del saldo del lote tras el archivo:
    /// <c>actual − bajas nuevas del archivo (mort+sel+error) − salidas + ingresos</c>, por sexo.
    /// </summary>
    public static SaldoAvesProyectado ProyectarSaldoAves(
        int avesHActual, int avesMActual,
        int bajasArchivoH, int bajasArchivoM,
        int salidasH, int salidasM,
        int ingresosH, int ingresosM) =>
        new(avesHActual - bajasArchivoH - salidasH + ingresosH,
            avesMActual - bajasArchivoM - salidasM + ingresosM);

    /// <summary>
    /// Clave de duplicado DENTRO del archivo (misma fecha + tipo + cantidades). Dos viajes idénticos
    /// el mismo día deben cargarse como UNA fila sumada: si fueran dos filas, al reimportar la
    /// idempotencia contra <c>movimiento_aves</c> no podría distinguir cuántas ya se aplicaron.
    /// </summary>
    public static string ClaveArchivo(DateTime fecha, MovimientoAvesMigracion tipo, int hembras, int machos) =>
        FormattableString.Invariant($"{fecha:yyyy-MM-dd}|{tipo}|{hembras}|{machos}");
}
