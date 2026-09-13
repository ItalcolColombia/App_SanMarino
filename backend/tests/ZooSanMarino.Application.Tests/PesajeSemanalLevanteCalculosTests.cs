// tests/ZooSanMarino.Application.Tests/PesajeSemanalLevanteCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.PesajeSemanalLevanteCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del bloque «Pesaje» de <c>fn_indicadores_levante_postura</c> (la fn es la dueña del
/// número). Fija dos cosas: con un registro por día la regla nueva es idéntica a la anterior
/// («el último registro de la semana con peso &gt; 0») y con varios registros el mismo día
/// (Santa Reyes) el peso se promedia por día sin perder el sexo que el último registro no pesó.
/// </summary>
public class PesajeSemanalLevanteCalculosTests
{
    private static DateOnly D(int dia) => new(2026, 9, dia);

    private static RegistroSemana R(long id, int dia, double ph = 0, double pm = 0, double uh = 0, double um = 0)
        => new(id, D(dia), ph, pm, uh, um);

    /// <summary>La regla hasta el 12-sep-2026: el último registro (día, id) de la semana con peso &gt; 0.</summary>
    private static Pesaje ReglaAnterior(IReadOnlyList<RegistroSemana> regs)
    {
        var elegido = regs.Where(r => r.PesoH > 0 || r.PesoM > 0).OrderBy(r => r.Dia).ThenBy(r => r.Id).LastOrDefault()
                      ?? regs.OrderBy(r => r.Dia).ThenBy(r => r.Id).Last();
        return new Pesaje(elegido.PesoH, elegido.PesoM, elegido.UnifH, elegido.UnifM);
    }

    [Fact]
    public void UnRegistroPorDia_EsIdenticoALaReglaAnterior()
    {
        var rnd = new Random(20260913);
        for (var caso = 0; caso < 500; caso++)
        {
            var regs = Enumerable.Range(1, rnd.Next(1, 8))
                .Select(dia => R(dia * 10 + rnd.Next(0, 9), dia,
                    ph: rnd.Next(0, 3) == 0 ? 0 : rnd.Next(300, 900),
                    pm: rnd.Next(0, 3) == 0 ? 0 : rnd.Next(400, 1200),
                    uh: rnd.Next(0, 2) == 0 ? 0 : rnd.Next(60, 95),
                    um: rnd.Next(0, 2) == 0 ? 0 : rnd.Next(60, 95)))
                .ToList();

            Assert.Equal(ReglaAnterior(regs), PesajeDeLaSemana(regs));
        }
    }

    [Fact]
    public void DosPesajesElMismoDia_PromediaElPeso()
    {
        var p = PesajeDeLaSemana(new[] { R(1, 1, ph: 480), R(2, 5, ph: 500), R(3, 5, ph: 520) });

        Assert.Equal(510, p.PesoH);   // día 5: (500 + 520) / 2, no el 520 del último registro
    }

    [Fact]
    public void ElUltimoRegistroSoloPesoHembras_ConservaLosMachosDelMismoDia()
    {
        var p = PesajeDeLaSemana(new[] { R(1, 5, ph: 500, pm: 700), R(2, 5, ph: 510) });

        Assert.Equal(505, p.PesoH);
        Assert.Equal(700, p.PesoM);   // la regla anterior daba 0 y la fn arrastraba el peso de la semana previa
    }

    [Fact]
    public void Uniformidad_EsLaDelUltimoRegistroDelDiaQueLaTrae()
    {
        var p = PesajeDeLaSemana(new[] { R(1, 5, ph: 500, uh: 82), R(2, 5, ph: 510), R(3, 4, ph: 490, uh: 90) });

        Assert.Equal(82, p.UnifH);    // día 5 es el último con pesaje; el registro 2 no midió uniformidad
    }

    [Fact]
    public void RegistrosSinPesajeDelUltimoDia_NoDesplazanAlDiaDePesaje()
    {
        var p = PesajeDeLaSemana(new[] { R(1, 3, ph: 450, uh: 80), R(2, 6), R(3, 6) });

        Assert.Equal(new Pesaje(450, 0, 80, 0), p);
    }

    [Fact]
    public void SemanaSinPesaje_TomaElUltimoRegistro()
    {
        var p = PesajeDeLaSemana(new[] { R(5, 2, uh: 70), R(9, 2, uh: 75), R(3, 1, uh: 99) });

        Assert.Equal(new Pesaje(0, 0, 75, 0), p);
    }

    [Fact]
    public void SemanaVacia_TodoEnCero()
        => Assert.Equal(new Pesaje(0, 0, 0, 0), PesajeDeLaSemana(Array.Empty<RegistroSemana>()));
}
