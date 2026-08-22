using System.Globalization;
using System.Text.Json;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Kilos de alimento capturados en un seguimiento diario, ya separados por el bloque del formulario
/// del que salieron.
///
/// <para>
/// <b>Por qué el bloque importa y no alcanza con el total:</b> la regla que pidió el usuario no es
/// «que haya alimento», es «que haya alimento <i>en el bloque que corresponde</i>» — en pollo engorde
/// sobre el campo Mixto, en postura sobre hembras y/o machos—. Un registro con 400 kg cargados en el
/// lugar equivocado cumple el total y sigue siendo el error que se quiere evitar.
/// </para>
///
/// <para>
/// <b>Generales no cuenta.</b> <c>itemsGenerales</c> es la bolsa de «otros ítems» (Colombia la usa
/// para descontar insumos que no son alimento por sexo); si contara, un registro con solo vitaminas
/// pasaría la validación. Se recibe igual para poder decirlo en el mensaje.
/// </para>
/// </summary>
/// <param name="KgHembras">Kilos del bloque Hembras. En pollo engorde en modo Mixto, el formulario
/// vuelca ahí el bloque Mixto (ver <c>modal-seguimiento-engorde</c>), así que este es el campo
/// que se valida.</param>
/// <param name="KgMachos">Kilos del bloque Machos.</param>
/// <param name="KgGenerales">Kilos de otros ítems. Nunca satisface la regla por sí solo.</param>
public readonly record struct AlimentoCapturado(decimal KgHembras, decimal KgMachos, decimal KgGenerales)
{
    /// <summary>Kilos que cuentan para la regla (los de los bloques por sexo / mixto).</summary>
    public decimal KgQueCuentan => KgHembras + KgMachos;

    /// <summary>True si hay algo cargado en algún bloque, aunque sea en el que no corresponde.</summary>
    public bool HayAlgoCargado => KgHembras > 0 || KgMachos > 0 || KgGenerales > 0;
}

/// <summary>
/// Regla PURA de <b>alimento obligatorio</b> en los seguimientos diarios.
///
/// <para>
/// Pedido del usuario (14ago26): «no se puede realizar seguimiento diario sin agregar alimento, el
/// tipo de alimento y la cantidad de consumo. Si es engorde es necesario que sea en el campo mixto;
/// si es postura con levante y producción debe ser alguno de los géneros, macho o hembra, o los dos;
/// también en reproductora pollo engorde debe tener registros».
/// </para>
///
/// <para>
/// Se valida en los dos lados: el front bloquea el submit y explica, y el backend la vuelve a exigir
/// porque la carga masiva por Excel y el push de la PWA entran por el mismo service sin pasar por el
/// formulario.
/// </para>
/// </summary>
public static class AlimentoObligatorioCalculos
{
    /// <summary>
    /// Kilos que valen para la regla, mirando las <b>dos</b> formas en que un cliente puede mandar el
    /// alimento: los ítems del metadata y el campo suelto de consumo (<c>ConsumoKgHembras</c> /
    /// <c>ConsumoKgMachos</c>, o su equivalente ya convertido a kg).
    ///
    /// <para>
    /// Existe porque <see cref="MetadataEngordeCalculos.ParseKgPorBloque"/> sólo suma ítems con
    /// <c>catalogItemId</c> / <c>itemInventarioEcuadorId</c> &gt; 0: un registro con
    /// <c>consumoHembras: 120</c> y sin ítems de inventario da cero por ahí. Así cargan la app móvil,
    /// la carga masiva por Excel y la PWA — todo lo que no pasa por el formulario web—, y sin mirar el
    /// campo suelto el guard rechazaba un registro que SÍ traía alimento.
    /// </para>
    ///
    /// <para>
    /// <b>MÁXIMO, no suma:</b> cuando el registro trae las dos cosas son el MISMO alimento expresado
    /// dos veces (el formulario llena el campo suelto además de los ítems). Sumarlos duplicaría los
    /// kg. Para lo único que se usan acá es para decidir si hay alimento o no, así que el máximo basta
    /// y no inventa cantidades.
    /// </para>
    ///
    /// <para>
    /// <c>KgGenerales</c> sale sólo del metadata: no hay campo suelto de «otros ítems», y de todos
    /// modos nunca satisface la regla por sí solo.
    /// </para>
    /// </summary>
    public static AlimentoCapturado Capturado(
        JsonElement? metadata, decimal kgHembrasDirecto, decimal kgMachosDirecto)
    {
        var (h, m, g) = metadata is null
            ? (0m, 0m, 0m)
            : MetadataEngordeCalculos.ParseKgPorBloque(metadata.Value);

        return new AlimentoCapturado(
            Math.Max(h, kgHembrasDirecto),
            Math.Max(m, kgMachosDirecto),
            g);
    }

    /// <summary>True si el registro cumple la regla de alimento obligatorio.</summary>
    public static bool Cumple(string modulo, bool loteEsMixto, AlimentoCapturado alimento) =>
        Motivo(modulo, loteEsMixto, alimento, fecha: null) is null;

    /// <summary>
    /// Motivo del rechazo, o <c>null</c> si cumple. Devolver el texto acá (y no un booleano más un
    /// mensaje armado en cada service) es lo que garantiza que los cuatro módulos digan exactamente lo
    /// mismo — el usuario reporta justamente que hoy «hay momentos en que no muestra el motivo».
    /// </summary>
    /// <param name="modulo">Uno de <see cref="ModuloSeguimiento"/>.</param>
    /// <param name="loteEsMixto">True cuando el formulario muestra la columna Mixto única en vez de
    /// Hembras/Machos (pollo engorde de Panamá y lotes mixtos).</param>
    /// <param name="alimento">Kilos por bloque.</param>
    /// <param name="fecha">Fecha del registro; si viene, se nombra en el mensaje.</param>
    public static string? Motivo(string modulo, bool loteEsMixto, AlimentoCapturado alimento, DateOnly? fecha)
    {
        if (alimento.KgQueCuentan > 0) return null;

        var prefijo = fecha.HasValue
            ? $"El registro del {fecha.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} no tiene alimento: "
            : "El registro no tiene alimento: ";

        var exigencia = BloqueExigido(modulo, loteEsMixto);

        // Caso frecuente y confuso: cargaron el alimento en «otros ítems» y el formulario se ve lleno.
        // Sin decirlo, el usuario relee los mismos campos sin encontrar el problema.
        if (alimento.KgGenerales > 0)
            return prefijo + exigencia +
                   " Lo que está cargado figura como otros ítems, que no cuentan como consumo de alimento.";

        return prefijo + exigencia;
    }

    /// <summary>
    /// Qué bloque exige cada módulo. Es la frase que ve el usuario, así que nombra el campo tal cual
    /// aparece en pantalla.
    /// </summary>
    public static string BloqueExigido(string modulo, bool loteEsMixto)
    {
        if (ModuloSeguimiento.EsEngorde(modulo))
            return loteEsMixto
                ? "hay que indicar el tipo de alimento y la cantidad de consumo en el campo Mixto."
                : "hay que indicar el tipo de alimento y la cantidad de consumo en Hembras o en Machos.";

        if (ModuloSeguimiento.EsPostura(modulo))
            return "hay que indicar el tipo de alimento y la cantidad de consumo en Hembras, en Machos o en ambos.";

        // Reproductora: el formulario es el de la primera semana de recogida y captura un solo bloque.
        return "hay que indicar el tipo de alimento y la cantidad de consumo del lote.";
    }
}
