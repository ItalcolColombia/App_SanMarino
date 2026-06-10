using System.Text.RegularExpressions;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA de DB Studio (sin EF/Npgsql/estado): validación y quoting de identificadores,
/// clasificación de sentencias SQL y armado de fragmentos DDL. Centraliza las reglas para que
/// se puedan testear con xUnit y reusar desde Infrastructure.
/// </summary>
public static class DbStudioSqlCalculos
{
    public enum SqlKind { Empty, Select, Dml, Ddl, Utility, Dangerous, Multiple }

    public sealed record SqlClassification(
        SqlKind Kind,
        bool IsReadOnly,
        bool RequiresConfirmation,
        string? Reason);

    private static readonly Regex Ident =
        new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly Regex LeadingSelect =
        new(@"^\s*(select|with|table|values|show|explain)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Palabras prohibidas dentro del path de SELECT seguro.
    private static readonly string[] ForbiddenInSelect =
    {
        "insert", "update", "delete", "drop", "alter", "create", "grant", "revoke",
        "truncate", "execute", "call", "copy", "comment", "vacuum", "reindex", "cluster"
    };

    private static readonly string[] DmlVerbs = { "insert", "update", "delete", "merge" };
    private static readonly string[] DdlVerbs = { "create", "alter", "drop", "truncate", "comment", "rename" };

    // Operaciones que un admin puede ejecutar pero que exigen confirmación explícita en UI.
    private static readonly Regex DangerousRx = new(
        @"\b(drop\s+(database|schema|role|user)|alter\s+system|truncate\b|drop\s+table|drop\s+view|drop\s+function|delete\s+from\s+\w+\s*(;|$)|update\s+\w+\s+set\b(?!.*\bwhere\b))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Bloqueado siempre (incluso para admin): acceso a SO / extracción de archivos / reset masivo.
    private static readonly Regex HardBlockedRx = new(
        @"\b(copy\s+.*\bprogram\b|pg_read_file|pg_read_binary_file|lo_import|lo_export|pg_terminate_backend|pg_cancel_backend)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ===================== Identificadores =====================

    public static bool IsValidIdentifier(string? ident) =>
        !string.IsNullOrWhiteSpace(ident) && Ident.IsMatch(ident);

    public static void EnsureValidIdentifier(string? ident, string kind)
    {
        if (!IsValidIdentifier(ident))
            throw new InvalidOperationException($"{kind} inválido: '{ident}'");
    }

    /// <summary>Quote de identificador simple (doble comilla, escapando comillas internas).</summary>
    public static string QuoteIdent(string ident) => $"\"{ident.Replace("\"", "\"\"")}\"";

    /// <summary>Quote de un nombre calificado schema.objeto (valida ambos).</summary>
    public static string QuoteQualified(string schema, string name)
    {
        EnsureValidIdentifier(schema, "Schema");
        EnsureValidIdentifier(name, "Objeto");
        return $"{QuoteIdent(schema)}.{QuoteIdent(name)}";
    }

    // ===================== Clasificación =====================

    /// <summary>True si la consulta es un SELECT/WITH puro y no contiene verbos de escritura.</summary>
    public static bool IsPureSelect(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        if (ContainsMultipleStatements(sql)) return false;
        if (!LeadingSelect.IsMatch(sql)) return false;
        var lower = sql.ToLowerInvariant();
        foreach (var f in ForbiddenInSelect)
            if (Regex.IsMatch(lower, $@"\b{f}\b"))
                return false;
        return true;
    }

    /// <summary>True si hay más de una sentencia (; que no sea el final).</summary>
    public static bool ContainsMultipleStatements(string sql)
    {
        var trimmed = (sql ?? string.Empty).Trim().TrimEnd(';');
        return trimmed.Contains(';');
    }

    /// <summary>Clasifica una sentencia arbitraria (para el modo admin de la consola).</summary>
    public static SqlClassification Classify(string? sql)
    {
        var s = (sql ?? string.Empty).Trim();
        if (s.Length == 0)
            return new SqlClassification(SqlKind.Empty, true, false, "Sentencia vacía.");

        if (HardBlockedRx.IsMatch(s))
            return new SqlClassification(SqlKind.Dangerous, false, true,
                "Operación bloqueada (acceso a sistema operativo / control de sesiones).");

        if (ContainsMultipleStatements(s))
            return new SqlClassification(SqlKind.Multiple, false, true,
                "Solo se permite una sentencia por ejecución.");

        var firstWord = FirstWord(s);

        if (LeadingSelect.IsMatch(s) && !DmlVerbs.Contains(firstWord) && !DdlVerbs.Contains(firstWord))
            return new SqlClassification(SqlKind.Select, true, false, null);

        var dangerous = DangerousRx.IsMatch(s);

        if (DdlVerbs.Contains(firstWord))
            return new SqlClassification(SqlKind.Ddl, false, dangerous,
                dangerous ? "Operación DDL destructiva: requiere confirmación." : null);

        if (DmlVerbs.Contains(firstWord))
            return new SqlClassification(SqlKind.Dml, false, dangerous,
                dangerous ? "DML sin WHERE o de borrado masivo: requiere confirmación." : null);

        return new SqlClassification(SqlKind.Utility, false, dangerous,
            dangerous ? "Operación administrativa: requiere confirmación." : null);
    }

    private static string FirstWord(string s)
    {
        var m = Regex.Match(s, @"^\s*([A-Za-z_]+)", RegexOptions.Compiled);
        return m.Success ? m.Groups[1].Value.ToLowerInvariant() : string.Empty;
    }

    // ===================== Armado de DDL =====================

    /// <summary>Renderiza el tipo con longitud/precisión: ej. varchar(50), numeric(10,2).</summary>
    public static string RenderColumnType(string type, int? maxLength, int? precision, int? scale)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new InvalidOperationException("Tipo de columna requerido.");

        var t = type.Trim();
        // Si el tipo ya viene parametrizado (contiene paréntesis), respetarlo.
        if (t.Contains('(')) return t;

        if (maxLength.HasValue && maxLength.Value > 0)
            return $"{t}({maxLength.Value})";
        if (precision.HasValue && precision.Value > 0)
            return scale.HasValue ? $"{t}({precision.Value},{scale.Value})" : $"{t}({precision.Value})";
        return t;
    }

    /// <summary>
    /// Arma la definición de una columna para CREATE/ADD COLUMN. <paramref name="defaultExpr"/> e
    /// <paramref name="identity"/> se interpolan tal cual (no parametrizables en DDL); el llamador
    /// es responsable de validarlos. El nombre se valida y quotea.
    /// </summary>
    public static string BuildColumnDefinition(
        string name, string type, bool nullable, string? defaultExpr,
        string? identity, int? maxLength, int? precision, int? scale)
    {
        EnsureValidIdentifier(name, "Columna");
        var def = $"{QuoteIdent(name)} {RenderColumnType(type, maxLength, precision, scale)}";

        switch (identity?.Trim().ToLowerInvariant())
        {
            case "always": def += " GENERATED ALWAYS AS IDENTITY"; break;
            case "by_default": def += " GENERATED BY DEFAULT AS IDENTITY"; break;
            case null or "": break;
            default: throw new InvalidOperationException("Identity debe ser 'always', 'by_default' o nulo.");
        }

        if (!nullable) def += " NOT NULL";
        if (!string.IsNullOrWhiteSpace(defaultExpr)) def += $" DEFAULT {defaultExpr}";
        return def;
    }
}
