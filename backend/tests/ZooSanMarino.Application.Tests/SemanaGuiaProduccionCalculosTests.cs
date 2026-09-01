using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Eje de la semana con que los reportes de producción cruzan la guía genética.
///
/// <para>La guía —las DOS tablas, la de cada empresa— se indexa por <b>semana de vida</b>: medido,
/// <c>guia_genetica_sanmarino_colombia</c> va de la edad 1 a la 71-97 y su primera edad con
/// producción es la 25/26 (cuando el ave empieza a poner), mientras
/// <c>guia_genetica_santa_reyes</c> va de la 18 a la 140. Los reportes numeraban desde el inicio de
/// producción y comparaban la semana 1 de postura contra la fila de la primera semana de VIDA.</para>
/// </summary>
public class SemanaGuiaProduccionCalculosTests
{
    // Lote real P-K345B de Sanmarino: 169 días entre encaset e inicio de producción ⇒ semana 25.
    private static readonly DateTime Encaset          = new(2025, 1, 31);
    private static readonly DateTime InicioProduccion = new(2025, 7, 19);

    /// <summary>
    /// Fórmula previa, copiada verbatim de los reportes. Se conserva como contrato de la
    /// aritmética: el fix cambia la FECHA BASE, no el redondeo ni el conteo.
    /// </summary>
    private static int FormulaPrevia(DateTime fecha, DateTime fechaBase)
    {
        var edadDias = (int)(fecha - fechaBase).TotalDays;
        return (int)Math.Ceiling((edadDias + 1.0) / 7);
    }

    // ── La aritmética no cambió: sólo la fecha base ─────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(200)]
    [InlineData(700)]
    public void SemanaDesde_conserva_la_aritmetica_previa(int dias)
    {
        var fecha = Encaset.AddDays(dias);

        Assert.Equal(FormulaPrevia(fecha, Encaset), SemanaGuiaProduccionCalculos.SemanaDesde(fecha, Encaset));
    }

    [Theory]
    [InlineData(0, 1)]   // día 1 de vida
    [InlineData(6, 1)]   // día 7
    [InlineData(7, 2)]   // día 8
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    public void SemanaDesde_cuenta_base_1(int dias, int semanaEsperada)
    {
        Assert.Equal(semanaEsperada, SemanaGuiaProduccionCalculos.SemanaDesde(Encaset.AddDays(dias), Encaset));
    }

    // ── El fix: se cruza por semana de VIDA ─────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 El defecto que motivó el cambio, con el lote real: el primer día de postura cruzaba
    /// contra la edad 1 de la guía (pollita de un día, sin producción y con la uniformidad de
    /// levante) en vez de contra la 25, que es la semana de vida que realmente tenía el ave.
    /// </summary>
    [Fact]
    public void El_primer_dia_de_produccion_cruza_contra_la_semana_de_VIDA_no_contra_la_1()
    {
        var semana = SemanaGuiaProduccionCalculos.Resolver(InicioProduccion, InicioProduccion, Encaset);

        Assert.Equal(25, semana);
        Assert.NotEqual(1, semana);
    }

    [Fact]
    public void El_dia_del_encaset_es_semana_1_de_vida()
    {
        Assert.Equal(1, SemanaGuiaProduccionCalculos.Resolver(Encaset, InicioProduccion, Encaset));
    }

    /// <summary>
    /// A las 26 semanas de vida —la primera edad con producción en la guía de Sanmarino— el cruce
    /// tiene que dar exactamente 26.
    /// </summary>
    [Fact]
    public void La_semana_26_de_vida_cruza_contra_la_edad_26_de_la_guia()
    {
        var fecha = Encaset.AddDays(25 * 7); // arranque de la semana 26

        Assert.Equal(26, SemanaGuiaProduccionCalculos.Resolver(fecha, InicioProduccion, Encaset));
    }

    /// <summary>El eje de vida avanza semana a semana junto con la postura.</summary>
    [Theory]
    [InlineData(0, 25)]
    [InlineData(7, 26)]
    [InlineData(14, 27)]
    [InlineData(70, 35)]
    public void Las_semanas_de_postura_avanzan_sobre_el_eje_de_vida(int diasDesdeInicioProd, int semanaVidaEsperada)
    {
        var fecha = InicioProduccion.AddDays(diasDesdeInicioProd);

        Assert.Equal(semanaVidaEsperada,
            SemanaGuiaProduccionCalculos.Resolver(fecha, InicioProduccion, Encaset));
    }

    // ── Guardas ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sin encaset no se puede saber la edad del ave: se cae a la semana relativa a producción
    /// —el comportamiento previo— en vez de lanzar. Hoy no hay lotes así, pero el reporte no es el
    /// lugar para enterarse.
    /// </summary>
    [Fact]
    public void Sin_encaset_cae_a_la_semana_relativa_a_produccion_sin_lanzar()
    {
        var fecha = InicioProduccion.AddDays(10);

        Assert.Equal(
            FormulaPrevia(fecha, InicioProduccion),
            SemanaGuiaProduccionCalculos.Resolver(fecha, InicioProduccion, fechaEncaset: null));
    }

    /// <summary>Una fecha anterior a la base da semana ≤ 0 y no lanza: el reporte no cruza y ya.</summary>
    [Fact]
    public void Fecha_anterior_al_encaset_no_lanza()
    {
        var semana = SemanaGuiaProduccionCalculos.Resolver(Encaset.AddDays(-10), InicioProduccion, Encaset);

        Assert.True(semana <= 0);
    }
}
