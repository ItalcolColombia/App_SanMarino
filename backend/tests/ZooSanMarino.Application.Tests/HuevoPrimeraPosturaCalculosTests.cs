using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Vigencia del ítem «Huevo de primera postura» — ver §6 (F7) de
/// <c>fase_de_desarrollo/santa_reyes_requerimientos_italapp_plan.md</c>. Santa Reyes hoy configura
/// <c>HuevoPrimeraPosturaHastaSemana = 22</c>.
/// </summary>
public class HuevoPrimeraPosturaCalculosTests
{
    [Theory]
    [InlineData(22, 1, true)]
    [InlineData(22, 21, true)]
    [InlineData(22, 22, true)]
    [InlineData(22, 23, false)]
    [InlineData(22, 40, false)]
    public void EsVigente_respeta_el_limite_configurado(int hastaSemana, int semanaVida, bool esperado)
    {
        Assert.Equal(esperado, HuevoPrimeraPosturaCalculos.EsVigente(hastaSemana, semanaVida));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(22)]
    [InlineData(200)]
    public void EsVigente_sin_limite_configurado_siempre_vigente(int semanaVida)
    {
        // Toda empresa salvo Santa Reyes: HuevoPrimeraPosturaHastaSemana es null ⇒ no se oculta nada.
        Assert.True(HuevoPrimeraPosturaCalculos.EsVigente(null, semanaVida));
    }

    [Fact]
    public void EsVigente_sin_semana_de_vida_calculable_no_bloquea()
    {
        // Sin fecha de encaset todavía no hay nada que evaluar — fail-open (es un gate de UI, no de guardado).
        Assert.True(HuevoPrimeraPosturaCalculos.EsVigente(22, null));
    }

    // ── D5 · MensajeFueraDeVigencia — la vigencia deja de ser solo UI ───────────────────

    [Fact]
    public void MensajeFueraDeVigencia_DentroDeVigencia_NoRechaza()
    {
        Assert.Null(HuevoPrimeraPosturaCalculos.MensajeFueraDeVigencia(22, 22, "HUEVO SIN CLAS ROJO PRIMERAS POSTURAS"));
    }

    [Fact]
    public void MensajeFueraDeVigencia_FueraDeVigencia_RechazaNombrandoItemYSemanas()
    {
        // El operario tiene que poder entender el rechazo sin ir a preguntar: qué ítem, hasta
        // cuándo valía, y en qué semana está el registro.
        var msg = HuevoPrimeraPosturaCalculos.MensajeFueraDeVigencia(22, 23, "HUEVO SIN CLAS ROJO PRIMERAS POSTURAS");

        Assert.NotNull(msg);
        Assert.Contains("HUEVO SIN CLAS ROJO PRIMERAS POSTURAS", msg);
        Assert.Contains("22", msg);
        Assert.Contains("23", msg);
    }

    [Theory]
    [InlineData(null, 30)]   // empresa sin límite configurado (todas salvo Santa Reyes)
    [InlineData(22, null)]   // lote sin fecha de encaset: no hay semana calculable
    public void MensajeFueraDeVigencia_SinReglaQueAplicar_EsFailOpen(int? hastaSemana, int? semanaVida)
    {
        Assert.Null(HuevoPrimeraPosturaCalculos.MensajeFueraDeVigencia(hastaSemana, semanaVida, "ITEM"));
    }

    [Fact]
    public void MensajeFueraDeVigencia_EsCoherenteConEsVigente_EnTodoElRango()
    {
        // Una sola regla, dos formas de preguntarla: si divergen, el selector y el guardado se
        // contradicen y el operario ve una opción habilitada que después el backend rechaza.
        for (var semana = 1; semana <= 40; semana++)
        {
            var vigente = HuevoPrimeraPosturaCalculos.EsVigente(22, semana);
            var msg = HuevoPrimeraPosturaCalculos.MensajeFueraDeVigencia(22, semana, "ITEM");
            Assert.Equal(vigente, msg is null);
        }
    }
}
