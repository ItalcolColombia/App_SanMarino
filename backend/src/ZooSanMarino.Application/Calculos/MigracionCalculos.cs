// src/ZooSanMarino.Application/Calculos/MigracionCalculos.cs
using System.Globalization;
using System.Text;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Coerción y normalización PURA (sin EF ni estado) usada por el módulo de Migraciones para
/// interpretar celdas de Excel. Testeable en aislamiento (xUnit).
/// </summary>
public static class MigracionCalculos
{
    /// <summary>Clave de comparación: minúsculas, sin acentos, sin dobles espacios, recortada.</summary>
    public static string NormalizarClave(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var formD = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch);
        }
        var sinAcentos = sb.ToString().Normalize(NormalizationForm.FormC);
        // colapsar espacios internos
        return string.Join(' ', sinAcentos.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Texto recortado o null si la celda está vacía.</summary>
    public static string? TextoLimpio(object? cell)
    {
        if (cell is null) return null;
        var s = cell is string str ? str : Convert.ToString(cell, CultureInfo.InvariantCulture);
        s = s?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    public static bool EsVacia(object? cell) => TextoLimpio(cell) is null;

    /// <summary>Entero desde celda numérica o string ("12", "1.234", "1,234"). Un decimal real falla.</summary>
    public static bool TryEntero(object? cell, out int val)
    {
        val = 0;
        switch (cell)
        {
            case null: return false;
            case int i: val = i; return true;
            case long l: if (l < int.MinValue || l > int.MaxValue) return false; val = (int)l; return true;
            case double d:
                if (Math.Abs(d - Math.Round(d)) > 1e-9 || d < int.MinValue || d > int.MaxValue) return false;
                val = (int)Math.Round(d); return true;
            case float f:
                if (Math.Abs(f - Math.Round(f)) > 1e-6 || f < int.MinValue || f > int.MaxValue) return false;
                val = (int)Math.Round(f); return true;
            case decimal m:
                if (m != Math.Round(m) || m < int.MinValue || m > int.MaxValue) return false;
                val = (int)m; return true;
        }
        var s = TextoLimpio(cell);
        if (s is null) return false;
        s = s.Replace(".", string.Empty).Replace(",", string.Empty); // separadores de miles
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out val);
    }

    /// <summary>Decimal desde celda numérica o string; el separador decimal es el que aparece más a la derecha.</summary>
    public static bool TryDecimal(object? cell, out decimal val)
    {
        val = 0m;
        switch (cell)
        {
            case null: return false;
            case double d: val = (decimal)d; return true;
            case decimal m: val = m; return true;
            case float f: val = (decimal)f; return true;
            case int i: val = i; return true;
            case long l: val = l; return true;
        }
        var s = TextoLimpio(cell);
        if (s is null) return false;

        int lastComma = s.LastIndexOf(',');
        int lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // ambos separadores: el de más a la derecha es el decimal
            if (lastComma > lastDot) s = s.Replace(".", string.Empty).Replace(',', '.'); // EU: 1.234,56
            else s = s.Replace(",", string.Empty);                                         // US: 1,234.56
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.'); // solo coma → separador decimal (convención es-XX)
        }
        // solo punto (o ninguno) → ya es punto decimal
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out val);
    }

    /// <summary>Fecha desde DateTime, serial Excel (double) o string (yyyy-MM-dd, dd/MM/yyyy, etc.).</summary>
    public static bool TryFecha(object? cell, out DateTime val)
    {
        val = default;
        switch (cell)
        {
            case null: return false;
            case DateTime dt: val = dt.Date; return true;
            case double d:
                try { val = DateTime.FromOADate(d).Date; return true; } catch { return false; }
            case int i:
                try { val = DateTime.FromOADate(i).Date; return true; } catch { return false; }
        }
        var s = TextoLimpio(cell);
        if (s is null) return false;
        string[] formatos = { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "yyyy/MM/dd", "dd-MM-yyyy" };
        if (DateTime.TryParseExact(s, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out val))
        { val = val.Date; return true; }
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out val))
        { val = val.Date; return true; }
        return false;
    }

    /// <summary>
    /// Hora del día desde <see cref="DateTime"/>/<see cref="TimeSpan"/>, serial de Excel (la parte
    /// fraccionaria del día: 0.5 = 12:00) o texto ("HH:mm", "H:mm:ss", "hh:mm tt"). Los segundos
    /// del serial se redondean al minuto para no arrastrar el ruido del punto flotante de Excel.
    /// </summary>
    public static bool TryHora(object? cell, out TimeOnly val)
    {
        val = default;
        switch (cell)
        {
            case null: return false;
            case DateTime dt: val = TimeOnly.FromDateTime(dt); return true;
            case TimeSpan ts:
                if (ts < TimeSpan.Zero || ts >= TimeSpan.FromDays(1)) return false;
                val = TimeOnly.FromTimeSpan(ts); return true;
            case double d: return TryHoraDesdeSerial(d, out val);
            case int i: return TryHoraDesdeSerial(i, out val);
        }

        var s = TextoLimpio(cell);
        if (s is null) return false;
        // Un serial de Excel puede llegar como texto ("0.5") si la celda venía sin formato de hora.
        if (double.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var serial)
            && s.IndexOf(':') < 0)
            return TryHoraDesdeSerial(serial, out val);

        string[] formatos = { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss", "hh:mm tt", "h:mm tt", "hh:mm:ss tt", "h:mm:ss tt" };
        if (TimeOnly.TryParseExact(s, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out val))
            return true;
        return TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out val);
    }

    /// <summary>Fracción de día de Excel → hora. Acepta seriales con parte entera (fecha+hora).</summary>
    private static bool TryHoraDesdeSerial(double serial, out TimeOnly val)
    {
        val = default;
        if (double.IsNaN(serial) || double.IsInfinity(serial) || serial < 0) return false;
        var fraccion = serial - Math.Floor(serial);
        var minutos = (int)Math.Round(fraccion * 24 * 60, MidpointRounding.AwayFromZero);
        if (minutos >= 24 * 60) minutos = 0; // 23:59:59.7 → 24:00 ⇒ medianoche
        val = new TimeOnly(minutos / 60, minutos % 60);
        return true;
    }

    /// <summary>
    /// Booleano de planilla: "sí/si/s/x/true/verdadero/1" ⇒ true · "no/n/false/falso/0" ⇒ false.
    /// Devuelve false (no reconocido) para cualquier otro texto, para que el llamador reporte el error.
    /// </summary>
    public static bool TryBooleanoSiNo(object? cell, out bool val)
    {
        val = false;
        if (cell is bool b) { val = b; return true; }
        var k = NormalizarClave(TextoLimpio(cell));
        if (k.Length == 0) return false;
        switch (k)
        {
            case "si" or "s" or "x" or "true" or "verdadero" or "1": val = true; return true;
            case "no" or "n" or "false" or "falso" or "0": val = false; return true;
            default: return false;
        }
    }

    /// <summary>Estado de granja: 'A' o 'I' (default 'A').</summary>
    public static string NormalizarEstado(string? s)
    {
        var k = NormalizarClave(s);
        if (k is "i" or "inactivo" or "inactiva" or "0") return "I";
        return "A";
    }

    /// <summary>Factor oficial 1 quintal = 100 lb = 45.36 kg (mismo que PuentePanamaCalculos.KgPorQuintal y el QQ_TO_KG del front).</summary>
    public const decimal KgPorQuintal = 45.36m;

    /// <summary>
    /// Unidad de consumo de la carga masiva: "kg" (default si la celda viene vacía) o "qq".
    /// Devuelve null si el texto no corresponde a ninguna de las dos (unidad inválida → error de fila).
    /// </summary>
    public static string? NormalizarUnidadConsumo(string? s)
    {
        var k = NormalizarClave(s);
        if (string.IsNullOrEmpty(k) || k is "kg" or "kilo" or "kilos" or "kilogramo" or "kilogramos") return "kg";
        if (k is "qq" or "quintal" or "quintales") return "qq";
        return null;
    }

    /// <summary>
    /// Convierte una cantidad de consumo a kg según la unidad normalizada: "qq" multiplica por
    /// <see cref="KgPorQuintal"/> con redondeo a 3 decimales (mismo redondeo que el toKg del front);
    /// "kg" pasa intacta. Null se conserva.
    /// </summary>
    public static decimal? ConsumoAKilos(decimal? cantidad, string unidad) =>
        cantidad.HasValue && unidad == "qq" ? Math.Round(cantidad.Value * KgPorQuintal, 3) : cantidad;
}
