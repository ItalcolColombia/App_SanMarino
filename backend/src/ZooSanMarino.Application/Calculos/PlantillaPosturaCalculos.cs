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

    /// <summary>Slots de alimento del inventario para hembras.</summary>
    private static readonly string[] AlimentoHembras =
    {
        "Alimento 1 H", "Consumo Alimento 1 H", "Alimento 2 H", "Consumo Alimento 2 H",
    };

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

        // 🔴 Empresas con el alimento ubicado en SILOS: la carga masiva todavía no mueve inventario
        // por silo (ver EmiteHojaAlimento). Ofrecer los slots de alimento del inventario sería
        // ofrecer el único camino que falla — el consumo se digita en "Consumo H/M (kg)", que es
        // consumo directo y no toca inventario. Las columnas siguen siendo válidas al importar: lo
        // que cambia es que la plantilla ya no las propone.
        if (flags.ManejaInventarioPorSilo)
        {
            foreach (var t in AlimentoHembras) ocultas.Add(t);
            foreach (var t in AlimentoMachos) ocultas.Add(t);
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
    /// ¿La plantilla emite la hoja <c>Alimento</c> (movimientos de inventario del período)?
    ///
    /// <para>
    /// 🔴 <b>No, para empresas que ubican el alimento en SILOS.</b> El módulo de migraciones no conoce
    /// los silos: no tiene columna, ni DTO, ni clave con silo. Medido en el camino real: cada fila de
    /// la hoja termina en «Debe indicar el silo o la bodega donde queda el movimiento» —
    /// <c>ValidarUbicacion</c> del servicio de inventario— así que no entra un solo kilo; y la
    /// simulación previa del dry-run, que suma todos los silos en una sola posición, da luz verde
    /// igual. Emitir la hoja es prometer un camino que no existe.
    /// </para>
    ///
    /// <para>
    /// Lo que SÍ funciona para esas empresas es el consumo DIRECTO (<c>Consumo H/M (kg)</c>): se
    /// guarda en el día y no toca inventario, que es el comportamiento correcto para un histórico
    /// cuyo alimento ya se movió en la realidad. Las entradas de inventario se cargan por pantalla.
    /// </para>
    /// </summary>
    public static bool EmiteHojaAlimento(FlagsPlantillaPostura flags) => !flags.ManejaInventarioPorSilo;

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
            ManejaInventarioPorSilo: true);
        return ColumnasOcultas(esLevante, todos);
    }
}
