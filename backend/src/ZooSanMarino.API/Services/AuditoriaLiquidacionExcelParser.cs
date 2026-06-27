// src/ZooSanMarino.API/Services/AuditoriaLiquidacionExcelParser.cs
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace ZooSanMarino.API.Services;

/// <summary>
/// Parsea el Excel "correcto" de liquidación pollo engorde (formato vertical etiqueta|valor del TOTAL
/// de la corrida) a un diccionario clave_canónica → valor. NO calcula nada: solo extrae y mapea por
/// nombre de indicador. La validación/comparación la hace la función de BD.
/// Formato esperado de números: separador de miles "," y decimal "." (ej. 97,165 / 2.77 / 6.26%).
/// </summary>
public static class AuditoriaLiquidacionExcelParser
{
    // Etiqueta normalizada (minúsculas, sin acentos, espacios colapsados) → clave que espera la función.
    private static readonly Dictionary<string, string> LabelMap = new()
    {
        ["aves encasetadas"]                      = "aves_encasetadas",
        ["aves sacrificadas"]                     = "aves_sacrificadas",
        ["mortalidad (unidades)"]                 = "mortalidad",
        ["mortalidad unidades"]                   = "mortalidad",
        ["mortalidad (%)"]                        = "mortalidad_pct",
        ["mortalidad %"]                          = "mortalidad_pct",
        ["merma (unidades)"]                      = "merma_unidades",
        ["merma unidades"]                        = "merma_unidades",
        ["merma (%)"]                             = "merma_pct",
        ["merma %"]                               = "merma_pct",
        ["total de aves despachadas"]             = "total_aves_despachadas",
        ["ajuste en aves"]                        = "ajuste_aves",
        ["porcentaje de ajuste"]                  = "porcentaje_ajuste",
        ["supervivencia"]                         = "supervivencia",
        ["consumo total"]                         = "consumo_total",
        ["produccion kilo en pie"]                = "produccion_kilo_en_pie",
        ["merma (kilos)"]                         = "merma_kilos",
        ["merma kilos"]                           = "merma_kilos",
        ["total kilos despachados a cliente"]     = "total_kilos_despachados_cliente",
        ["consumo ave"]                           = "consumo_ave",
        ["peso promedio"]                         = "peso_promedio",
        ["conversion"]                            = "conversion",
        ["eficiencia americana"]                  = "eficiencia_americana",
        ["dias de engorde"]                       = "dias_engorde",
        ["productividad"]                         = "productividad",
        ["edad ponderada"]                        = "edad_ponderada",
    };

    public static Dictionary<string, decimal?> Parse(Stream excel)
    {
        var result = new Dictionary<string, decimal?>();
        using var wb = new XLWorkbook(excel);
        var ws = wb.Worksheets.First();

        foreach (var row in ws.RowsUsed())
        {
            // Celdas usadas de la fila, ordenadas por columna
            var cells = row.CellsUsed().OrderBy(c => c.Address.ColumnNumber).ToList();
            if (cells.Count < 2) continue;

            for (int i = 0; i < cells.Count; i++)
            {
                var key = MatchLabel(cells[i].GetString());
                if (key is null || result.ContainsKey(key)) continue;

                // El valor es la primera celda numérica a la derecha de la etiqueta.
                for (int j = i + 1; j < cells.Count; j++)
                {
                    var num = CellNumber(cells[j]);
                    if (num.HasValue) { result[key] = num; break; }
                }
                break; // una etiqueta por fila
            }
        }
        return result;
    }

    private static string? MatchLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return LabelMap.TryGetValue(Normalize(raw), out var key) ? key : null;
    }

    private static decimal? CellNumber(IXLCell cell)
    {
        // Celda numérica de verdad: usar el valor directo (sin ambigüedad de cultura).
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<double>(out var d) && !double.IsNaN(d))
            return (decimal)d;

        // Celda de texto (p.ej. "97,165" o "2.77"): quitar separador de miles "," y "%",
        // y parsear con "." como decimal (formato del Excel del cliente).
        var s = cell.GetString()?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        s = s.Replace(",", string.Empty).Replace("%", string.Empty).Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : null;
    }

    private static string Normalize(string s)
    {
        s = s.Trim().ToLowerInvariant();
        // quita acentos
        var formD = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        s = sb.ToString().Normalize(NormalizationForm.FormC);
        return Regex.Replace(s, @"\s+", " ").Trim();
    }
}
