using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Eje de la semana con que el reporte técnico de producción cruza la guía genética.
///
/// <para>Guía compartida ⇒ semana relativa al inicio de producción (lo de siempre). Guía propia
/// (indexada por semana de vida, medido 18..140) ⇒ semana desde el encasetamiento.</para>
/// </summary>
public class SemanaGuiaProduccionCalculosTests
{
    private static readonly DateTime Encaset          = new(2025, 1, 31);
    private static readonly DateTime InicioProduccion = new(2025, 7, 19); // ~24 semanas después (P-K345B real)

    // ── Delta cero: guía compartida se comporta EXACTAMENTE como la fórmula previa ──────────────

    /// <summary>
    /// Fórmula previa, copiada verbatim de <c>ReporteTecnicoProduccionService.Tabs</c> antes del
    /// cambio. Es el contrato que la rama de guía compartida no puede romper.
    /// </summary>
    private static int FormulaPrevia(DateTime fecha, DateTime fechaInicioProd)
    {
        var edadDias = (int)(fecha - fechaInicioProd).TotalDays;
        return (int)Math.Ceiling((edadDias + 1.0) / 7);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(200)]
    [InlineData(700)]
    public void GuiaCompartida_devuelve_lo_mismo_que_la_formula_previa(int diasDesdeInicioProd)
    {
        var fecha = InicioProduccion.AddDays(diasDesdeInicioProd);

        var esperado = FormulaPrevia(fecha, InicioProduccion);
        var actual = SemanaGuiaProduccionCalculos.Resolver(
            guiaEsPropia: false, fecha, InicioProduccion, Encaset);

        Assert.Equal(esperado, actual);
    }

    [Fact]
    public void GuiaCompartida_ignora_el_encaset_aunque_este_cargado()
    {
        var fecha = InicioProduccion.AddDays(3);

        Assert.Equal(
            SemanaGuiaProduccionCalculos.Resolver(false, fecha, InicioProduccion, Encaset),
            SemanaGuiaProduccionCalculos.Resolver(false, fecha, InicioProduccion, fechaEncaset: null));
    }

    // ── Guía propia: la semana es de VIDA ───────────────────────────────────────────────────────

    [Fact]
    public void GuiaPropia_el_dia_del_encaset_es_semana_1()
    {
        Assert.Equal(1, SemanaGuiaProduccionCalculos.Resolver(true, Encaset, InicioProduccion, Encaset));
    }

    [Theory]
    [InlineData(0, 1)]   // día 1 de vida
    [InlineData(6, 1)]   // día 7
    [InlineData(7, 2)]   // día 8
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    public void GuiaPropia_cuenta_semanas_de_vida_base_1(int diasDesdeEncaset, int semanaEsperada)
    {
        var fecha = Encaset.AddDays(diasDesdeEncaset);

        Assert.Equal(semanaEsperada,
            SemanaGuiaProduccionCalculos.Resolver(true, fecha, InicioProduccion, Encaset));
    }

    /// <summary>
    /// El caso que motivó el cambio: el primer día de producción de un lote encasetado 24 semanas
    /// antes cruzaba contra la semana 1 de la guía (que en la guía propia ni existe, arranca en la
    /// 18). Con el eje de vida cruza contra la semana real del ave.
    /// </summary>
    [Fact]
    public void GuiaPropia_el_primer_dia_de_produccion_no_cruza_contra_la_semana_1()
    {
        var relativa = SemanaGuiaProduccionCalculos.Resolver(false, InicioProduccion, InicioProduccion, Encaset);
        var deVida   = SemanaGuiaProduccionCalculos.Resolver(true,  InicioProduccion, InicioProduccion, Encaset);

        Assert.Equal(1, relativa);
        Assert.Equal(25, deVida);
        Assert.NotEqual(relativa, deVida);
    }

    // ── Guardas ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GuiaPropia_sin_encaset_cae_a_la_relativa_sin_lanzar()
    {
        var fecha = InicioProduccion.AddDays(10);

        Assert.Equal(
            FormulaPrevia(fecha, InicioProduccion),
            SemanaGuiaProduccionCalculos.Resolver(true, fecha, InicioProduccion, fechaEncaset: null));
    }

    /// <summary>Una fecha anterior a la base da semana ≤ 0 y no lanza: el reporte simplemente no cruza.</summary>
    [Fact]
    public void Fecha_anterior_a_la_base_no_lanza()
    {
        var fecha = InicioProduccion.AddDays(-10);

        var semana = SemanaGuiaProduccionCalculos.Resolver(false, fecha, InicioProduccion, Encaset);

        Assert.True(semana <= 0);
    }
}
