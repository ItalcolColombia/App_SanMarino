// src/ZooSanMarino.Application/Calculos/GuiaGeneticaSantaReyesCalculos.cs
// Lógica PURA de la guía genética reducida (tabla plana de 3 métricas): clave natural, parseo del
// Excel y decisión de upsert. Sin EF, sin estado, sin I/O.
using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Qué hace el import con una fila, una vez resuelta su clave natural.</summary>
public enum AccionImportGuiaSantaReyes
{
    /// <summary>El código no existe para la empresa ⇒ alta.</summary>
    Insertar,

    /// <summary>El código existe y al menos una métrica cambió ⇒ actualización.</summary>
    Actualizar,

    /// <summary>El código existe y las tres métricas son idénticas ⇒ no se toca la fila.</summary>
    OmitirSinCambios
}

/// <summary>Las tres métricas de negocio de una línea de la guía reducida.</summary>
/// <param name="ProdPorcentaje">% de producción de la semana. <c>null</c> ≠ 0: la línea no tiene dato.</param>
/// <param name="RetiroAcH">% de mortalidad ACUMULADA de hembras a esa semana.</param>
/// <param name="GrAveDiaH">Consumo en gramos/ave/día de hembras a esa semana.</param>
public readonly record struct MetricasGuiaSantaReyes(
    decimal? ProdPorcentaje,
    decimal? RetiroAcH,
    decimal? GrAveDiaH);

/// <summary>Una fila del Excel ya validada y tipada, lista para el upsert.</summary>
public sealed record FilaImportGuiaSantaReyes(
    string Raza,
    string AnioGuia,
    int Edad,
    MetricasGuiaSantaReyes Metricas,
    string Codigo);

/// <summary>
/// Qué se hace con una fila del Excel: saltearla por vacía, rechazarla con un motivo, o importarla.
/// </summary>
/// <param name="EsVacia">
/// La fila no tiene una sola celda con contenido. No es un error: Excel arrastra filas en blanco al
/// final de cualquier hoja editada a mano, y contarlas como error convertiría todo import en «120
/// errores» ilegibles.
/// </param>
/// <param name="Fila">La fila tipada, si es importable.</param>
/// <param name="Motivo">Por qué se rechaza, en el idioma del usuario. <c>null</c> si no se rechaza.</param>
public sealed record ResultadoFilaImportGuiaSantaReyes(
    bool EsVacia,
    FilaImportGuiaSantaReyes? Fila,
    string? Motivo);

/// <summary>
/// Reglas puras de la guía genética <b>reducida</b> (<c>guia_genetica_santa_reyes</c>): la clave
/// natural con la que el import es idempotente, el parseo de las celdas del Excel y la decisión de
/// insertar / actualizar / no tocar.
///
/// <para>
/// <b>Por qué vive en Application/Calculos y no en el service:</b> es aritmética y texto, sin EF ni
/// <c>_ctx</c>, y es exactamente lo que los tests tienen que poder afirmar sin levantar una base
/// (CLAUDE.md §🧩). El service de Infrastructure orquesta: lee el Excel, consulta, guarda.
/// </para>
/// </summary>
public static class GuiaGeneticaSantaReyesCalculos
{
    // ─────────────────────────────────────────────────────────────────────────
    // Clave natural
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Clave natural de una línea: <c>Raza + AnioGuia + Edad</c>, sin separadores.
    ///
    /// <para>
    /// 🔴 Es <b>la misma fórmula</b> que <c>ExcelImportService.ComputeCodigo</c>
    /// (<c>ExcelImportService.cs:491-497</c>: <c>$"{Raza.Trim()}{AnioGuia.Trim()}{Edad.Trim()}"</c>) y
    /// que el seed <c>20260820093323_SeedGuiaGeneticaSantaReyes</c>
    /// (<c>'Babcock Brown202618'</c>). Contra el UNIQUE parcial
    /// <c>ux_guia_genetica_santa_reyes_codigo (company_id, codigo_guia_genetica)
    /// WHERE deleted_at IS NULL AND codigo_guia_genetica IS NOT NULL</c>, esto es lo que hace que
    /// reimportar el mismo archivo <b>actualice en vez de duplicar</b>.
    /// </para>
    ///
    /// <para>
    /// <b>La única diferencia con la tabla compartida</b> es que acá <c>edad</c> es una columna
    /// <c>int</c>, así que siempre se renderiza canónicamente («18»). En la compartida es
    /// <c>varchar</c> y convive «25» con «25P», por eso allá el código sale del texto tal cual.
    /// </para>
    /// </summary>
    /// <returns>El código, o <c>null</c> si falta raza o año (sin ellos no hay clave que valga).</returns>
    public static string? CodigoNatural(string? raza, string? anioGuia, int edad)
    {
        if (string.IsNullOrWhiteSpace(raza) || string.IsNullOrWhiteSpace(anioGuia)) return null;
        return $"{raza.Trim()}{anioGuia.Trim()}{edad.ToString(CultureInfo.InvariantCulture)}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Decisión del upsert
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Qué hacer con una fila cuya clave natural ya se resolvió.
    ///
    /// <para>
    /// Sólo se comparan las tres métricas: si el <b>código</b> coincide, raza/año/edad coinciden por
    /// construcción (el código se deriva de ellos). Una fila idéntica se deja intacta —
    /// <see cref="AccionImportGuiaSantaReyes.OmitirSinCambios"/>— en vez de reescribirla: marcar 615
    /// filas como modificadas en cada reimport ensucia <c>updated_at</c> y miente sobre lo que pasó.
    /// El invariante que pide el plan se cumple igual: mismo archivo dos veces ⇒ <b>cero altas</b> y
    /// el mismo conteo de filas.
    /// </para>
    ///
    /// <para>
    /// La comparación de <c>decimal?</c> es por valor y <b>ignora la escala</b> (<c>95.0m == 95.00m</c>),
    /// que es justo lo que hace falta: la columna es <c>numeric(6,2)</c> y devuelve «95.00» donde el
    /// Excel decía «95».
    /// </para>
    /// </summary>
    public static AccionImportGuiaSantaReyes DecidirAccion(
        MetricasGuiaSantaReyes? existente,
        MetricasGuiaSantaReyes entrante)
    {
        if (existente is not MetricasGuiaSantaReyes actual) return AccionImportGuiaSantaReyes.Insertar;

        var iguales =
            actual.ProdPorcentaje == entrante.ProdPorcentaje &&
            actual.RetiroAcH == entrante.RetiroAcH &&
            actual.GrAveDiaH == entrante.GrAveDiaH;

        return iguales
            ? AccionImportGuiaSantaReyes.OmitirSinCambios
            : AccionImportGuiaSantaReyes.Actualizar;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parseo de celdas
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parsea una celda numérica opcional.
    ///
    /// <para>
    /// 🔴 <b>Celda vacía ⇒ <c>null</c>, NUNCA 0.</b> No es una sutileza de estilo: la raza Criolla
    /// tiene 40 filas legítimamente nulas en <c>prod_porcentaje</c> (semanas 101–140, se apaga antes
    /// que las demás). Escribir 0 ahí diría «esta ave puso cero huevos esa semana» en vez de «no hay
    /// guía para esa semana», y los reportes lo promediarían como un dato real.
    /// </para>
    ///
    /// <para>
    /// Tolera lo que trae un Excel del cliente: espacios, símbolo <c>%</c>, y coma o punto como
    /// separador decimal — el mismo criterio de <c>ProduccionAvicolaRawService.ParseNumber</c>, para
    /// que el mismo archivo se lea igual en los dos módulos.
    /// </para>
    /// </summary>
    /// <returns><c>true</c> si la celda es interpretable (vacía incluida); <c>false</c> si tiene basura.</returns>
    public static bool TryParsearDecimalOpcional(string? crudo, out decimal? valor)
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(crudo)) return true;

        var limpio = crudo.Trim().Replace(" ", "").Replace("%", "");
        if (limpio.Length == 0) return true;

        limpio = NormalizarSeparadoresDecimales(limpio);

        if (!decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return false;

        valor = d;
        return true;
    }

    /// <summary>
    /// Parsea la semana de vida. Acepta «18», «18.0» y «18,0» (Excel devuelve los enteros de una
    /// celda numérica como «18» o como «18.0» según el formato de la celda) y trunca a entero.
    /// </summary>
    /// <returns><c>true</c> si hay un entero interpretable; <c>false</c> si está vacía o es basura.</returns>
    public static bool TryParsearEdad(string? crudo, out int edad)
    {
        edad = 0;
        if (string.IsNullOrWhiteSpace(crudo)) return false;

        var limpio = NormalizarSeparadoresDecimales(crudo.Trim().Replace(" ", ""));
        if (!decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return false;

        var truncado = Math.Truncate(d);
        if (truncado < int.MinValue || truncado > int.MaxValue) return false;

        edad = (int)truncado;
        return true;
    }

    /// <summary>
    /// Decide el separador decimal cuando conviven punto y coma: manda la ÚLTIMA aparición.
    /// Copiado en criterio de <c>ProduccionAvicolaRawService.NormalizeDecimalSeparators</c> para que
    /// «1.234,56» y «1,234.56» se lean igual en los dos módulos.
    /// </summary>
    private static string NormalizarSeparadoresDecimales(string entrada)
    {
        var tienePunto = entrada.Contains('.');
        var tieneComa = entrada.Contains(',');

        if (tienePunto && tieneComa)
        {
            return entrada.LastIndexOf(',') > entrada.LastIndexOf('.')
                ? entrada.Replace(".", "").Replace(",", ".")   // coma decimal, punto de miles
                : entrada.Replace(",", "");                    // punto decimal, coma de miles
        }

        return tieneComa ? entrada.Replace(",", ".") : entrada;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Validación de una fila del Excel
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Columnas del archivo, en el orden de la plantilla que descarga el usuario.</summary>
    public static IReadOnlyList<string> ColumnasPlantilla { get; } = new[]
    {
        ColumnaRaza, ColumnaAnioGuia, ColumnaEdad,
        ColumnaProdPorcentaje, ColumnaRetiroAcH, ColumnaGrAveDiaH
    };

    /// <summary>Nombre canónico (snake_case, como la columna en base) de cada campo del archivo.</summary>
    public const string ColumnaRaza = "raza";

    /// <inheritdoc cref="ColumnaRaza"/>
    public const string ColumnaAnioGuia = "anio_guia";

    /// <inheritdoc cref="ColumnaRaza"/>
    public const string ColumnaEdad = "edad";

    /// <inheritdoc cref="ColumnaRaza"/>
    public const string ColumnaProdPorcentaje = "prod_porcentaje";

    /// <inheritdoc cref="ColumnaRaza"/>
    public const string ColumnaRetiroAcH = "retiro_ac_h";

    /// <inheritdoc cref="ColumnaRaza"/>
    public const string ColumnaGrAveDiaH = "gr_ave_dia_h";

    /// <summary>
    /// Encabezados que se reconocen, ya normalizados (minúsculas, sin acentos, sin espacios ni
    /// separadores). Se aceptan la grafía de la plantilla y la del «Excel rojo» del cliente, igual
    /// que <c>ExcelColumnMappings</c> del módulo compartido: pedirle al usuario que renombre sus
    /// columnas es la forma más barata de que el import «no funcione».
    /// </summary>
    private static readonly Dictionary<string, string> EncabezadosConocidos = new(StringComparer.Ordinal)
    {
        ["raza"] = ColumnaRaza,
        ["linea"] = ColumnaRaza,
        ["lineagenetica"] = ColumnaRaza,

        ["anioguia"] = ColumnaAnioGuia,
        ["anio"] = ColumnaAnioGuia,
        ["ano"] = ColumnaAnioGuia,
        ["anoguia"] = ColumnaAnioGuia,
        ["guia"] = ColumnaAnioGuia,

        ["edad"] = ColumnaEdad,
        ["semana"] = ColumnaEdad,
        ["edadsemanas"] = ColumnaEdad,

        ["prodporcentaje"] = ColumnaProdPorcentaje,
        ["prod"] = ColumnaProdPorcentaje,
        ["produccion"] = ColumnaProdPorcentaje,
        ["porcentajeproduccion"] = ColumnaProdPorcentaje,

        ["retiroach"] = ColumnaRetiroAcH,
        ["retiroacumulado"] = ColumnaRetiroAcH,
        ["mortalidadacumulada"] = ColumnaRetiroAcH,

        ["gravediah"] = ColumnaGrAveDiaH,
        ["gravedia"] = ColumnaGrAveDiaH,
        ["consumo"] = ColumnaGrAveDiaH,
        ["consumogravedia"] = ColumnaGrAveDiaH
    };

    /// <summary>
    /// Nombre canónico de la columna que corresponde a un encabezado del Excel, o <c>null</c> si no
    /// se reconoce (la columna se ignora, no rompe el import).
    /// </summary>
    public static string? MapearEncabezado(string? encabezado)
    {
        var clave = NormalizarEncabezado(encabezado);
        if (clave.Length == 0) return null;
        return EncabezadosConocidos.TryGetValue(clave, out var canonico) ? canonico : null;
    }

    /// <summary>Minúsculas, sin acentos y sin nada que no sea letra o dígito.</summary>
    private static string NormalizarEncabezado(string? encabezado)
    {
        if (string.IsNullOrWhiteSpace(encabezado)) return string.Empty;

        var sb = new System.Text.StringBuilder(encabezado.Length);
        foreach (var c in encabezado.Trim().ToLowerInvariant())
        {
            var reemplazo = c switch
            {
                'á' or 'à' or 'ä' or 'â' => 'a',
                'é' or 'è' or 'ë' or 'ê' => 'e',
                'í' or 'ì' or 'ï' or 'î' => 'i',
                'ó' or 'ò' or 'ö' or 'ô' => 'o',
                'ú' or 'ù' or 'ü' or 'û' => 'u',
                'ñ' => 'n',
                _ => c
            };

            if (char.IsLetterOrDigit(reemplazo)) sb.Append(reemplazo);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Interpreta una fila cruda del Excel: la saltea si está vacía, la rechaza con un motivo legible
    /// o la devuelve tipada y con su clave natural ya calculada.
    /// </summary>
    public static ResultadoFilaImportGuiaSantaReyes InterpretarFila(
        string? raza,
        string? anioGuia,
        string? edad,
        string? prodPorcentaje,
        string? retiroAcH,
        string? grAveDiaH)
    {
        var celdas = new[] { raza, anioGuia, edad, prodPorcentaje, retiroAcH, grAveDiaH };
        if (celdas.All(string.IsNullOrWhiteSpace))
            return new ResultadoFilaImportGuiaSantaReyes(EsVacia: true, Fila: null, Motivo: null);

        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(raza)) faltantes.Add(ColumnaRaza);
        if (string.IsNullOrWhiteSpace(anioGuia)) faltantes.Add(ColumnaAnioGuia);
        if (string.IsNullOrWhiteSpace(edad)) faltantes.Add(ColumnaEdad);

        if (faltantes.Count > 0)
            return Rechazo($"Faltan campos clave: {string.Join(", ", faltantes)}.");

        if (!TryParsearEdad(edad, out var edadNumerica))
            return Rechazo($"«{ColumnaEdad}» no es un número entero: «{edad!.Trim()}».");

        if (edadNumerica <= 0)
            return Rechazo($"«{ColumnaEdad}» debe ser mayor que cero (llegó {edadNumerica}).");

        if (!TryParsearDecimalOpcional(prodPorcentaje, out var prod))
            return Rechazo($"«{ColumnaProdPorcentaje}» no es un número: «{prodPorcentaje!.Trim()}».");

        if (!TryParsearDecimalOpcional(retiroAcH, out var retiro))
            return Rechazo($"«{ColumnaRetiroAcH}» no es un número: «{retiroAcH!.Trim()}».");

        if (!TryParsearDecimalOpcional(grAveDiaH, out var gramos))
            return Rechazo($"«{ColumnaGrAveDiaH}» no es un número: «{grAveDiaH!.Trim()}».");

        var razaLimpia = raza!.Trim();
        var anioLimpio = anioGuia!.Trim();
        var codigo = CodigoNatural(razaLimpia, anioLimpio, edadNumerica);

        if (string.IsNullOrWhiteSpace(codigo))
            return Rechazo("No se pudo calcular el código de la guía (requiere raza + anio_guia + edad).");

        return new ResultadoFilaImportGuiaSantaReyes(
            EsVacia: false,
            Fila: new FilaImportGuiaSantaReyes(
                razaLimpia,
                anioLimpio,
                edadNumerica,
                new MetricasGuiaSantaReyes(prod, retiro, gramos),
                codigo!),
            Motivo: null);
    }

    private static ResultadoFilaImportGuiaSantaReyes Rechazo(string motivo) =>
        new(EsVacia: false, Fila: null, Motivo: motivo);
}
