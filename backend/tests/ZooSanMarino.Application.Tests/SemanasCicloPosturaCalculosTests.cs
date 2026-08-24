using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Cortes de Santa Reyes (8 alistamiento + 16 levante + 4 levante-en-producción, ambos grupos;
/// 74 semanas de postura para rojas/criollas u 84 para blancas/Azur) — ver §6 de
/// <c>fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md</c>.
/// </summary>
public class SemanasCicloPosturaCalculosTests
{
    // ── Grupo por raza ───────────────────────────────────────────────────────
    [Theory]
    [InlineData("Lohmann LSL", true)]
    [InlineData("LOHMANN LSL", true)]
    [InlineData("Azur", true)]
    [InlineData("Babcock Brown", false)]
    [InlineData("Hy Line Brown", false)]
    [InlineData("Criolla", false)]
    public void EsGrupoBlancaAzur_reconoce_las_5_razas_sembradas_en_F2_1(string raza, bool esperado)
    {
        Assert.Equal(esperado, SemanasCicloPosturaCalculos.EsGrupoBlancaAzur(raza));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Raza inventada")]
    public void EsGrupoBlancaAzur_raza_no_reconocida_no_adivina(string? raza)
    {
        Assert.Null(SemanasCicloPosturaCalculos.EsGrupoBlancaAzur(raza));
    }

    /// <summary>
    /// 🔴 Regresión del defecto corregido el 24-ago-2026: <c>"Lohmann Brown"</c> contiene el token
    /// <c>LOHMANN</c> y se clasificaba BLANCA (fin de ciclo en la 112). El <c>Lotes.xlsx</c> del
    /// cliente la declara <c>ROJA</c> ⇒ le corresponde el cierre en la 102. Afecta al lote 229.
    /// </summary>
    [Theory]
    [InlineData("Lohmann Brown")]
    [InlineData("LOHMANN BROWN")]
    [InlineData("  lohmann brown  ")]
    public void EsGrupoBlancaAzur_lohmann_brown_es_ROJA_no_blanca(string raza)
    {
        Assert.False(SemanasCicloPosturaCalculos.EsGrupoBlancaAzur(raza));
    }

    [Theory]
    [InlineData(102, "Postura")]
    [InlineData(103, "FueraDeCiclo")]
    public void ObtenerEtapa_lohmann_brown_cierra_a_las_102_como_las_demas_rojas(int semanas, string esperada)
    {
        Assert.Equal(esperada, SemanasCicloPosturaCalculos.ObtenerEtapa("Lohmann Brown", semanas));
        Assert.Equal(esperada, SemanasCicloPosturaCalculos.ObtenerEtapa("LOHMANN BROWN", semanas));
    }

    /// <summary>
    /// Las grafías que traen los lotes desde el ERP del cliente (<c>BABCOK</c> sin la segunda C,
    /// <c>HY LINE</c> sin apellido) tienen que clasificar igual que su nombre comercial completo.
    /// </summary>
    [Theory]
    [InlineData("BABCOK BROWN", false)]
    [InlineData("HY LINE", false)]
    [InlineData("LOHMANN LSL", true)]
    public void EsGrupoBlancaAzur_tolera_la_grafia_del_ERP(string raza, bool esperado)
    {
        Assert.Equal(esperado, SemanasCicloPosturaCalculos.EsGrupoBlancaAzur(raza));
    }

    // ── Etapa por semana — cortes compartidos por los dos grupos ────────────
    [Theory]
    [InlineData(1, "Alistamiento")]
    [InlineData(8, "Alistamiento")]
    [InlineData(9, "Levante")]
    [InlineData(24, "Levante")]
    [InlineData(25, "LevanteEnProduccion")]
    [InlineData(28, "LevanteEnProduccion")]
    [InlineData(29, "Postura")]
    public void ObtenerEtapa_cortes_iguales_en_ambos_grupos(int semanas, string esperada)
    {
        Assert.Equal(esperada, SemanasCicloPosturaCalculos.ObtenerEtapa("Babcock Brown", semanas));
        Assert.Equal(esperada, SemanasCicloPosturaCalculos.ObtenerEtapa("Lohmann LSL", semanas));
    }

    // ── Fin de ciclo — depende de la raza ───────────────────────────────────
    [Theory]
    [InlineData(102, "Postura")]
    [InlineData(103, "FueraDeCiclo")]
    public void ObtenerEtapa_roja_criolla_cierra_a_las_102_semanas(int semanas, string esperada)
    {
        Assert.Equal(esperada, SemanasCicloPosturaCalculos.ObtenerEtapa("Babcock Brown", semanas));
        Assert.Equal(esperada, SemanasCicloPosturaCalculos.ObtenerEtapa("Criolla", semanas));
    }

    [Theory]
    [InlineData(112, "Postura")]
    [InlineData(113, "FueraDeCiclo")]
    public void ObtenerEtapa_blanca_azur_cierra_a_las_112_semanas(int semanas, string esperada)
    {
        Assert.Equal(esperada, SemanasCicloPosturaCalculos.ObtenerEtapa("Lohmann LSL", semanas));
        Assert.Equal(esperada, SemanasCicloPosturaCalculos.ObtenerEtapa("Azur", semanas));
    }

    // ── Indeterminado ────────────────────────────────────────────────────────
    [Fact]
    public void ObtenerEtapa_raza_no_reconocida_devuelve_null()
    {
        Assert.Null(SemanasCicloPosturaCalculos.ObtenerEtapa(null, 10));
        Assert.Null(SemanasCicloPosturaCalculos.ObtenerEtapa("Raza inventada", 10));
    }

    [Fact]
    public void ObtenerEtapa_semana_menor_a_1_devuelve_null()
    {
        Assert.Null(SemanasCicloPosturaCalculos.ObtenerEtapa("Babcock Brown", 0));
        Assert.Null(SemanasCicloPosturaCalculos.ObtenerEtapa("Babcock Brown", -1));
    }
}
