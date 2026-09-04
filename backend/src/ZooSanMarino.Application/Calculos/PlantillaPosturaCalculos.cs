// src/ZooSanMarino.Application/Calculos/PlantillaPosturaCalculos.cs
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Flags de la EMPRESA que condicionan qué columnas trae la plantilla .xlsx de seguimiento de postura.
/// Se resuelven por la empresa dueña de la GRANJA del lote (no la del token) y fail-closed: ante
/// granja irresoluble quedan todos en <c>false</c>, que es el comportamiento histórico.
/// </summary>
/// <param name="OcultaMachosEnPostura">La empresa no maneja machos en postura (Santa Reyes).</param>
/// <param name="ConsumoAlimentoSoloHembras">El consumo de alimento se digita solo para hembras.</param>
/// <param name="ClasificacionHuevoPorItems">El huevo se clasifica por ítem del catálogo, no por las 11 categorías fijas.</param>
/// <param name="CapturaHuevosEnLevante">La empresa registra huevos durante el levante.</param>
/// <param name="ManejaInventarioPorSilo">El alimento se ubica en SILOS (Santa Reyes), no en granja/galpón.</param>
public readonly record struct FlagsPlantillaPostura(
    bool OcultaMachosEnPostura,
    bool ConsumoAlimentoSoloHembras,
    bool ClasificacionHuevoPorItems,
    bool CapturaHuevosEnLevante,
    bool ManejaInventarioPorSilo = false);

/// <summary>
/// Cálculo PURO (sin EF ni estado) que decide qué columnas de <see cref="MigracionEsquemas"/> se
/// OMITEN al generar la plantilla de seguimiento de postura para una empresa.
///
/// <para>
/// 🔴 Lo que este cálculo NO hace: cambiar el esquema de validación. Las columnas omitidas siguen
/// siendo opcionales al importar (todas son <c>Requerida: false</c>; lo fija
/// <see cref="TitulosOcultablesPorEsquema"/> y su test), así que un archivo viejo con las 43 columnas
/// sigue entrando sin un solo Error. Es el mismo contrato que documenta <c>PonerEncabezadosSin</c>.
/// </para>
///
/// <para>
/// El criterio no se inventa: es el ESPEJO EXACTO de lo que el formulario vivo ya oculta con los
/// mismos flags — <c>modal-seguimiento-diario.component.html</c> (<c>@if (!ocultaMachosEnPostura)</c>,
/// <c>@if (!clasificacionHuevoPorItems)</c>) y <c>seguimiento-lote-levante-list.component.html</c>.
/// Si la plantilla pidiera un dato que la pantalla no muestra, la carga masiva sería la puerta de
/// atrás del flag.
/// </para>
/// </summary>
public static class PlantillaPosturaCalculos
{
    /// <summary>Columnas por sexo MACHO del seguimiento de levante (las de producción son un subconjunto).</summary>
    private static readonly string[] MachosLevante =
    {
        "Mort M", "Sel M", "Error Sexaje M", "Consumo M (kg)", "Peso M (g)",
        "Uniformidad M", "Coef. Variación M",
    };

    /// <summary>
    /// Columnas por sexo MACHO del seguimiento de producción. Ojo: producción NO tiene "Uniformidad M"
    /// ni "Coef. Variación M" (su pesaje es un único valor de lote: "Uniformidad" y "Coef. Variación").
    /// </summary>
    private static readonly string[] MachosProduccion =
    {
        "Mort M", "Sel M", "Error Sexaje M", "Consumo M (kg)", "Peso M (g)",
    };

    /// <summary>Slots de alimento del inventario para machos (las dos líneas de postura los comparten).</summary>
    private static readonly string[] AlimentoMachos =
    {
        "Alimento 1 M", "Consumo Alimento 1 M", "Alimento 2 M", "Consumo Alimento 2 M",
    };

    /// <summary>
    /// Columnas de SILO apareadas a cada slot de alimento. Solo se emiten en empresas con
    /// <c>maneja_inventario_por_silo</c>: para las demás son ruido, y peor —el parseo RECHAZA un silo
    /// en modo clásico (<c>ConsumoSiloCalculos.MensajeSiloNoAplica</c>), así que ofrecerlas sería
    /// ofrecer una columna que hace fallar el archivo.
    /// </summary>
    private static readonly string[] SilosPorSlot =
    {
        "Silo Alimento 1 H", "Silo Alimento 2 H", "Silo Alimento 1 M", "Silo Alimento 2 M",
    };

    /// <summary>Columnas de silo de la hoja <c>Alimento</c> (destino y origen del movimiento).</summary>
    private static readonly string[] SilosHojaAlimento = { "Silo", "Silo Origen" };

    /// <summary>
    /// Las 11 categorías de la clasificadora fija, con el mismo título que emite
    /// <see cref="MigracionEsquemas"/>. Se ocultan cuando el huevo se clasifica por ítem del catálogo
    /// (producción) o cuando la empresa no captura huevos en levante.
    /// </summary>
    private static readonly string[] CategoriasHuevo =
    {
        "Huevo Limpio", "Huevo Tratado", "Huevo Sucio", "Huevo Deforme", "Huevo Blanco",
        "Huevo Doble Yema", "Huevo Piso", "Huevo Pequeño", "Huevo Roto", "Huevo Desecho", "Huevo Otro",
    };

    /// <summary>
    /// Títulos que se omiten de la hoja "Datos" de la plantilla, según la línea y los flags de la
    /// empresa. Con TODOS los flags en su valor neutro el conjunto es VACÍO ⇒ la plantilla sale
    /// idéntica a la histórica (delta cero por construcción).
    /// </summary>
    /// <param name="esLevante">true = Seguimiento Levante; false = Seguimiento Producción.</param>
    /// <param name="flags">Flags de la empresa dueña de la granja del lote.</param>
    public static IReadOnlySet<string> ColumnasOcultas(bool esLevante, FlagsPlantillaPostura flags)
    {
        var ocultas = new HashSet<string>(StringComparer.Ordinal);

        if (flags.OcultaMachosEnPostura)
            foreach (var t in esLevante ? MachosLevante : MachosProduccion) ocultas.Add(t);

        // El consumo por sexo desaparece con cualquiera de los dos flags: sin machos no hay consumo de
        // machos, y "solo hembras" lo dice literalmente. Se evalúan por separado porque una empresa
        // puede digitar el consumo solo de hembras y aun así llevar el conteo de machos.
        if (flags.ConsumoAlimentoSoloHembras || flags.OcultaMachosEnPostura)
        {
            foreach (var t in AlimentoMachos) ocultas.Add(t);
            ocultas.Add("Consumo M (kg)");
        }

        // El SILO por slot de alimento solo aplica a las empresas que ubican el inventario por silo.
        // En las demás el parseo lo RECHAZA (modo clásico ⇒ MensajeSiloNoAplica), así que emitir la
        // columna sería invitar a romper el archivo.
        if (!flags.ManejaInventarioPorSilo)
            foreach (var t in SilosPorSlot) ocultas.Add(t);
        // Y en las que sí lo manejan, el silo de un slot de MACHOS se va junto con el slot.
        else if (flags.ConsumoAlimentoSoloHembras || flags.OcultaMachosEnPostura)
        {
            ocultas.Add("Silo Alimento 1 M");
            ocultas.Add("Silo Alimento 2 M");
        }

        if (esLevante)
        {
            // Sin captura de huevos en levante, el parseo ya NEUTRALIZA esas columnas con Advertencia
            // (MigracionService.Historicos.cs). Emitirlas es pedir un dato que el sistema va a tirar.
            if (!flags.CapturaHuevosEnLevante)
            {
                foreach (var t in CategoriasHuevo) ocultas.Add(t);
                ocultas.Add("Peso Huevo (g)");
            }
        }
        else if (flags.ClasificacionHuevoPorItems)
        {
            // Espejo del modal: con clasificación por ítems se ocultan Huevos Totales, Huevos
            // Incubables, Peso Promedio y la clasificadora entera. El desglose viaja en la hoja
            // "Huevos" y el total se DERIVA de ella (huevo_tot = suma de ítems, huevo_inc = 0).
            foreach (var t in CategoriasHuevo) ocultas.Add(t);
            ocultas.Add("Huevo Total");
            ocultas.Add("Huevo Incubable");
            ocultas.Add("Peso Huevo (g)");
        }

        return ocultas;
    }

    /// <summary>
    /// Columnas de la hoja <c>Alimento</c> que NO se emiten. Solo las de silo, y solo para empresas
    /// que ubican el inventario por núcleo/galpón: ahí el servicio de inventario rechaza un
    /// movimiento que traiga silo (<c>InventarioUbicacionSiloCalculos.MensajeSiloNoAplica</c>).
    /// </summary>
    public static IReadOnlySet<string> ColumnasOcultasHojaAlimento(bool manejaInventarioPorSilo)
        => manejaInventarioPorSilo
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(SilosHojaAlimento, StringComparer.Ordinal);

    /// <summary>
    /// Los títulos que este cálculo puede llegar a ocultar, para el test que verifica que NINGUNO sea
    /// <c>Requerida: true</c> en su esquema. Ocultar una columna requerida rompería la validación de
    /// encabezados del propio archivo que la plantilla genera.
    /// </summary>
    public static IReadOnlySet<string> TitulosOcultablesPorEsquema(bool esLevante)
    {
        var todos = new FlagsPlantillaPostura(
            OcultaMachosEnPostura: true,
            ConsumoAlimentoSoloHembras: true,
            ClasificacionHuevoPorItems: true,
            CapturaHuevosEnLevante: false,
            ManejaInventarioPorSilo: false);
        var sinSilo = ColumnasOcultas(esLevante, todos);
        // Y las de silo, que se ocultan con el flag en el otro sentido.
        var conSilo = ColumnasOcultas(esLevante, todos with { ManejaInventarioPorSilo = true });
        return new HashSet<string>(sinSilo.Concat(conSilo), StringComparer.Ordinal);
    }
}
