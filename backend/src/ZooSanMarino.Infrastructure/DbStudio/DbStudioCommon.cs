using System.Text.RegularExpressions;

namespace ZooSanMarino.Infrastructure.DbStudio;

internal static class DbStudioCommon
{
    // Solo nombres válidos de identificadores simples (sin comillas)
    static readonly Regex Ident = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static void EnsureValidIdent(string ident, string kind)
    {
        if (string.IsNullOrWhiteSpace(ident) || !Ident.IsMatch(ident))
            throw new InvalidOperationException($"{kind} inválido: '{ident}'");
    }

    public static string QI(string ident)
    {
        // Quote Ident simple (doble comilla)
        return $"\"{ident.Replace("\"", "\"\"")}\"";
    }

    public static string QTable(string schema, string table)
    {
        EnsureValidIdent(schema, "Schema");
        EnsureValidIdent(table, "Table");
        return $"{QI(schema)}.{QI(table)}";
    }
}
