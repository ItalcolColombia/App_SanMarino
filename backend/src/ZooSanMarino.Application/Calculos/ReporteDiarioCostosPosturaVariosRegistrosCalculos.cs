// src/ZooSanMarino.Application/Calculos/ReporteDiarioCostosPosturaVariosRegistrosCalculos.cs
using System.Text.RegularExpressions;
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosPostura;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// ESPECIFICACIÓN EJECUTABLE de <c>fn_reporte_diario_costos_postura</c> v3 para los días con VARIOS
/// registros (flag <c>companies.permite_multiples_seguimientos_diarios</c>, hoy solo Santa Reyes). La fn
/// SQL es la dueña del número; esta clase es su contrato (regla «una sola fórmula por número»).
///
/// Reglas (plan reporte_diario_costos_postura_varios_registros_dia):
///  • Levante, fila del día: sin el flag gana UN registro — el más temprano por (fecha, id), el
///    <c>DISTINCT ON</c> de siempre —; con el flag se SUMAN mortalidad, selección, error de sexaje,
///    venta de aves y consumo de todos los registros del día.
///  • Alimentos: una entrada por ítem de CADA registro; el fallback por <c>tipo_alimento</c> se decide
///    por registro (sin ítems de ese sexo pero con kg ⇒ entrada con SUS kg).
///  • Producción, venta de aves: sin el flag la del registro <c>seg_id</c> de la fn; con el flag la suma
///    de los registros de <c>seguimiento_diario_produccion</c> del día.
/// Con UN registro el día cada regla da lo mismo con y sin flag. Sin EF ni estado: funciones puras.
/// </summary>
public static class ReporteDiarioCostosPosturaVariosRegistrosCalculos
{
    public const string SexoHembras = "H";
    public const string SexoMachos = "M";
    public const string OrigenMetadata = "metadata";
    public const string OrigenTipoAlimento = "tipo_alimento";
    public const string SinEspecificar = "Sin especificar";

    /// <summary>Ítem de <c>metadata.itemsHembras/itemsMachos</c> con el nombre ya resuelto
    /// (<c>nombre</c> del json → ítem de inventario → catálogo; null si ninguno lo trae).</summary>
    public sealed record ItemAlimento(string? Nombre, double CantidadKg);

    /// <summary>
    /// Un registro crudo del día. Los enteros ya vienen con NULL ⇒ 0 (el <c>COALESCE</c> de la fn).
    /// <c>ItemsH/ItemsM</c> null o vacío = la metadata no trae ítems de ese sexo.
    /// <c>Fuente</c> = 'sdl' (seguimiento_diario_levante) | 'sdp' (seguimiento_diario_produccion): los ids
    /// de las dos tablas pueden coincidir, el registro se identifica por el par.
    /// </summary>
    public sealed record RegistroDia(
        string Fuente,
        long Id,
        DateTimeOffset Fecha,
        int MortH = 0, int MortM = 0,
        int SelH = 0, int SelM = 0,
        int ErrH = 0, int ErrM = 0,
        int VentaH = 0, int VentaM = 0,
        decimal ConsKgH = 0m, decimal ConsKgM = 0m,
        string? TipoAlimento = null,
        IReadOnlyList<ItemAlimento>? ItemsH = null,
        IReadOnlyList<ItemAlimento>? ItemsM = null);

    /// <summary>Pestañas Aves y Alimento de una fila del reporte.</summary>
    public sealed record FilaDia(
        int MortalidadH, int MortalidadM,
        int SeleccionH, int SeleccionM,
        int ErrorSexajeH, int ErrorSexajeM,
        int VentaAvesH, int VentaAvesM,
        double ConsumoKgH, double ConsumoKgM);

    /// <summary>
    /// Registros que aportan a la fila del día: sin el flag SOLO el más temprano por (fecha, id) — espejo
    /// de <c>lev_dedup</c>; con el flag todos (<c>lev_agrupado</c>), en orden de carga.
    /// </summary>
    public static IReadOnlyList<RegistroDia> RegistrosQueAportan(
        IEnumerable<RegistroDia> registrosDelDia, bool permiteMultiples)
    {
        var ordenados = registrosDelDia
            .OrderBy(r => r.Fecha)
            .ThenBy(r => r.Fuente, StringComparer.Ordinal)
            .ThenBy(r => r.Id)
            .ToList();
        return permiteMultiples || ordenados.Count <= 1 ? ordenados : ordenados.Take(1).ToList();
    }

    /// <summary>Fila de LEVANTE del día — espejo de <c>lev_dias</c> (<c>lev_dedup</c> ∪ <c>lev_agrupado</c>).</summary>
    public static FilaDia FilaLevanteDelDia(IEnumerable<RegistroDia> registrosDelDia, bool permiteMultiples)
    {
        var usados = RegistrosQueAportan(registrosDelDia, permiteMultiples);
        return new FilaDia(
            MortalidadH: usados.Sum(r => r.MortH),
            MortalidadM: usados.Sum(r => r.MortM),
            SeleccionH: usados.Sum(r => r.SelH),
            SeleccionM: usados.Sum(r => r.SelM),
            ErrorSexajeH: usados.Sum(r => r.ErrH),
            ErrorSexajeM: usados.Sum(r => r.ErrM),
            VentaAvesH: usados.Sum(r => r.VentaH),
            VentaAvesM: usados.Sum(r => r.VentaM),
            // SUM(numeric) y recién después ::float8, como la fn.
            ConsumoKgH: (double)usados.Sum(r => r.ConsKgH),
            ConsumoKgM: (double)usados.Sum(r => r.ConsKgM));
    }

    /// <summary>
    /// Json <c>alimentos</c> del día — espejo de <c>items_metadata</c> + <c>items_fallback</c> +
    /// <c>alimentos_json</c> sobre los registros que aportan. Orden: sexo, nombre, y a igualdad el orden de
    /// carga del registro (fecha, fuente, id). El nombre se compara ordinal; la fn ordena con la collation
    /// de la BD — coinciden para nombres en mayúsculas sin acentos, que es lo que usan los tests.
    /// </summary>
    public static IReadOnlyList<ReporteDiarioCostosPosturaAlimentoDto> AlimentosDelDia(
        IEnumerable<RegistroDia> registrosQueAportan)
    {
        var entradas = new List<(RegistroDia Reg, ReporteDiarioCostosPosturaAlimentoDto Dto)>();

        foreach (var r in registrosQueAportan)
        {
            var (nombreH, nombreM) = PartirTipoAlimento(r.TipoAlimento);
            Agregar(r, SexoHembras, r.ItemsH, r.ConsKgH, nombreH);
            Agregar(r, SexoMachos, r.ItemsM, r.ConsKgM, nombreM);
        }

        return entradas
            .OrderBy(e => e.Dto.Sexo, StringComparer.Ordinal)
            .ThenBy(e => e.Dto.Nombre, StringComparer.Ordinal)
            .ThenBy(e => e.Reg.Fecha)
            .ThenBy(e => e.Reg.Fuente, StringComparer.Ordinal)
            .ThenBy(e => e.Reg.Id)
            .Select(e => e.Dto)
            .ToList();

        void Agregar(RegistroDia r, string sexo, IReadOnlyList<ItemAlimento>? items, decimal consumoKg, string? nombreFallback)
        {
            if (items is { Count: > 0 })
            {
                foreach (var it in items)
                    entradas.Add((r, new ReporteDiarioCostosPosturaAlimentoDto(
                        sexo, NombreONulo(it.Nombre) ?? SinEspecificar, it.CantidadKg, OrigenMetadata)));
                return;
            }

            // Nunca se descarta consumo: sin ítems de ese sexo pero con kg ⇒ entrada de fallback.
            if (consumoKg != 0m)
                entradas.Add((r, new ReporteDiarioCostosPosturaAlimentoDto(
                    sexo, nombreFallback ?? SinEspecificar, (double)consumoKg, OrigenTipoAlimento)));
        }
    }

    /// <summary>
    /// Venta de aves de una fila de PRODUCCIÓN. Sin el flag: la del registro cuyo id es el <c>seg_id</c> de
    /// <c>fn_seguimiento_diario_produccion</c> (0 si no hay); con el flag: la suma de los registros de
    /// <c>seguimiento_diario_produccion</c> del día (la fila de la fn agrupa N registros y su seg_id es el primero).
    /// </summary>
    public static (int Hembras, int Machos) VentaAvesProduccionDelDia(
        IReadOnlyCollection<RegistroDia> registrosProduccionDelDia, long? segIdDeLaFn, bool permiteMultiples)
    {
        if (permiteMultiples)
            return (registrosProduccionDelDia.Sum(r => r.VentaH), registrosProduccionDelDia.Sum(r => r.VentaM));

        var reg = registrosProduccionDelDia.FirstOrDefault(r => r.Id == segIdDeLaFn);
        return reg is null ? (0, 0) : (reg.VentaH, reg.VentaM);
    }

    private static readonly Regex PrefijoHembras = new(@"^\s*H\s*:\s*", RegexOptions.CultureInvariant);

    /// <summary>
    /// Nombre de fallback por sexo a partir de <c>tipo_alimento</c> — espejo del CASE de <c>items_fallback</c>:
    /// <c>"H: x + y / M: z"</c> ⇒ (x + y, z); <c>"x / y"</c> ⇒ (x, y); otro texto ⇒ el mismo para los dos
    /// sexos (sin quitar un prefijo "H:" suelto). Vacío ⇒ null.
    /// </summary>
    public static (string? Hembras, string? Machos) PartirTipoAlimento(string? tipoAlimento)
    {
        var tipo = tipoAlimento ?? string.Empty;

        if (tipo.Contains("/ M:", StringComparison.Ordinal))
            return (NombreONulo(PrefijoHembras.Replace(SplitPart(tipo, "/ M:", 1), string.Empty, 1)),
                    NombreONulo(SplitPart(tipo, "/ M:", 2)));

        if (tipo.Contains(" / ", StringComparison.Ordinal))
            return (NombreONulo(SplitPart(tipo, " / ", 1)), NombreONulo(SplitPart(tipo, " / ", 2)));

        return (NombreONulo(tipo), NombreONulo(tipo));
    }

    /// <summary><c>split_part</c> de Postgres: campo n (base 1), vacío si no existe.</summary>
    private static string SplitPart(string texto, string separador, int campo)
    {
        var partes = texto.Split(separador);
        return campo <= partes.Length ? partes[campo - 1] : string.Empty;
    }

    /// <summary><c>NULLIF(TRIM(x), '')</c> — el TRIM de Postgres solo quita espacios.</summary>
    private static string? NombreONulo(string? texto)
    {
        var t = (texto ?? string.Empty).Trim(' ');
        return t.Length == 0 ? null : t;
    }
}
