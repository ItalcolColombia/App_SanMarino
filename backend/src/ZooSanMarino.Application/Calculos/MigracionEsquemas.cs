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
        new("Mort H",             Requerida: false, Alias: new[] { "mortalidad hembras" }),
        new("Mort M",             Requerida: false, Alias: new[] { "mortalidad machos" }),
        new("Sel H",              Requerida: false),
        new("Sel M",              Requerida: false),
        new("Error Sexaje H",     Requerida: false),
        new("Error Sexaje M",     Requerida: false),
        new("Consumo H (kg)",     Requerida: false, Alias: new[] { "consumo h" }),
        new("Consumo M (kg)",     Requerida: false, Alias: new[] { "consumo m" }),
        // Unidad del consumo (directo y por alimento): "kg" (default) o "qq" — con qq se convierte a kg (×45.36).
        new("Unidad Consumo",     Requerida: false, Alias: new[] { "unidad", "unidad de consumo", "unidad medida" }, Opciones: new[] { "kg", "qq" }),
        new("Tipo Alimento",      Requerida: false),
        // Hasta DOS alimentos del inventario por sexo y fecha: el nombre/código se busca en los ítems
        // de concepto alimento de la empresa (sin mayúsculas/acentos) y descuenta inventario al importar.
        new("Alimento 1 H",       Requerida: false, Alias: new[] { "alimento 1 hembras", "alimento uno hembras" }),
        new("Consumo Alimento 1 H", Requerida: false, Alias: new[] { "consumo 1 h", "consumo alimento uno hembras" }),
        new("Alimento 2 H",       Requerida: false, Alias: new[] { "alimento 2 hembras", "alimento dos hembras" }),
        new("Consumo Alimento 2 H", Requerida: false, Alias: new[] { "consumo 2 h", "consumo alimento dos hembras" }),
        new("Alimento 1 M",       Requerida: false, Alias: new[] { "alimento 1 machos", "alimento uno machos" }),
        new("Consumo Alimento 1 M", Requerida: false, Alias: new[] { "consumo 1 m", "consumo alimento uno machos" }),
        new("Alimento 2 M",       Requerida: false, Alias: new[] { "alimento 2 machos", "alimento dos machos" }),
        new("Consumo Alimento 2 M", Requerida: false, Alias: new[] { "consumo 2 m", "consumo alimento dos machos" }),
        new("Peso H (g)",         Requerida: false, Alias: new[] { "peso h" }),
        new("Peso M (g)",         Requerida: false, Alias: new[] { "peso m" }),
        new("Uniformidad H",      Requerida: false),
        new("Uniformidad M",      Requerida: false),
        // Panamá: alimento en quintales por categoría (persisten en qq_*; opcionales para CO/EC).
        new("QQ Mixtas",          Requerida: false, Alias: new[] { "qq mixtas", "quintales mixtas" }),
        new("QQ H",               Requerida: false, Alias: new[] { "qq hembras", "quintales hembras" }),
        new("QQ M",               Requerida: false, Alias: new[] { "qq machos", "quintales machos" }),
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

    public static EsquemaMigracion VentaPolloEngorde { get; } = new("Datos", new ColumnaEsquema[]
    {
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
