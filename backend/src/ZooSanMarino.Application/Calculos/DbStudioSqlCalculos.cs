using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ZooSanMarino.Application.DTOs;

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

    // ===================== Backup completo (armado de DDL a partir de introspección) =====================

    /// <summary>
    /// Arma <c>CREATE TABLE [IF NOT EXISTS] schema.tabla (columnas... [, PRIMARY KEY (...)])</c> a partir
    /// de columnas ya introspectadas. Compartido por <c>ExportSchemaAsync</c> (comportamiento preexistente,
    /// <paramref name="includeIdentity"/>=false) y el backup completo (<c>WriteDatabaseBackupAsync</c>,
    /// <paramref name="includeIdentity"/>=true, necesario para que el restore conserve el auto-incremento).
    /// </summary>
    public static string BuildCreateTableSql(
        string schema, string table, List<ColumnDto> cols, bool ifNotExists, bool includeIdentity)
    {
        var sb = new StringBuilder();
        var ine = ifNotExists ? "IF NOT EXISTS " : "";
        sb.AppendLine($"CREATE TABLE {ine}{QuoteQualified(schema, table)} (");
        var defs = new List<string>();
        foreach (var c in cols)
        {
            var def = $"  {QuoteIdent(c.Name)} {c.DataType}";
            if (includeIdentity && c.IsIdentity) def += " GENERATED BY DEFAULT AS IDENTITY";
            if (!c.IsNullable) def += " NOT NULL";
            if (!string.IsNullOrWhiteSpace(c.Default)) def += $" DEFAULT {c.Default}";
            defs.Add(def);
        }
        var pk = cols.Where(c => c.IsPrimaryKey).Select(c => QuoteIdent(c.Name)).ToList();
        if (pk.Count > 0) defs.Add($"  PRIMARY KEY ({string.Join(", ", pk)})");
        sb.AppendLine(string.Join(",\n", defs)).AppendLine(");");
        return sb.ToString();
    }

    private static readonly Regex CreateIndexRx = new(@"^CREATE (UNIQUE )?INDEX ", RegexOptions.Compiled);

    /// <summary>
    /// Inserta <c>IF NOT EXISTS</c> en el <c>CREATE [UNIQUE] INDEX ...</c> devuelto por
    /// <c>pg_get_indexdef</c> para que el restore sea idempotente (reintentable sin error si el índice
    /// ya existe).
    /// </summary>
    public static string MakeCreateIndexIdempotent(string createIndexSql) =>
        CreateIndexRx.IsMatch(createIndexSql)
            ? CreateIndexRx.Replace(createIndexSql, "CREATE $1INDEX IF NOT EXISTS ", 1)
            : createIndexSql;

    /// <summary>
    /// <c>pg_get_serial_sequence</c> cubre tanto columnas IDENTITY como serial clásico; si no hay
    /// secuencia asociada devuelve NULL y <c>setval</c> no se ejecuta contra nada (no-op seguro).
    /// </summary>
    public static string BuildSetvalSql(string schema, string table, string column)
    {
        var qtable = QuoteQualified(schema, table);
        var qcol = QuoteIdent(column);
        return $"SELECT setval(pg_get_serial_sequence('{schema}.{table}', '{column}'), " +
               $"coalesce((SELECT max({qcol}) FROM {qtable}), 1), (SELECT max({qcol}) FROM {qtable}) IS NOT NULL) " +
               $"WHERE pg_get_serial_sequence('{schema}.{table}', '{column}') IS NOT NULL;";
    }

    public static bool IsAutoIncrementColumn(ColumnDto c) =>
        c.IsIdentity || (c.Default?.Contains("nextval(", StringComparison.OrdinalIgnoreCase) ?? false);

    private static readonly Regex NextvalRx = new(@"nextval\('([^']+)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Columnas "serial" clásicas (no IDENTITY) declaran su autoincremento vía <c>DEFAULT nextval('seq')</c>
    /// referenciando una secuencia que es un objeto aparte — a diferencia de IDENTITY, cuya secuencia la
    /// crea automáticamente <c>CREATE TABLE ... GENERATED BY DEFAULT AS IDENTITY</c>. Si no se recrea esa
    /// secuencia ANTES de la tabla, el restore falla con "relation ... _seq does not exist". Devuelve el
    /// nombre de la secuencia (calificado con <paramref name="fallbackSchema"/> si venía sin esquema).
    /// </summary>
    public static bool TryExtractLegacySequenceName(ColumnDto c, string fallbackSchema, out string sequenceSchema, out string sequenceName)
    {
        sequenceSchema = fallbackSchema;
        sequenceName = "";
        if (c.IsIdentity || string.IsNullOrWhiteSpace(c.Default)) return false;
        var m = NextvalRx.Match(c.Default);
        if (!m.Success) return false;

        var raw = m.Groups[1].Value; // "seqname" o "schema.seqname"
        var dot = raw.IndexOf('.');
        if (dot > 0) { sequenceSchema = raw[..dot]; sequenceName = raw[(dot + 1)..]; }
        else sequenceName = raw;
        return !string.IsNullOrWhiteSpace(sequenceName);
    }

    /// <summary>
    /// Recrea una secuencia "serial" clásica (no IDENTITY) antes de las tablas que la referencian en su
    /// DEFAULT. Usa valores por defecto de rango: el valor real se corrige después vía <see cref="BuildSetvalSql"/>.
    /// </summary>
    public static string BuildCreateSequenceSql(string schema, string sequenceName) =>
        $"CREATE SEQUENCE IF NOT EXISTS {QuoteQualified(schema, sequenceName)};";

    /// <summary>Ata la secuencia a su columna para que <c>pg_get_serial_sequence</c> la resuelva al resync.</summary>
    public static string BuildAlterSequenceOwnedBySql(string schema, string sequenceName, string table, string column) =>
        $"ALTER SEQUENCE {QuoteQualified(schema, sequenceName)} OWNED BY {QuoteQualified(schema, table)}.{QuoteIdent(column)};";

    /// <summary>Literal SQL de un valor CLR devuelto por Npgsql (usado por export de tabla y backup completo).</summary>
    public static string SqlLiteral(object? v) => v switch
    {
        null => "NULL",
        bool b => b ? "true" : "false",
        short or int or long or float or double or decimal => Convert.ToString(v, CultureInfo.InvariantCulture)!,
        DateTime dt => $"'{dt:o}'",
        DateTimeOffset dto => $"'{dto:o}'",
        byte[] bytes => bytes.Length == 0 ? "'\\x'" : $"'\\x{Convert.ToHexString(bytes)}'",
        Array arr => BuildArrayLiteral(arr),
        _ => "'" + (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Replace("'", "''") + "'"
    };

    /// <summary>
    /// Literal de array Postgres (ej. text[]/int[]) a partir de un array CLR devuelto por Npgsql. Un
    /// <c>ARRAY[]</c> vacío es ambiguo para Postgres ("cannot determine type of empty array") — necesita
    /// el cast explícito al tipo del array de origen.
    /// </summary>
    private static string BuildArrayLiteral(Array arr)
    {
        if (arr.Length == 0) return $"ARRAY[]::{PgArrayTypeCast(arr.GetType().GetElementType())}";
        var items = new List<string>();
        foreach (var item in arr) items.Add(SqlLiteral(item));
        return $"ARRAY[{string.Join(", ", items)}]";
    }

    private static string PgArrayTypeCast(Type? elementType)
    {
        var underlying = elementType is null ? null : Nullable.GetUnderlyingType(elementType) ?? elementType;
        return underlying switch
        {
            null => "text[]",
            _ when underlying == typeof(int) => "integer[]",
            _ when underlying == typeof(long) => "bigint[]",
            _ when underlying == typeof(short) => "smallint[]",
            _ when underlying == typeof(bool) => "boolean[]",
            _ when underlying == typeof(double) => "double precision[]",
            _ when underlying == typeof(float) => "real[]",
            _ when underlying == typeof(decimal) => "numeric[]",
            _ when underlying == typeof(Guid) => "uuid[]",
            _ when underlying == typeof(DateTime) => "timestamp[]",
            _ => "text[]"
        };
    }
}
