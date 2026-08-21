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
}
