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
    });

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
        new("Peso H (g)",         Requerida: false, Alias: new[] { "peso h" }),
        new("Peso M (g)",         Requerida: false, Alias: new[] { "peso m" }),
        new("Uniformidad H",      Requerida: false),
        new("Uniformidad M",      Requerida: false),
        new("CV H",               Requerida: false, Alias: new[] { "cv hembras" }),
        new("CV M",               Requerida: false, Alias: new[] { "cv machos" }),
        new("Observaciones",      Requerida: false),
    });

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
