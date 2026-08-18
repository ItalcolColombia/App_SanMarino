using Xunit;
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.CuadreAvesEngordeCalculos;

namespace ZooSanMarino.Application.Tests;

public class CuadreAvesEngordeCalculosTests
{
    [Fact]
    public void Lote132_el_caso_real_corrige_hacia_el_Inicio()
    {
        // Inicio 8.414 H + 10.773 M = 19.187 · encaset 19.387 · desfase 200 H / 0 M
        var r = Resolver(new EstadoLote(TieneInicio: true, InicioTotal: 19_187,
                                        AvesEncasetadas: 19_387, DesfaseH: 200, DesfaseM: 0));

        Assert.NotNull(r);
        Assert.Equal(19_187, r!.Value.AvesEncasetadas);
        Assert.Equal(200, r.Value.RestaH);
        Assert.Equal(0, r.Value.RestaM);
    }

    [Fact]
    public void Un_lote_que_ya_cuadra_no_se_toca()
    {
        Assert.Null(Resolver(new EstadoLote(true, 19_187, 19_187, 0, 0)));
    }

    [Fact]
    public void Idempotencia_el_resultado_de_corregir_ya_no_entra()
    {
        var r = Resolver(new EstadoLote(true, 19_187, 19_387, 200, 0))!.Value;
        // Tras aplicar: encaset = 19.187 y el maestro bajó 200 ⇒ desfase 0.
        Assert.Null(Resolver(new EstadoLote(true, 19_187, r.AvesEncasetadas, 0, 0)));
    }

    [Fact]
    public void Si_el_gap_del_encaset_no_es_el_desfase_del_maestro_es_otra_causa_y_no_se_toca()
    {
        // Encaset sobra 200 pero el maestro sobra 500: la regla no lo explica.
        Assert.Null(Resolver(new EstadoLote(true, 19_187, 19_387, 500, 0)));
    }

    [Fact]
    public void Sin_registro_Inicio_no_hay_referencia_y_no_se_toca()
    {
        Assert.Null(Resolver(new EstadoLote(TieneInicio: false, InicioTotal: 0,
                                            AvesEncasetadas: 19_387, DesfaseH: 200, DesfaseM: 0)));
    }

    [Theory]
    [InlineData(-200, 0)]
    [InlineData(0, -50)]
    public void Un_desfase_negativo_es_otra_causa_restarle_al_maestro_lo_empeoraria(int dh, int dm)
    {
        Assert.Null(Resolver(new EstadoLote(true, 19_187, 19_187 + dh + dm, dh, dm)));
    }

    [Fact]
    public void El_desfase_puede_repartirse_entre_los_dos_sexos()
    {
        var r = Resolver(new EstadoLote(true, 40_000, 40_700, 300, 400));

        Assert.NotNull(r);
        Assert.Equal(40_000, r!.Value.AvesEncasetadas);
        Assert.Equal(300, r.Value.RestaH);
        Assert.Equal(400, r.Value.RestaM);
    }
}
