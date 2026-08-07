// src/ZooSanMarino.Application/Calculos/MigracionEsquemas.cs
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Esquema único por tipo de migración: fuente de verdad tanto para generar la plantilla .xlsx como
/// para validar los encabezados del archivo subido. Títulos, orden y alias transcriptos EXACTOS del
/// código de plantillas/parseo existente (MigracionService.Plantillas.cs, .Historicos.cs,
/// .EstructuraEngorde.cs, .SeguimientoEngorde.cs, .VentaEngorde.cs) — no se inventa ni renombra nada,
/// la clave normalizada del propio Título siempre se acepta sin necesidad de repetirla en Alias.
/// </summary>
public static class MigracionEsquemas
{
    public static EsquemaMigracion Granjas { get; } = new("Datos", new ColumnaEsquema[]
    {
        new("Nombre",       Requerida: true),
        new("Departamento", Requerida: true),
        new("Ciudad",       Requerida: true, Alias: new[] { "municipio" }),
        new("Regional",     Requerida: true),
        new("Estado",       Requerida: false, Opciones: new[] { "A", "I" }),
    });

    public static EsquemaMigracion Nucleos { get; } = new("Datos", new ColumnaEsquema[]
    {
        new("Granja",         Requerida: true),
        new("Código Núcleo",  Requerida: true, Alias: new[] { "codigo" }),
        new("Nombre",         Requerida: true),
    });

    public static EsquemaMigracion Galpones { get; } = new("Datos", new ColumnaEsquema[]
    {
        new("Granja",         Requerida: true),
        new("Núcleo",         Requerida: true),
        new("Código Galpón",  Requerida: false, Alias: new[] { "codigo" }),
        new("Nombre",         Requerida: true),
        new("Ancho",          Requerida: false),
        new("Largo",          Requerida: false),
        new("Tipo Galpón",    Requerida: false, Alias: new[] { "tipo" }),
    });

    // ── Columnas compartidas por las dos líneas de POSTURA ────────────────────────────────────────
    // Definidas una sola vez para que Levante y Producción no se puedan desincronizar (mismo criterio
    // que los títulos mixtos de engorde).

    /// <summary>Unidad del consumo (directo y por alimento): "kg" (default) o "qq" (×45.36).</summary>
    private static ColumnaEsquema UnidadConsumoPostura() =>
        new("Unidad Consumo", Requerida: false, Alias: new[] { "unidad", "unidad de consumo", "unidad medida" }, Opciones: new[] { "kg", "qq" });

    /// <summary>
    /// Hasta DOS alimentos del INVENTARIO por sexo: el nombre/código se busca entre los ítems de
    /// concepto alimento de la empresa y, cuando la fila los trae, el consumo DESCUENTA stock real
    /// (a nivel granja o galpón según <c>maneja_alimento_por_galpon</c>). Sin ellos, "Consumo H/M (kg)"
    /// sigue siendo el consumo directo de siempre, que no toca inventario.
    /// </summary>
    private static IEnumerable<ColumnaEsquema> AlimentosPorSexoPostura()
    {
        yield return new("Alimento 1 H",         Requerida: false, Alias: new[] { "alimento 1 hembras", "alimento uno hembras" });
        yield return new("Consumo Alimento 1 H", Requerida: false, Alias: new[] { "consumo 1 h", "consumo alimento uno hembras" });
        yield return new("Alimento 2 H",         Requerida: false, Alias: new[] { "alimento 2 hembras", "alimento dos hembras" });
        yield return new("Consumo Alimento 2 H", Requerida: false, Alias: new[] { "consumo 2 h", "consumo alimento dos hembras" });
        yield return new("Alimento 1 M",         Requerida: false, Alias: new[] { "alimento 1 machos", "alimento uno machos" });
        yield return new("Consumo Alimento 1 M", Requerida: false, Alias: new[] { "consumo 1 m", "consumo alimento uno machos" });
        yield return new("Alimento 2 M",         Requerida: false, Alias: new[] { "alimento 2 machos", "alimento dos machos" });
        yield return new("Consumo Alimento 2 M", Requerida: false, Alias: new[] { "consumo 2 m", "consumo alimento dos machos" });
    }

    /// <summary>
    /// Las 11 categorías de la clasificadora fija, en el MISMO orden del modal. Incubables y total se
    /// DERIVAN de ellas cuando la fila las trae (<c>HuevosClasificacion</c>), igual que en pantalla.
    /// </summary>
    private static IEnumerable<ColumnaEsquema> CategoriasHuevo()
    {
        yield return new("Huevo Limpio",     Requerida: false, Alias: new[] { "limpio" });
        yield return new("Huevo Tratado",    Requerida: false, Alias: new[] { "tratado" });
        yield return new("Huevo Sucio",      Requerida: false, Alias: new[] { "sucio" });
        yield return new("Huevo Deforme",    Requerida: false, Alias: new[] { "deforme" });
        yield return new("Huevo Blanco",     Requerida: false, Alias: new[] { "blanco" });
        yield return new("Huevo Doble Yema", Requerida: false, Alias: new[] { "doble yema", "huevo doble" });
        yield return new("Huevo Piso",       Requerida: false, Alias: new[] { "piso" });
        yield return new("Huevo Pequeño",    Requerida: false, Alias: new[] { "huevo pequeno", "pequeño", "pequeno" });
        yield return new("Huevo Roto",       Requerida: false, Alias: new[] { "roto" });
        yield return new("Huevo Desecho",    Requerida: false, Alias: new[] { "desecho" });
        yield return new("Huevo Otro",       Requerida: false, Alias: new[] { "otro", "otros" });
    }

    /// <summary>
    /// Seguimiento diario de LEVANTE (postura). Las 15 primeras columnas son las históricas — mismo
    /// título y mismo orden — de modo que un archivo anterior sigue siendo válido sin tocarlo. Lo
    /// agregado es opcional: unidad de consumo, alimentos del inventario (que descuentan stock) y los
    /// huevos de la semana 14 en adelante (empresas con <c>captura_huevos_en_levante</c>).
    /// </summary>
    public static EsquemaMigracion SeguimientoLevante { get; } = new("Datos", new ColumnaEsquema[]
    {
        new("Fecha",              Requerida: true),
        new("Mort H",             Requerida: false, Alias: new[] { "mortalidad hembras" }),
        new("Mort M",             Requerida: false, Alias: new[] { "mortalidad machos" }),
        new("Sel H",              Requerida: false),
        new("Sel M",              Requerida: false),
        new("Error Sexaje H",     Requerida: false),
        new("Error Sexaje M",     Requerida: false),
        new("Consumo H (kg)",     Requerida: false, Alias: new[] { "consumo h" }),
        new("Consumo M (kg)",     Requerida: false, Alias: new[] { "consumo m" }),
        new("Tipo Alimento",      Requerida: false),
        new("Peso H (g)",         Requerida: false, Alias: new[] { "peso h" }),
        new("Peso M (g)",         Requerida: false, Alias: new[] { "peso m" }),
        new("Uniformidad H",      Requerida: false),
        new("Uniformidad M",      Requerida: false),
        new("Observaciones",      Requerida: false),
    }
        .Append(UnidadConsumoPostura())
        .Concat(AlimentosPorSexoPostura())
        .Concat(CategoriasHuevo())
        .Append(new ColumnaEsquema("Peso Huevo (g)", Requerida: false, Alias: new[] { "peso huevo" }))
        // Pesaje y agua del registro diario que la carga de LEVANTE no aceptaba, mientras que la de
        // producción sí. El coeficiente de variación es el caso grave: `fn_reporte_semanal_levante_extras`
        // y `fn_resumen_semanal_ra_pesadas_levante` LEEN cv_hembras/cv_machos, así que la columna C.V.%
        // del reporte semanal de levante salía siempre vacía porque nada la escribía.
        .Concat(new ColumnaEsquema[]
        {
            new("Coef. Variación H",      Requerida: false, Alias: new[] { "coeficiente de variacion h", "cv h", "coef variacion h" }),
            new("Coef. Variación M",      Requerida: false, Alias: new[] { "coeficiente de variacion m", "cv m", "coef variacion m" }),
            new("Observaciones Pesaje",   Requerida: false, Alias: new[] { "obs pesaje" }),
            new("Consumo Agua (L)",       Requerida: false, Alias: new[] { "consumo agua", "agua", "litros agua" }),
            new("pH Agua",                Requerida: false, Alias: new[] { "ph", "ph agua" }),
            new("ORP Agua (mV)",          Requerida: false, Alias: new[] { "orp", "orp agua" }),
            new("Temperatura Agua (°C)",  Requerida: false, Alias: new[] { "temperatura agua", "temp agua" }),
        })
        .ToArray());

    /// <summary>
    /// Seguimiento diario de PRODUCCIÓN (postura). Las 12 primeras columnas son las históricas. Lo
    /// agregado es opcional: unidad de consumo, alimentos del inventario y las 11 categorías de la
    /// clasificadora que el modal ya captura y el Excel no pedía.
    /// <para>
    /// La clasificación POR ÍTEMS del catálogo (empresas con <c>clasificacion_huevo_por_items</c>, ej.
    /// Santa Reyes) NO va en columnas fijas — el desglose es variable por empresa — sino en la hoja
    /// <see cref="HuevosPostura"/>.
    /// </para>
    /// </summary>
    public static EsquemaMigracion SeguimientoProduccion { get; } = new("Datos", new ColumnaEsquema[]
    {
        new("Fecha",              Requerida: true),
        new("Mort H",             Requerida: false, Alias: new[] { "mortalidad hembras" }),
        new("Mort M",             Requerida: false, Alias: new[] { "mortalidad machos" }),
        new("Sel H",              Requerida: false),
        new("Sel M",              Requerida: false),
        new("Consumo H (kg)",     Requerida: false, Alias: new[] { "consumo h" }),
        new("Consumo M (kg)",     Requerida: false, Alias: new[] { "consumo m" }),
        new("Huevo Total",        Requerida: false),
        new("Huevo Incubable",    Requerida: false),
        new("Peso Huevo (g)",     Requerida: false, Alias: new[] { "peso huevo" }),
        new("Etapa",              Requerida: false),
        new("Observaciones",      Requerida: false),
    }
        .Append(UnidadConsumoPostura())
        .Concat(AlimentosPorSexoPostura())
        .Concat(CategoriasHuevo())
        // Campos del modal de producción que la carga no aceptaba (todos opcionales, al final):
        // error de sexaje, pesaje corporal y los 4 de AGUA (consumo_agua_* de la tabla).
        .Concat(new ColumnaEsquema[]
        {
            new("Error Sexaje H",         Requerida: false, Alias: new[] { "error sexaje hembras" }),
            new("Error Sexaje M",         Requerida: false, Alias: new[] { "error sexaje machos" }),
            new("Peso H (g)",             Requerida: false, Alias: new[] { "peso h", "peso corporal h" }),
            new("Peso M (g)",             Requerida: false, Alias: new[] { "peso m", "peso corporal m" }),
            new("Uniformidad",            Requerida: false, Alias: new[] { "uniformidad lote" }),
            new("Coef. Variación",        Requerida: false, Alias: new[] { "coeficiente de variacion", "cv" }),
            new("Observaciones Pesaje",   Requerida: false, Alias: new[] { "obs pesaje" }),
            new("Consumo Agua (L)",       Requerida: false, Alias: new[] { "consumo agua", "agua", "litros agua" }),
            new("pH Agua",                Requerida: false, Alias: new[] { "ph", "ph agua" }),
            new("ORP Agua (mV)",          Requerida: false, Alias: new[] { "orp", "orp agua" }),
            new("Temperatura Agua (°C)",  Requerida: false, Alias: new[] { "temperatura agua", "temp agua" }),
        })
        .ToArray());

    /// <summary>
    /// Hoja <c>Alimento</c> de los archivos de POSTURA: movimientos de INVENTARIO que se aplican ANTES
    /// de las filas de consumo de la hoja <c>Datos</c>, para que la granja/galpón tenga stock cuando el
    /// seguimiento lo descuenta. Mismas columnas y reglas que <see cref="AlimentoEngorde"/>.
    /// <para>
    /// Diferencia con engorde: en una granja que maneja el alimento a NIVEL GRANJA (Sanmarino, Santa
    /// Reyes) las columnas Núcleo/Galpón se ignoran con Advertencia — es donde vive el stock y donde el
    /// alta manual descuenta.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Expression-bodied a propósito: <see cref="AlimentoEngorde"/> se declara más abajo en el archivo
    /// y los inicializadores de propiedades estáticas corren en orden textual — con <c>{ get; } =</c>
    /// esta propiedad quedaría en null.
    /// </remarks>
    public static EsquemaMigracion AlimentoPostura => AlimentoEngorde;

    /// <summary>
    /// Hoja <c>Huevos</c> del archivo de PRODUCCIÓN: clasificación por ÍTEM del catálogo de huevo de la
    /// empresa (empresas con <c>clasificacion_huevo_por_items = true</c>, ej. Santa Reyes). Varias filas
    /// por fecha, una por ítem.
    /// <para>
    /// Va en hoja aparte y no en columnas fijas porque el desglose es VARIABLE por empresa (Santa Reyes
    /// tiene 21 ítems) y <see cref="MigracionEsquemas"/> es un esquema estático que debe seguir siendo
    /// la fuente única de plantilla y validación.
    /// </para>
    /// </summary>
    public static EsquemaMigracion HuevosPostura { get; } = new("Huevos", new ColumnaEsquema[]
    {
        new("Fecha",    Requerida: true),
        new("Ítem",     Requerida: true, Alias: new[] { "item", "huevo", "producto", "codigo", "código" }),
        new("Cantidad", Requerida: true, Alias: new[] { "cantidad huevos", "unidades" }),
    });

    /// <summary>
    /// Hoja <c>Movimientos Huevos</c> del archivo de PRODUCCIÓN: salidas de huevos del lote hacia
    /// PLANTA (<c>Traslado</c>) o por <c>Venta</c>, contra <c>traslado_huevos</c> en estado
    /// Completado + un recálculo del espejo al final. Levante ignora esta hoja.
    /// </summary>
    public static EsquemaMigracion MovimientosHuevosProduccion { get; } = new("Movimientos Huevos", new ColumnaEsquema[]
    {
        new("Fecha",        Requerida: true),
        new("Tipo",         Requerida: true, Alias: new[] { "movimiento", "tipo operacion", "operacion" }, Opciones: new[] { "Traslado", "Venta" }),
    }
        .Concat(CategoriasHuevo())
        .Concat(new ColumnaEsquema[]
        {
            new("Tipo Destino",  Requerida: false, Opciones: new[] { "Planta", "Cliente", "Empresa" }),
            new("Destino",       Requerida: false, Alias: new[] { "planta", "cliente", "lote destino" }),
            new("Motivo",        Requerida: false, Alias: new[] { "motivo venta" }),
            new("Descripción",   Requerida: false, Alias: new[] { "descripcion" }),
            new("Observaciones", Requerida: false),
        })
        .ToArray());

    /// <summary>
    /// Hoja <c>Movimientos Aves</c> de los archivos de LEVANTE y PRODUCCIÓN: movimientos de aves
    /// UNILATERALES sobre el lote del archivo. <c>Salida</c> descuenta a este lote y exige que el
    /// "Lote Contraparte" exista en la MISMA fase en la empresa (NO lo acredita: ese lote carga su
    /// propio Ingreso en su propio archivo); <c>Ingreso</c> acredita a este lote las aves recibidas
    /// en tránsito sin tocar al lote origen (contraparte opcional, informativa/cohorte);
    /// <c>Venta</c> descuenta las aves (en levante queda además en <c>venta_aves_*</c> de la fila
    /// diaria; producción no tiene esas columnas y la venta queda en la auditoría + observaciones).
    /// </summary>
    public static EsquemaMigracion MovimientosAvesLevante { get; } = new("Movimientos Aves", new ColumnaEsquema[]
    {
        new("Fecha",              Requerida: true),
        new("Tipo",               Requerida: true,  Alias: new[] { "movimiento", "tipo movimiento" }, Opciones: new[] { "Salida", "Ingreso", "Venta" }),
        new("Hembras",            Requerida: false, Alias: new[] { "cantidad hembras", "traslado hembras" }),
        new("Machos",             Requerida: false, Alias: new[] { "cantidad machos", "traslado machos" }),
        new("Lote Contraparte",   Requerida: false, Alias: new[] { "lote destino", "lote origen", "lote" }),
        new("Granja Contraparte", Requerida: false, Alias: new[] { "granja destino", "granja origen" }),
        new("Motivo",             Requerida: false, Alias: new[] { "motivo venta" }),
        new("Observaciones",      Requerida: false),
    });

    public static EsquemaMigracion LotesPolloEngorde { get; } = new("Datos", new ColumnaEsquema[]
    {
        new("Lote",                  Requerida: true,  Alias: new[] { "nombre lote", "nombre" }),
        new("Granja",                Requerida: true),
        new("Núcleo",                Requerida: false),
        new("Galpón",                Requerida: false, Alias: new[] { "galpón" }),
        new("Raza",                  Requerida: true),
        new("Año Tabla",             Requerida: true,  Alias: new[] { "año tabla", "anio tabla genetica" }),
        new("Fecha Encaset",         Requerida: false, Alias: new[] { "fecha de encaset" }),
        new("Hembras",               Requerida: false, Alias: new[] { "hembras l" }),
        new("Machos",                Requerida: false, Alias: new[] { "machos l" }),
        new("Mixtas",                Requerida: false),
        new("Aves Encasetadas",      Requerida: false, Alias: new[] { "encasetadas" }),
        new("Peso Inicial H (g)",    Requerida: false, Alias: new[] { "peso inicial h" }),
        new("Peso Inicial M (g)",    Requerida: false, Alias: new[] { "peso inicial m" }),
        new("Edad Inicial",          Requerida: false),
        new("Técnico",               Requerida: false, Alias: new[] { "técnico" }),
        new("Lote ERP",              Requerida: false, Alias: new[] { "erp" }),
    });

    // ── Títulos MIXTOS del seguimiento pollo engorde ─────────────────────────────────────────────
    // Son a la vez los TÍTULOS que emite SeguimientoPolloEngordeMixto (plantilla de una empresa con
    // seguimiento_engorde_mixto = true) y los ALIAS que acepta SeguimientoPolloEngorde al parsear.
    // Definidos una sola vez para que plantilla y parseo no se puedan desincronizar.
    private const string MixMortalidad        = "Mort Mixta";
    private const string MixSeleccion         = "Sel Mixta";
    private const string MixConsumo           = "Consumo Mixto (kg)";
    private const string MixAlimento1         = "Alimento 1 Mixto";
    private const string MixConsumoAlimento1  = "Consumo Alimento 1 Mixto";
    private const string MixAlimento2         = "Alimento 2 Mixto";
    private const string MixConsumoAlimento2  = "Consumo Alimento 2 Mixto";
    private const string MixPeso              = "Peso Mixto (g)";
    private const string MixUniformidad       = "Uniformidad Mixta";

    public static EsquemaMigracion SeguimientoPolloEngorde { get; } = new("Datos", new ColumnaEsquema[]
    {
        // Ubicación por NOMBRES (opcional): si la fila trae "Lote", se resuelve el lote engorde por
        // nombre (comparación sin mayúsculas/acentos) acotado por Granja/Núcleo/Galpón si vienen;
        // sin "Lote", la fila corresponde al lote seleccionado en pantalla.
        new("Granja",             Requerida: false, Alias: new[] { "nombre granja" }),
        new("Núcleo",             Requerida: false, Alias: new[] { "nombre nucleo" }),
        new("Galpón",             Requerida: false, Alias: new[] { "nombre galpon" }),
        new("Lote",               Requerida: false, Alias: new[] { "nombre lote" }),
        new("Fecha",              Requerida: true),
        // Los alias "…mixta/mixto" son los títulos que emite la plantilla de una empresa con
        // seguimiento_engorde_mixto = true (Panamá: sin manejo por sexo tras salir de reproductora).
        // Apuntan a la MISMA columna H porque el sistema suma H+M en todos sus cálculos.
        new("Mort H",             Requerida: false, Alias: new[] { "mortalidad hembras", MixMortalidad, "mortalidad mixta", "mortalidad mixtas" }),
        new("Mort M",             Requerida: false, Alias: new[] { "mortalidad machos" }),
        new("Sel H",              Requerida: false, Alias: new[] { MixSeleccion, "seleccion mixta" }),
        new("Sel M",              Requerida: false),
        new("Error Sexaje H",     Requerida: false, Alias: new[] { "error sexaje mixta" }),
        new("Error Sexaje M",     Requerida: false),
        new("Consumo H (kg)",     Requerida: false, Alias: new[] { "consumo h", MixConsumo, "consumo mixto", "consumo mixtas (kg)" }),
        new("Consumo M (kg)",     Requerida: false, Alias: new[] { "consumo m" }),
        // Unidad del consumo (directo y por alimento): "kg" (default) o "qq" — con qq se convierte a kg (×45.36).
        new("Unidad Consumo",     Requerida: false, Alias: new[] { "unidad", "unidad de consumo", "unidad medida" }, Opciones: new[] { "kg", "qq" }),
        new("Tipo Alimento",      Requerida: false),
        // Hasta DOS alimentos del inventario por sexo y fecha: el nombre/código se busca en los ítems
        // de concepto alimento de la empresa (sin mayúsculas/acentos) y descuenta inventario al importar.
        new("Alimento 1 H",       Requerida: false, Alias: new[] { "alimento 1 hembras", "alimento uno hembras", MixAlimento1 }),
        new("Consumo Alimento 1 H", Requerida: false, Alias: new[] { "consumo 1 h", "consumo alimento uno hembras", MixConsumoAlimento1 }),
        new("Alimento 2 H",       Requerida: false, Alias: new[] { "alimento 2 hembras", "alimento dos hembras", MixAlimento2 }),
        new("Consumo Alimento 2 H", Requerida: false, Alias: new[] { "consumo 2 h", "consumo alimento dos hembras", MixConsumoAlimento2 }),
        new("Alimento 1 M",       Requerida: false, Alias: new[] { "alimento 1 machos", "alimento uno machos" }),
        new("Consumo Alimento 1 M", Requerida: false, Alias: new[] { "consumo 1 m", "consumo alimento uno machos" }),
        new("Alimento 2 M",       Requerida: false, Alias: new[] { "alimento 2 machos", "alimento dos machos" }),
        new("Consumo Alimento 2 M", Requerida: false, Alias: new[] { "consumo 2 m", "consumo alimento dos machos" }),
        new("Peso H (g)",         Requerida: false, Alias: new[] { "peso h", MixPeso, "peso mixto" }),
        new("Peso M (g)",         Requerida: false, Alias: new[] { "peso m" }),
        new("Uniformidad H",      Requerida: false, Alias: new[] { MixUniformidad }),
        new("Uniformidad M",      Requerida: false),
        // Panamá: alimento en quintales por categoría (persisten en qq_*; opcionales para CO/EC).
        new("QQ Mixtas",          Requerida: false, Alias: new[] { "qq mixtas", "quintales mixtas" }),
        new("QQ H",               Requerida: false, Alias: new[] { "qq hembras", "quintales hembras" }),
        new("QQ M",               Requerida: false, Alias: new[] { "qq machos", "quintales machos" }),
        new("Observaciones",      Requerida: false),
    });

    /// <summary>
    /// Variante MIXTA del seguimiento pollo engorde: la usan las empresas con
    /// <c>seguimiento_engorde_mixto = true</c> (Panamá), donde el lote deja de manejarse por sexo al
    /// salir de reproductora. Se usa SOLO para GENERAR la plantilla — el parseo sigue corriendo con
    /// <see cref="SeguimientoPolloEngorde"/>, que acepta estos títulos como alias de sus columnas "H".
    /// <para>
    /// Por eso desaparecen las columnas por sexo (Mort M, Consumo M, Peso M, QQ H, QQ M…): el dato
    /// mixto se digita una sola vez y el sistema, que suma H+M en todos sus cálculos, obtiene el
    /// mismo resultado que el formulario diario (un único campo de consumo).
    /// </para>
    /// </summary>
    public static EsquemaMigracion SeguimientoPolloEngordeMixto { get; } = new("Datos", new ColumnaEsquema[]
    {
        new("Granja",             Requerida: false, Alias: new[] { "nombre granja" }),
        new("Núcleo",             Requerida: false, Alias: new[] { "nombre nucleo" }),
        new("Galpón",             Requerida: false, Alias: new[] { "nombre galpon" }),
        new("Lote",               Requerida: false, Alias: new[] { "nombre lote" }),
        new("Fecha",              Requerida: true),
        new(MixMortalidad,        Requerida: false),
        new(MixSeleccion,         Requerida: false),
        new(MixConsumo,           Requerida: false),
        new("Unidad Consumo",     Requerida: false, Alias: new[] { "unidad", "unidad de consumo", "unidad medida" }, Opciones: new[] { "kg", "qq" }),
        new("Tipo Alimento",      Requerida: false),
        new(MixAlimento1,         Requerida: false),
        new(MixConsumoAlimento1,  Requerida: false),
        new(MixAlimento2,         Requerida: false),
        new(MixConsumoAlimento2,  Requerida: false),
        new(MixPeso,              Requerida: false),
        new(MixUniformidad,       Requerida: false),
        new("QQ Mixtas",          Requerida: false, Alias: new[] { "qq mixtas", "quintales mixtas" }),
        new("Observaciones",      Requerida: false),
    });

    /// <summary>
    /// Hoja <c>Alimento</c> del archivo de seguimiento pollo engorde: movimientos de INVENTARIO de
    /// alimento (ingresos a la granja/galpón, traslados y recepción de traslados en tránsito) que se
    /// aplican ANTES de las filas de consumo de la hoja <c>Datos</c>, para que el galpón tenga stock
    /// cuando el seguimiento lo descuenta.
    /// <para>
    /// Es una hoja OPCIONAL: un archivo sin ella se procesa exactamente como siempre. La ubicación
    /// (Granja/Núcleo/Galpón) también es opcional — vacía, el movimiento va a la ubicación del lote
    /// elegido en pantalla, que es el caso normal de "todo el alimento entró a este galpón".
    /// </para>
    /// </summary>
    public static EsquemaMigracion AlimentoEngorde { get; } = new("Alimento", new ColumnaEsquema[]
    {
        new("Fecha",           Requerida: true),
        new("Movimiento",      Requerida: false, Alias: new[] { "tipo movimiento", "tipo" },
                               Opciones: new[] { "Ingreso", "Traslado", "Recepción", "Consumo" }),
        new("Alimento",        Requerida: true,  Alias: new[] { "item", "producto", "tipo alimento" }),
        new("Cantidad",        Requerida: true,  Alias: new[] { "cantidad kg", "kg", "cantidad (kg)" }),
        new("Unidad",          Requerida: false, Alias: new[] { "unidad consumo", "unidad medida" }, Opciones: new[] { "kg", "qq" }),
        // Destino del movimiento. Vacío ⇒ ubicación del lote seleccionado en pantalla.
        new("Granja",          Requerida: false, Alias: new[] { "nombre granja", "granja destino" }),
        new("Núcleo",          Requerida: false, Alias: new[] { "nombre nucleo", "nucleo destino" }),
        new("Galpón",          Requerida: false, Alias: new[] { "nombre galpon", "galpon destino" }),
        // Origen: solo para Traslado / Recepción.
        new("Granja Origen",   Requerida: false, Alias: new[] { "granja de origen", "desde granja" }),
        new("Núcleo Origen",   Requerida: false, Alias: new[] { "nucleo de origen", "desde nucleo" }),
        new("Galpón Origen",   Requerida: false, Alias: new[] { "galpon de origen", "desde galpon" }),
        // Solo para Ingreso: de dónde viene (define el estado del movimiento en el histórico).
        new("Origen",          Requerida: false, Alias: new[] { "origen tipo", "procedencia" },
                               Opciones: new[] { "planta", "bodega", "granja" }),
        new("Referencia",      Requerida: false, Alias: new[] { "remision", "factura", "documento" }),
        new("Observaciones",   Requerida: false),
    });

    /// <summary>
    /// Seguimiento reproductora engorde (primera semana). El lote engorde sale de la fila (columnas de
    /// ubicación por NOMBRE, sin mayúsculas/acentos) o del seleccionado en pantalla; la columna
    /// "Reproductora" identifica el lote reproductora dentro de él (por id, código o nombre) y puede
    /// quedar vacía si en pantalla se eligió una reproductora puntual. Consumos en kg (el modal
    /// convierte qq→kg; acá lo hace la columna Unidad Consumo).
    /// </summary>
    public static EsquemaMigracion SeguimientoReproductoraEngorde { get; } = new("Datos", new ColumnaEsquema[]
    {
        new("Granja",             Requerida: false, Alias: new[] { "nombre granja" }),
        new("Núcleo",             Requerida: false, Alias: new[] { "nombre nucleo" }),
        new("Galpón",             Requerida: false, Alias: new[] { "nombre galpon" }),
        new("Lote",               Requerida: false, Alias: new[] { "nombre lote" }),
        new("Reproductora",       Requerida: false, Alias: new[] { "reproductora id", "repro", "codigo reproductora" }),
        new("Fecha",              Requerida: true),
        new("Mort H",             Requerida: false, Alias: new[] { "mortalidad hembras" }),
        new("Mort M",             Requerida: false, Alias: new[] { "mortalidad machos" }),
        new("Sel H",              Requerida: false),
        new("Sel M",              Requerida: false),
        new("Error Sexaje H",     Requerida: false),
        new("Error Sexaje M",     Requerida: false),
        new("Consumo H (kg)",     Requerida: false, Alias: new[] { "consumo h" }),
        new("Consumo M (kg)",     Requerida: false, Alias: new[] { "consumo m" }),
        // Unidad del consumo H/M: "kg" (default) o "qq" — con qq la carga convierte a kg (×45.36).
        new("Unidad Consumo",     Requerida: false, Alias: new[] { "unidad", "unidad de consumo", "unidad medida" }, Opciones: new[] { "kg", "qq" }),
        new("Tipo Alimento",      Requerida: false),
        // Hasta DOS alimentos del inventario por sexo, igual que en SeguimientoPolloEngorde: cuando la
        // fila los trae, el consumo DESCUENTA el stock del galpón. Sin ellos, "Consumo H/M (kg)" sigue
        // siendo el consumo directo de siempre (sin tocar inventario) — columnas opcionales, aditivas.
        // Necesario para que la PRIMERA SEMANA del lote engorde (que se digita en reproductora y luego
        // cruza) descuente el alimento realmente consumido en el galpón.
        new("Alimento 1 H",       Requerida: false, Alias: new[] { "alimento 1 hembras", "alimento uno hembras", MixAlimento1 }),
        new("Consumo Alimento 1 H", Requerida: false, Alias: new[] { "consumo 1 h", "consumo alimento uno hembras", MixConsumoAlimento1 }),
        new("Alimento 2 H",       Requerida: false, Alias: new[] { "alimento 2 hembras", "alimento dos hembras", MixAlimento2 }),
        new("Consumo Alimento 2 H", Requerida: false, Alias: new[] { "consumo 2 h", "consumo alimento dos hembras", MixConsumoAlimento2 }),
        new("Alimento 1 M",       Requerida: false, Alias: new[] { "alimento 1 machos", "alimento uno machos" }),
        new("Consumo Alimento 1 M", Requerida: false, Alias: new[] { "consumo 1 m", "consumo alimento uno machos" }),
        new("Alimento 2 M",       Requerida: false, Alias: new[] { "alimento 2 machos", "alimento dos machos" }),
        new("Consumo Alimento 2 M", Requerida: false, Alias: new[] { "consumo 2 m", "consumo alimento dos machos" }),
        new("Peso H (g)",         Requerida: false, Alias: new[] { "peso h" }),
        new("Peso M (g)",         Requerida: false, Alias: new[] { "peso m" }),
        new("Uniformidad H",      Requerida: false),
        new("Uniformidad M",      Requerida: false),
        new("CV H",               Requerida: false, Alias: new[] { "cv hembras" }),
        new("CV M",               Requerida: false, Alias: new[] { "cv machos" }),
        new("Observaciones",      Requerida: false),
    });

    /// <summary>
    /// Hoja <c>Reproductora</c> del archivo de seguimiento pollo engorde: MISMAS columnas y reglas que
    /// <see cref="SeguimientoReproductoraEngorde"/>, solo cambia el nombre de la hoja.
    /// <para>
    /// Existe para poder cargar TODO el lote en un archivo: la primera semana (que se digita en
    /// reproductora y luego cruza a engorde) en la hoja <c>Reproductora</c>, los días 8 en adelante en
    /// <c>Datos</c> y el inventario en <c>Alimento</c>. El parseo es literalmente el mismo código
    /// (<c>ParsearFilasReproductoraAsync</c>), así que validar por una vía u otra da idéntico resultado.
    /// </para>
    /// </summary>
    public static EsquemaMigracion ReproductoraEnHoja { get; } =
        SeguimientoReproductoraEngorde with { Hoja = "Reproductora" };

    /// <summary>
    /// Venta de pollo engorde. Todas las columnas son opcionales salvo la fecha ⇒ un archivo con
    /// las 11 columnas históricas (Fecha…Observaciones) sigue siendo válido.
    /// <para>
    /// MULTI-LOTE: si la fila trae "Lote" se resuelve por nombre acotado por Granja/Núcleo/Galpón;
    /// sin "Lote" se usa el lote seleccionado en pantalla. Las filas que comparten
    /// "N° Despacho" + Fecha + Granja forman UN despacho: se les asigna una misma factura y el peso
    /// báscula (que es el del camión) se prorratea entre ellas por aves, igual que una venta hecha
    /// por pantalla.
    /// </para>
    /// </summary>
    public static EsquemaMigracion VentaPolloEngorde { get; } = new("Datos", new ColumnaEsquema[]
    {
        // Ubicación por NOMBRES (opcional): mismo mecanismo que SeguimientoPolloEngorde.
        new("Granja",             Requerida: false, Alias: new[] { "nombre granja" }),
        new("Núcleo",             Requerida: false, Alias: new[] { "nombre nucleo" }),
        new("Galpón",             Requerida: false, Alias: new[] { "nombre galpon" }),
        new("Lote",               Requerida: false, Alias: new[] { "nombre lote" }),
        new("Fecha",              Requerida: true),
        new("Cantidad H",         Requerida: false, Alias: new[] { "cant h", "hembras" }),
        new("Cantidad M",         Requerida: false, Alias: new[] { "cant m", "machos" }),
        new("Cantidad Mixtas",    Requerida: false, Alias: new[] { "cant mixtas", "mixtas" }),
        new("Motivo",             Requerida: false),
        new("Peso Bruto (kg)",    Requerida: false, Alias: new[] { "peso bruto" }),
        new("Peso Tara (kg)",     Requerida: false, Alias: new[] { "peso tara" }),
        new("Edad Aves",          Requerida: false, Alias: new[] { "edad" }),
        new("Raza",               Requerida: false),
        new("Placa",              Requerida: false),
        new("Observaciones",      Requerida: false),
        // ── Datos del despacho (mismos campos del formulario de venta) ──
        new("N° Despacho",        Requerida: false, Alias: new[] { "no despacho", "nro despacho", "numero despacho", "despacho" }),
        new("Total Pollos Galpón",Requerida: false, Alias: new[] { "total pollos galpon", "total pollos" }),
        new("Hora Salida",        Requerida: false, Alias: new[] { "hora" }),
        new("Guía Agrocalidad",   Requerida: false, Alias: new[] { "guia agrocalidad", "guia" }),
        new("Sellos",             Requerida: false),
        new("Ayuno",              Requerida: false),
        new("Cliente / Conductor",Requerida: false, Alias: new[] { "cliente / conductor", "cliente", "conductor" }),
        new("Planta Destino",     Requerida: false, Alias: new[] { "planta" }),
        new("Descripción",        Requerida: false, Alias: new[] { "descripcion" }),
        // Estado con el que nace la venta. "Completado" (default) = comportamiento histórico:
        // descuenta las aves del lote. "Pendiente" = venta a la espera de la báscula; el descuento
        // ocurre al confirmarla desde la pantalla de movimientos.
        new("Estado",             Requerida: false, Opciones: new[] { "Completado", "Pendiente" }),
        // Venta sobre MIXTAS (Panamá): el split H/M se asigna sobre las mixtas del lote; el stock
        // sale de mixtas y no de hembras_l/machos_l. Espeja EsVentaMixta de la venta por pantalla.
        new("Venta sobre mixtas", Requerida: false, Alias: new[] { "es venta mixta", "sobre mixtas", "panama" }),
    });

    /// <summary>Devuelve el esquema correspondiente a un tipo de migración implementado.</summary>
    public static EsquemaMigracion Para(TipoMigracion tipo) => tipo switch
    {
        TipoMigracion.Granjas => Granjas,
        TipoMigracion.Nucleos => Nucleos,
        TipoMigracion.Galpones => Galpones,
        TipoMigracion.SeguimientoLevante => SeguimientoLevante,
        TipoMigracion.SeguimientoProduccion => SeguimientoProduccion,
        TipoMigracion.LotesPolloEngorde => LotesPolloEngorde,
        TipoMigracion.SeguimientoPolloEngorde => SeguimientoPolloEngorde,
        TipoMigracion.SeguimientoReproductoraEngorde => SeguimientoReproductoraEngorde,
        TipoMigracion.VentaPolloEngorde => VentaPolloEngorde,
        _ => throw new NotSupportedException($"El tipo de migración '{tipo}' no tiene esquema (Fase 3: Ventas/Movimientos, aún no implementada)."),
    };

    /// <summary>Los 9 tipos con esquema implementado (para recorrer en tests).</summary>
    public static IReadOnlyList<TipoMigracion> TiposConEsquema { get; } = new[]
    {
        TipoMigracion.Granjas,
        TipoMigracion.Nucleos,
        TipoMigracion.Galpones,
        TipoMigracion.SeguimientoLevante,
        TipoMigracion.SeguimientoProduccion,
        TipoMigracion.LotesPolloEngorde,
        TipoMigracion.SeguimientoPolloEngorde,
        TipoMigracion.SeguimientoReproductoraEngorde,
        TipoMigracion.VentaPolloEngorde,
    };
}
