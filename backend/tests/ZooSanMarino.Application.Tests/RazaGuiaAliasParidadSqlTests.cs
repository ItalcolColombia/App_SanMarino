using System.Text.RegularExpressions;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El alias de grafía de raza vive en DOS lados desde el 30-ago-2026:
/// <list type="bullet">
///   <item><c>RazaGuiaAliasCalculos</c> (C#), que usan <c>GuiaGeneticaLookup</c> y los reportes técnicos.</item>
///   <item>La tercera rama de <c>backend/sql/vw_guia_genetica_postura.sql</c>, que usan los
///         indicadores de postura —que calcula Postgres, no C#—.</item>
/// </list>
///
/// <para>
/// <b>Por qué este test.</b> Tener el alias de un solo lado es exactamente el defecto que se
/// corrigió: un lote cargado con <c>BABCOK BROWN</c> mostraba la guía en el reporte técnico y nada
/// en indicadores de producción (y <c>0,00</c> en los de levante). Si alguien agrega un alias en C#
/// y se olvida del SQL —o al revés— el defecto vuelve <b>en silencio</b>: no hay error, sólo una
/// columna vacía. Este test compara las dos listas y corta el build si divergen.
/// </para>
///
/// <para>
/// Lee el <c>.sql</c> del repo a propósito, y no una copia: el archivo es el espejo del objeto que
/// la migración aplica, así que es la definición real, no una transcripción que también puede
/// quedar vieja.
/// </para>
/// </summary>
public class RazaGuiaAliasParidadSqlTests
{
    /// <summary>
    /// El bloque de la rama alias: <c>JOIN (VALUES ('babcock brown', 'BABCOK BROWN'), …)</c>.
    /// Cada par es (raza tal como vive en la guía, grafía con la que la escribe el ERP).
    /// </summary>
    private static readonly Regex ParAlias = new(
        @"\(\s*'(?<guia>[^']+)'\s*,\s*'(?<erp>[^']+)'\s*\)",
        RegexOptions.Compiled);

    /// <summary>
    /// Sube desde el directorio del assembly hasta encontrar la raíz del repo (la que tiene
    /// <c>backend/sql</c>). No se usa una ruta relativa fija porque el binario cambia de carpeta
    /// según configuración y TFM.
    /// </summary>
    private static string RutaEspejoSql()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidato = Path.Combine(dir.FullName, "backend", "sql", "vw_guia_genetica_postura.sql");
            if (File.Exists(candidato)) return candidato;
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "No se encontró backend/sql/vw_guia_genetica_postura.sql subiendo desde " +
            AppContext.BaseDirectory + ". El test necesita el espejo del repo para comparar el alias.");
    }

    /// <summary>Pares (grafía del ERP normalizada ⇒ grafía de la guía) declarados en el SQL.</summary>
    private static Dictionary<string, string> AliasDelSql()
    {
        var sql = File.ReadAllText(RutaEspejoSql());

        // Sólo el bloque del JOIN de la rama alias: el resto del archivo también tiene paréntesis
        // y comillas, y no queremos barrerlos.
        var inicio = sql.IndexOf("JOIN (VALUES", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "La vista ya no tiene la rama ALIAS (JOIN (VALUES …)).");
        var fin = sql.IndexOf("AS a(raza_guia, alias)", inicio, StringComparison.Ordinal);
        Assert.True(fin > inicio, "El JOIN de la rama ALIAS no cierra con AS a(raza_guia, alias).");

        return ParAlias.Matches(sql[inicio..fin])
            .ToDictionary(
                m => RazaGuiaAliasCalculos.Normalizar(m.Groups["erp"].Value),
                m => RazaGuiaAliasCalculos.Normalizar(m.Groups["guia"].Value));
    }

    [Fact]
    public void El_alias_del_SQL_dice_exactamente_lo_mismo_que_el_del_Csharp()
    {
        var enSql = AliasDelSql();
        var enCsharp = RazaGuiaAliasCalculos.AliasConocidos;

        Assert.Equal(
            enCsharp.OrderBy(p => p.Key).Select(p => $"{p.Key} => {p.Value}"),
            enSql.OrderBy(p => p.Key).Select(p => $"{p.Key} => {p.Value}"));
    }

    /// <summary>
    /// La columna IZQUIERDA del <c>VALUES</c> es la clave con la que la vista JOINea contra
    /// <c>guia_genetica_santa_reyes</c> por <c>btrim(lower(raza))</c>: tiene que estar normalizada o
    /// el JOIN no devuelve ninguna fila y la rama alias queda muerta sin que nada falle.
    ///
    /// <para>
    /// La columna DERECHA —la grafía que la vista emite como <c>raza</c>— NO se normaliza a
    /// propósito: se escribe tal como la manda el ERP (<c>BABCOK BROWN</c>, en mayúsculas), que es
    /// lo que hace falta para el único lector que todavía compara la raza exacta.
    /// </para>
    /// </summary>
    [Fact]
    public void La_clave_del_JOIN_esta_normalizada_para_que_el_join_encuentre_la_fila()
    {
        var sql = File.ReadAllText(RutaEspejoSql());
        var inicio = sql.IndexOf("JOIN (VALUES", StringComparison.Ordinal);
        var fin = sql.IndexOf("AS a(raza_guia, alias)", inicio, StringComparison.Ordinal);

        foreach (Match m in ParAlias.Matches(sql[inicio..fin]))
        {
            var claveDelJoin = m.Groups["guia"].Value;
            Assert.Equal(RazaGuiaAliasCalculos.Normalizar(claveDelJoin), claveDelJoin);
        }
    }
}
