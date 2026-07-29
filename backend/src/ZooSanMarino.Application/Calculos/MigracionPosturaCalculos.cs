// src/ZooSanMarino.Application/Calculos/MigracionPosturaCalculos.cs
// Lógica PURA de la carga masiva de POSTURA (Levante + Producción): a qué posición de stock va el
// alimento de un lote, cuándo un consumo descuenta inventario, validación de etapa y resolución de
// los totales de huevos entre las tres fuentes posibles (totales explícitos, 11 categorías, ítems del
// catálogo). Sin EF ni estado: el service resuelve ids/flags y delega acá.
//
// Por qué existe: la carga masiva de postura escribía el consumo como un número suelto y NADIE lo
// descontaba del inventario, mientras el alta manual sí valida stock y descuenta. Estas funciones son
// las decisiones que había que tomar para cerrar esa brecha sin cambiar el comportamiento de los
// archivos que ya están en uso.
using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

public static class MigracionPosturaCalculos
{
    // ── Etapa (Producción) ────────────────────────────────────────────────────────────────────────
    /// <summary>Etapa mínima aceptada por el alta manual (<c>[Range(1,3)]</c> en <c>CrearSeguimientoRequest</c>).</summary>
    public const int EtapaMinima = 1;

    /// <summary>Etapa máxima aceptada por el alta manual.</summary>
    public const int EtapaMaxima = 3;

    /// <summary>
    /// Etapa del día. <c>null</c> (celda vacía) es válido — la función SQL aplica el default 1, igual
    /// que hoy. Un valor fuera de [1,3] NO es válido: el modal lo rechaza y la carga masiva lo aceptaba
    /// en silencio, dejando filas que ningún reporte por etapa podía ubicar.
    /// </summary>
    public static bool EtapaValida(int? etapa) => etapa is null || (etapa >= EtapaMinima && etapa <= EtapaMaxima);

    /// <summary>Etapa a persistir: el valor informado o 1 (mismo default que <c>COALESCE(f.etapa,1)</c> de la fn).</summary>
    public static int EtapaEfectiva(int? etapa) => etapa is null or 0 ? EtapaMinima : etapa.Value;

    // ── Nivel del stock de alimento ───────────────────────────────────────────────────────────────
    /// <summary>
    /// Posición de stock del alimento de un lote según el flag efectivo
    /// (<see cref="AlimentoNivelResolver.ManejaPorGalpon"/>).
    /// <para>
    /// Nivel GRANJA (Sanmarino, Santa Reyes) ⇒ núcleo y galpón en <c>null</c>: es donde vive el stock
    /// (<c>inventario_gestion_stock</c> con <c>nucleo_id</c>/<c>galpon_id</c> NULL) y es lo que exige
    /// <c>InventarioGestionService</c>, que LANZA si le mandan ubicación a una granja de nivel granja.
    /// Nivel GALPÓN (Ecuador, Panamá) ⇒ la ubicación del lote, como en engorde.
    /// </para>
    /// </summary>
    public static UbicacionAlimento PosicionStockDeLote(bool manejaPorGalpon, int farmId, string? nucleoId, string? galponId)
        => manejaPorGalpon
            ? new UbicacionAlimento(farmId, nucleoId, galponId).Normalizada()
            : new UbicacionAlimento(farmId, null, null);

    /// <summary>
    /// Ajusta una ubicación escrita por el usuario en la hoja "Alimento" al nivel efectivo de la granja.
    /// En nivel granja se descartan núcleo y galpón (ver <see cref="UbicacionTraeDetalleIgnorado"/>
    /// para avisarlo); en nivel galpón se conserva tal cual.
    /// </summary>
    public static UbicacionAlimento NormalizarUbicacionSegunNivel(UbicacionAlimento ubicacion, bool manejaPorGalpon)
        => manejaPorGalpon
            ? ubicacion.Normalizada()
            : new UbicacionAlimento(ubicacion.FarmId, null, null);

    /// <summary>
    /// ¿La fila indicó núcleo/galpón en una granja que maneja el alimento a nivel granja? Es una
    /// Advertencia, no un error: el movimiento se aplica igual (a nivel granja) y el usuario se entera
    /// de que su columna no tuvo efecto. Sin este aviso, propagar la ubicación haría que
    /// <c>RegistrarIngresoAsync</c> lance y la fila se perdiera con un mensaje de infraestructura.
    /// </summary>
    public static bool UbicacionTraeDetalleIgnorado(UbicacionAlimento ubicacion, bool manejaPorGalpon)
    {
        if (manejaPorGalpon) return false;
        var n = ubicacion.Normalizada();
        return n.NucleoId is not null || n.GalponId is not null;
    }

    // ── Consumo: ítems del inventario vs consumo directo ──────────────────────────────────────────
    /// <summary>
    /// ¿El consumo de esta fila descuenta inventario? Solo cuando la fila trae ítems del inventario
    /// (columnas "Alimento 1/2"). Sin ítems, "Consumo H/M (kg)" sigue siendo el número suelto de
    /// siempre y NO toca stock — es lo que mantiene válidos los archivos ya en uso.
    /// </summary>
    public static bool ConsumoDescuentaInventario(int cantidadItems) => cantidadItems > 0;

    /// <summary>
    /// ¿Hay que avisar que el consumo directo se ignora? Ocurre cuando la fila trae ítems Y además un
    /// consumo suelto &gt; 0: el consumo real sale de los ítems (espejo de la regla de engorde).
    /// </summary>
    public static bool ConsumoDirectoIgnorado(int cantidadItems, decimal? consumoDirecto)
        => cantidadItems > 0 && consumoDirecto is > 0;

    // ── Referencias de los movimientos de inventario ──────────────────────────────────────────────
    // Deben coincidir BYTE A BYTE con las que escribe el alta manual: es la cadena por la que se
    // reconoce, audita y concilia el movimiento en el histórico de inventario. Si la carga masiva
    // usara otro formato, los mismos días quedarían con dos referencias distintas según cómo se
    // cargaron.

    // El id se toma como long porque la clave de seguimiento_diario_levante lo es; el texto que sale
    // es idéntico al del alta manual (que la recibe como int desde su DTO).

    /// <summary>Referencia del consumo de un día de Levante (espejo de <c>SeguimientoLoteLevanteService</c>).</summary>
    public static string ReferenciaConsumoLevante(long seguimientoId, DateTime fecha)
        => $"Seguimiento lote levante #{seguimientoId} {fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    /// <summary>Referencia del consumo de un día de Producción (espejo de <c>ProduccionService</c>).</summary>
    public static string ReferenciaConsumoProduccion(long seguimientoId, DateTime fecha)
        => $"Seguimiento producción #{seguimientoId} {fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

    // ── Huevos ────────────────────────────────────────────────────────────────────────────────────
    // Los totales NO se recalculan acá: HuevosClasificacion ya deriva Totales e Incubables con la
    // misma aritmética del modal. Estas funciones solo deciden CUÁL de las fuentes manda.

    /// <summary>¿La fila trae algún dato de huevos (categorías, totales o peso)?</summary>
    public static bool TraeHuevos(HuevosClasificacion categorias, int? total, int? incubables, double? pesoHuevo)
        => categorias.Totales > 0 || total is > 0 || incubables is > 0 || pesoHuevo is > 0;

    /// <summary>
    /// Total e incubables a persistir.
    /// <list type="bullet">
    ///   <item>Sin categorías → los valores explícitos de las columnas "Huevo Total"/"Huevo Incubable"
    ///   tal cual (comportamiento actual, byte a byte, para los archivos ya en uso).</item>
    ///   <item>Con categorías → DERIVADOS de la clasificación, igual que el modal
    ///   (<c>Totales</c> = suma de las 11, <c>Incubables</c> = Limpio + Tratado). El desglose es el
    ///   dato fino; dejarlo peleado con el total sería un descuadre permanente.</item>
    /// </list>
    /// </summary>
    public static (int Total, int Incubables) TotalesHuevoEfectivos(
        HuevosClasificacion categorias, int? totalExplicito, int? incubablesExplicitos)
        => categorias.Totales > 0
            ? (categorias.Totales, categorias.Incubables)
            : (totalExplicito ?? 0, incubablesExplicitos ?? 0);

    /// <summary>
    /// ¿El total explícito discrepa de la suma de las categorías? Es Advertencia y no error: el alta
    /// manual tampoco obliga a que cuadren (hay días en que el operario carga el total y desglosa solo
    /// una parte), pero el usuario debe enterarse de que va a ganar el desglose.
    /// </summary>
    public static bool TotalDiscrepaDeCategorias(int? totalExplicito, HuevosClasificacion categorias)
        => categorias.Totales > 0 && totalExplicito is int t && t != categorias.Totales;

    /// <summary>
    /// ¿La fila mezcla las dos formas de clasificar huevos? Con clasificación POR ÍTEMS (Santa Reyes)
    /// <c>huevo_tot</c> es la suma de los ítems y las 11 columnas quedan en 0 (regla de
    /// <see cref="HuevoItemsCalculos"/>): si además llegan categorías o incubables, no hay forma de
    /// saber cuál gana ⇒ es un ERROR, no se adivina.
    /// </summary>
    public static bool MezclaFuentesDeHuevos(bool hayHuevoItems, HuevosClasificacion categorias, int? incubables)
        => hayHuevoItems && (categorias.Totales > 0 || incubables is > 0);
}
