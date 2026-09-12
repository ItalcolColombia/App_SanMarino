using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// La grilla de producción despliega los registros de un día con varios, sin tocar la fila agrupada
/// que leen indicadores, gráfica y Excel.
/// </summary>
public class SeguimientoProduccionRegistrosDelDiaCalculosTests
{
    private static SeguimientoItemDto Item(int id, DateTime fecha, int mortH = 0) => new(
        id, 1, fecha, mortH, 0, 0, 0, 0m, 0m, 0m, 0, 0, "", 0m, 0, null, fecha, null,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, null, null, null, null, null, null, null, null, null);

    private static DateTime Utc(int dia, int hora) => new(2026, 9, dia, hora, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DiaConUnSoloRegistro_QuedaIgual()
    {
        var fila = Item(10, Utc(1, 12), 3);
        var res = SeguimientoProduccionRegistrosDelDiaCalculos.Adjuntar([fila], [fila]);

        Assert.Single(res);
        Assert.Same(fila, res[0]);
        Assert.Null(res[0].RegistrosDelDia);
    }

    [Fact]
    public void DiaConDosRegistros_CuelgaAmbosEnOrden_YConservaLaFilaAgrupada()
    {
        var agrupada = Item(10, Utc(1, 12), 5);
        var segundo = Item(11, Utc(1, 12), 2);
        var primero = Item(10, Utc(1, 12), 3);

        var res = SeguimientoProduccionRegistrosDelDiaCalculos.Adjuntar([agrupada], [segundo, primero]);

        Assert.Single(res);
        Assert.Equal(5, res[0].MortalidadH);
        Assert.Equal([10, 11], res[0].RegistrosDelDia!.Select(r => r.Id));
    }

    [Fact]
    public void SoloAdjuntaAlDiaQueCorresponde()
    {
        var dia1 = Item(10, Utc(1, 12));
        var dia2 = Item(20, Utc(2, 12));
        var registros = new[] { Item(20, Utc(2, 12)), Item(21, Utc(2, 18)), Item(10, Utc(1, 12)) };

        var res = SeguimientoProduccionRegistrosDelDiaCalculos.Adjuntar([dia2, dia1], registros);

        Assert.Equal([20, 10], res.Select(r => r.Id));
        Assert.Equal([20, 21], res[0].RegistrosDelDia!.Select(r => r.Id));
        Assert.Null(res[1].RegistrosDelDia);
    }

    /// <summary>La fn agrupa por día de Bogotá: 03:00 UTC del 2 todavía es el 1 en Bogotá.</summary>
    [Fact]
    public void AgrupaPorDiaDeBogota_NoPorDiaUtc()
    {
        Assert.Equal(new DateOnly(2026, 9, 1), SeguimientoProduccionRegistrosDelDiaCalculos.DiaBogota(Utc(2, 3)));
        Assert.Equal(new DateOnly(2026, 9, 2), SeguimientoProduccionRegistrosDelDiaCalculos.DiaBogota(Utc(2, 5)));

        var fila = Item(10, Utc(1, 12));
        var res = SeguimientoProduccionRegistrosDelDiaCalculos.Adjuntar([fila], [Item(10, Utc(1, 12)), Item(11, Utc(2, 3))]);
        Assert.Equal(2, res[0].RegistrosDelDia!.Count);
    }

    [Fact]
    public void SinRegistros_NoCambiaNada()
    {
        var fila = Item(10, Utc(1, 12));
        var res = SeguimientoProduccionRegistrosDelDiaCalculos.Adjuntar([fila], []);
        Assert.Same(fila, res[0]);
    }
}
