// src/ZooSanMarino.Application/Calculos/MigracionAlimentoCalculos.cs
// Lógica PURA de la hoja "Alimento" de la carga masiva de seguimiento pollo engorde: normalización del
// tipo de movimiento y del origen, y la SIMULACIÓN del balance de inventario (stock actual + entradas
// del archivo − consumos del archivo) para poder rechazar en el dry-run lo que hoy fallaba en silencio.
// Sin EF ni estado: el service resuelve ids/nombres y delega acá.
namespace ZooSanMarino.Application.Calculos;

/// <summary>Tipo de movimiento de la hoja "Alimento".</summary>
public enum MovimientoAlimento
{
    /// <summary>Entrada al inventario desde planta, bodega u otra granja.</summary>
    Ingreso,
    /// <summary>Salida de una ubicación hacia otra (galpón→galpón en la misma granja, o granja→granja vía tránsito).</summary>
    Traslado,
    /// <summary>Aceptación de un traslado inter-granja que quedó en tránsito.</summary>
    Recepcion,
    /// <summary>
    /// Salida de alimento del galpón que NO nace de un día de seguimiento. Espeja el consumo manual
    /// que ya existe en la pantalla de gestión de inventario (<c>POST /inventario-gestion/consumo</c>).
    /// <para>
    /// Su caso es el alimento que salió de verdad pero cuyo registro diario no lo descontó: la primera
    /// semana del lote, que se digita en reproductora y queda CONFIRMADA (inmutable — corregirla exige
    /// reabrir el lote), y en general cualquier histórico cargado antes de que el consumo descontara
    /// inventario. Sin esto el galpón arrastra para siempre un sobrante que no existe.
    /// </para>
    /// </summary>
    Consumo
}

/// <summary>
/// Ubicación de stock de alimento. En Ecuador/Panamá el alimento vive a nivel galpón; en Colombia (o
/// en una granja con <c>maneja_alimento_por_galpon = false</c>) núcleo y galpón van en null.
///
/// <para>
/// <b>Empresas con <c>maneja_inventario_por_silo</c> (Santa Reyes):</b> el stock vive en el SILO y
/// núcleo/galpón se anulan — misma decisión que toma
/// <see cref="InventarioUbicacionSiloCalculos.NormalizarUbicacion"/> para el alta manual. Un silo
/// alimenta a varios galpones, así que llevar el saldo por (galpón, silo) partiría en dos el
/// contenido físico de un mismo silo.
/// </para>
/// </summary>
public readonly record struct UbicacionAlimento(int FarmId, string? NucleoId, string? GalponId, int? SiloId = null)
{
    /// <summary>Normaliza null/espacios a cadena vacía para que la clave sea comparable.</summary>
    public UbicacionAlimento Normalizada() => new(
        FarmId,
        string.IsNullOrWhiteSpace(NucleoId) ? null : NucleoId.Trim(),
        string.IsNullOrWhiteSpace(GalponId) ? null : GalponId.Trim(),
        SiloId is > 0 ? SiloId : null);

    /// <summary>
    /// La misma ubicación resuelta para el modo de inventario de la empresa. En modo por silo se
    /// anulan núcleo y galpón; en modo clásico se descarta cualquier silo que se haya colado.
    /// Espeja <see cref="InventarioUbicacionSiloCalculos.NormalizarUbicacion"/>.
    /// </summary>
    public UbicacionAlimento ParaModo(ModoUbicacionInventario modo)
    {
        var (nucleo, galpon, silo) = InventarioUbicacionSiloCalculos.NormalizarUbicacion(
            modo, NucleoId, GalponId, SiloId);
        return new UbicacionAlimento(FarmId, nucleo, galpon, silo).Normalizada();
    }
}

/// <summary>Una posición del balance: qué ítem, en qué ubicación.</summary>
public readonly record struct PosicionAlimento(UbicacionAlimento Ubicacion, int ItemId);

/// <summary>Faltante detectado por la simulación: cuánto se consume de más sobre lo disponible.</summary>
/// <param name="Posicion">Ubicación + ítem donde falta stock.</param>
/// <param name="Disponible">Stock inicial + entradas del archivo.</param>
/// <param name="Requerido">Total que el archivo intenta consumir/sacar.</param>
public sealed record FaltanteAlimento(PosicionAlimento Posicion, decimal Disponible, decimal Requerido)
{
    /// <summary>Kilos que faltan (siempre &gt; 0 cuando el registro existe).</summary>
    public decimal Faltante => Requerido - Disponible;
}

/// <summary>Saldo proyectado de una posición al terminar de aplicar el archivo.</summary>
public sealed record SaldoAlimentoProyectado(PosicionAlimento Posicion, decimal SaldoInicial, decimal Entradas, decimal Salidas)
{
    public decimal SaldoFinal => SaldoInicial + Entradas - Salidas;
}

public static class MigracionAlimentoCalculos
{
    /// <summary>Movimiento por defecto cuando la columna viene vacía: el caso masivo es cargar entradas.</summary>
    public const MovimientoAlimento MovimientoPorDefecto = MovimientoAlimento.Ingreso;

    /// <summary>Origen por defecto de un ingreso (mismo default que la pantalla de gestión de inventario).</summary>
    public const string OrigenPorDefecto = "planta";

    /// <summary>
    /// Interpreta la columna "Movimiento" (sin mayúsculas/acentos). Vacío ⇒ <see cref="MovimientoPorDefecto"/>.
    /// Devuelve false si el texto no corresponde a ningún movimiento conocido.
    /// </summary>
    public static bool TryMovimiento(string? texto, out MovimientoAlimento movimiento)
    {
        movimiento = MovimientoPorDefecto;
        var k = MigracionCalculos.NormalizarClave(texto);
        if (string.IsNullOrEmpty(k)) return true;

        switch (k)
        {
            case "ingreso":
            case "ingresos":
            case "entrada":
            case "entradas":
            case "compra":
                movimiento = MovimientoAlimento.Ingreso;
                return true;
            case "traslado":
            case "traslados":
            case "transferencia":
            case "salida":
                movimiento = MovimientoAlimento.Traslado;
                return true;
            case "recepcion":
            case "recepciones":
            case "recibo":
            case "recibido":
            case "ingreso por traslado":
            case "entrada por traslado":
                movimiento = MovimientoAlimento.Recepcion;
                return true;
            case "consumo":
            case "consumos":
            case "consumido":
                movimiento = MovimientoAlimento.Consumo;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Interpreta la columna "Origen" de un ingreso: planta (default) | bodega | granja. Devuelve false
    /// si el texto no es ninguno de los tres.
    /// </summary>
    public static bool TryOrigenIngreso(string? texto, out string origen)
    {
        origen = OrigenPorDefecto;
        var k = MigracionCalculos.NormalizarClave(texto);
        if (string.IsNullOrEmpty(k)) return true;

        switch (k)
        {
            case "planta": origen = "planta"; return true;
            case "bodega": origen = "bodega"; return true;
            case "granja":
            case "otra granja": origen = "granja"; return true;
            default: return false;
        }
    }

    /// <summary>
    /// Simula el archivo completo sobre el stock actual y devuelve las posiciones que quedarían en
    /// negativo.
    /// <para>
    /// El punto: <c>RegistrarConsumoAsync</c> valida contra el stock ACUMULADO, y el llamador del
    /// seguimiento se traga la excepción en un catch que solo loguea — el día se guarda y el inventario
    /// queda mal sin que nadie se entere. Corriendo esta simulación ANTES de insertar, el archivo se
    /// rechaza entero con el faltante exacto.
    /// </para>
    /// <para>
    /// Se compara el TOTAL de entradas contra el TOTAL de salidas por posición (no día a día): las
    /// entradas del archivo se aplican antes que los consumos, así que el orden cronológico dentro del
    /// archivo no puede dejar el stock negativo aunque las llegadas estén fechadas después del consumo
    /// (caso real: alimento facturado con días de atraso respecto del consumo del galpón).
    /// </para>
    /// </summary>
    /// <param name="stockActual">Stock hoy en BD por posición (las posiciones ausentes valen 0).</param>
    /// <param name="entradas">Entradas que agrega el archivo (ingresos y destinos de traslado/recepción).</param>
    /// <param name="salidas">Salidas que agrega el archivo (consumos del seguimiento y orígenes de traslado).</param>
    public static IReadOnlyList<FaltanteAlimento> Simular(
        IReadOnlyDictionary<PosicionAlimento, decimal> stockActual,
        IReadOnlyDictionary<PosicionAlimento, decimal> entradas,
        IReadOnlyDictionary<PosicionAlimento, decimal> salidas)
    {
        var faltantes = new List<FaltanteAlimento>();
        foreach (var (posicion, requerido) in salidas)
        {
            if (requerido <= 0) continue;
            var disponible = Valor(stockActual, posicion) + Valor(entradas, posicion);
            if (disponible < requerido)
                faltantes.Add(new FaltanteAlimento(posicion, disponible, requerido));
        }
        return faltantes;
    }

    /// <summary>
    /// Saldo proyectado de cada posición tocada por el archivo (para mostrarlo en el reporte del
    /// dry-run: es la cifra que el usuario compara contra su planilla de inventario).
    /// </summary>
    public static IReadOnlyList<SaldoAlimentoProyectado> Proyectar(
        IReadOnlyDictionary<PosicionAlimento, decimal> stockActual,
        IReadOnlyDictionary<PosicionAlimento, decimal> entradas,
        IReadOnlyDictionary<PosicionAlimento, decimal> salidas)
    {
        var posiciones = new HashSet<PosicionAlimento>(entradas.Keys);
        foreach (var p in salidas.Keys) posiciones.Add(p);

        return posiciones
            .Select(p => new SaldoAlimentoProyectado(p, Valor(stockActual, p), Valor(entradas, p), Valor(salidas, p)))
            .OrderBy(s => s.Posicion.Ubicacion.FarmId)
            .ThenBy(s => s.Posicion.Ubicacion.GalponId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(s => s.Posicion.Ubicacion.SiloId ?? 0)
            .ThenBy(s => s.Posicion.ItemId)
            .ToList();
    }

    /// <summary>Acumula una cantidad en el diccionario de una posición (helper de armado).</summary>
    public static void Acumular(IDictionary<PosicionAlimento, decimal> destino, PosicionAlimento posicion, decimal cantidad)
    {
        if (cantidad == 0) return;
        destino[posicion] = Valor(destino, posicion) + cantidad;
    }

    private static decimal Valor(IEnumerable<KeyValuePair<PosicionAlimento, decimal>> mapa, PosicionAlimento posicion)
    {
        if (mapa is IReadOnlyDictionary<PosicionAlimento, decimal> ro)
            return ro.TryGetValue(posicion, out var v) ? v : 0m;
        if (mapa is IDictionary<PosicionAlimento, decimal> d)
            return d.TryGetValue(posicion, out var v2) ? v2 : 0m;
        return 0m;
    }

    /// <summary>
    /// Clave de idempotencia de un movimiento de alimento: mismo ítem, misma ubicación, misma fecha,
    /// misma cantidad y misma referencia ⇒ es el mismo movimiento ya cargado, se omite (no duplica
    /// stock). Espeja la idempotencia por (lote, fecha) que ya tiene el seguimiento.
    /// </summary>
    public static string ClaveIdempotencia(
        MovimientoAlimento movimiento, UbicacionAlimento destino, int itemId, DateTime fecha, decimal cantidadKg, string? referencia)
    {
        var u = destino.Normalizada();
        var refN = MigracionCalculos.NormalizarClave(referencia);
        // 🔴 El segmento del silo se agrega SOLO cuando hay silo. Agregarlo siempre (aunque fuera
        // vacío) cambiaría la clave de TODAS las filas ya cargadas por empresas sin silo, y un
        // reimport volvería a aplicar movimientos que ya estaban: la clave se recomputa de la BD en
        // ClavesMovimientosExistentesAsync, así que los dos lados tienen que rendir el mismo string.
        var silo = u.SiloId is > 0 ? $"|silo{u.SiloId.Value}" : string.Empty;
        return string.Join('|',
            movimiento.ToString(),
            u.FarmId.ToString(),
            u.NucleoId ?? "",
            u.GalponId ?? "",
            itemId.ToString(),
            fecha.ToString("yyyy-MM-dd"),
            // 3 decimales con formato FIJO: es la precisión con la que el resto del módulo compara
            // kilos. El formato importa — decimal conserva la escala, así que el 2717.5 que viene del
            // Excel y el 2717.500 que devuelve la columna numeric son el MISMO número pero
            // ToString() los escribe distinto, y el movimiento se volvía a aplicar en cada reintento.
            decimal.Round(cantidadKg, 3).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
            refN) + silo;
    }
}
