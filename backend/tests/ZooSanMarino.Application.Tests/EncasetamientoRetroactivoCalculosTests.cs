using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.EncasetamientoRetroactivoCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Diagnóstico de informar la hora de encasetamiento en un lote que YA tiene seguimientos: el caso de
/// producción, donde todos los lotes se crearon sin hora. Solo una hora tardía puede dejar registros
/// fuera, y únicamente los que caigan en el día del encasetamiento.
/// </summary>
public class EncasetamientoRetroactivoCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Semana de recogida tal como está hoy en producción: edades 0..6 (08/06 a 14/06).</summary>
    private static DateTime[] SemanaDesdeElEncaset() =>
        Enumerable.Range(0, 7).Select(d => Encaset.AddDays(d)).ToArray();

    /// <summary>Semana como quedaría en un lote tardío: edades 1..7 (09/06 a 15/06).</summary>
    private static DateTime[] SemanaDesdeElDiaSiguiente() =>
        Enumerable.Range(1, 7).Select(d => Encaset.AddDays(d)).ToArray();

    [Fact]
    public void SinHora_SiempreCompatible_AunqueHayaRegistroEnElDiaDelEncaset()
    {
        var diag = Diagnosticar(Encaset, null, SemanaDesdeElEncaset());

        Assert.True(diag.Compatible);
        Assert.Equal(0, diag.RegistrosFuera);
    }

    [Fact]
    public void HoraTemprana_NoMueveNada_ElLoteQuedaIgual()
    {
        var diag = Diagnosticar(Encaset, new TimeOnly(9, 0), SemanaDesdeElEncaset());

        Assert.True(diag.Compatible);
        Assert.Equal(Encaset.Date, diag.PrimerDia.Date);
    }

    [Fact]
    public void HoraTardia_ConRegistroEnElDiaDelEncaset_EsIncompatible()
    {
        var diag = Diagnosticar(Encaset, new TimeOnly(15, 0), SemanaDesdeElEncaset());

        Assert.False(diag.Compatible);
        Assert.Equal(1, diag.RegistrosFuera); // solo el del día del encaset
        Assert.Equal(new DateTime(2026, 6, 9).Date, diag.PrimerDia.Date);
        Assert.Equal(new DateTime(2026, 6, 8).Date, diag.PrimeraFechaFuera!.Value.Date);
    }

    [Fact]
    public void HoraTardia_ConLaSemanaYaCorrida_EsCompatible()
    {
        // El caso de los 101 lotes de producción que ya arrancan después del encaset.
        var diag = Diagnosticar(Encaset, new TimeOnly(15, 0), SemanaDesdeElDiaSiguiente());

        Assert.True(diag.Compatible);
        Assert.Equal(0, diag.RegistrosFuera);
    }

    [Fact]
    public void HoraTardia_LoteSinRegistros_EsCompatible()
    {
        var diag = Diagnosticar(Encaset, new TimeOnly(18, 0), Array.Empty<DateTime>());

        Assert.True(diag.Compatible);
    }

    [Fact]
    public void SinFechaDeEncaset_NoSePuedeDiagnosticar_YNoBloquea()
    {
        var diag = Diagnosticar(null, new TimeOnly(18, 0), SemanaDesdeElEncaset());

        Assert.True(diag.Compatible);
    }

    [Fact]
    public void CorteInclusive_LasTrece_YaEsTardia()
    {
        var alas1259 = Diagnosticar(Encaset, new TimeOnly(12, 59), SemanaDesdeElEncaset());
        var alas1300 = Diagnosticar(Encaset, new TimeOnly(13, 0), SemanaDesdeElEncaset());

        Assert.True(alas1259.Compatible);
        Assert.False(alas1300.Compatible);
    }

    [Fact]
    public void VariosRegistrosFuera_LosCuentaTodos_YReportaElMasAntiguo()
    {
        // Un lote cuyos registros arrancan ANTES del encaset (dato sucio): todos quedan fuera.
        var fechas = new[] { Encaset.AddDays(-2), Encaset.AddDays(-1), Encaset, Encaset.AddDays(1) };

        var diag = Diagnosticar(Encaset, new TimeOnly(14, 0), fechas);

        Assert.False(diag.Compatible);
        Assert.Equal(3, diag.RegistrosFuera);
        Assert.Equal(Encaset.AddDays(-2).Date, diag.PrimeraFechaFuera!.Value.Date);
    }

    [Fact]
    public void MensajeIncompatible_DiceQueHacer()
    {
        var diag = Diagnosticar(Encaset, new TimeOnly(15, 0), SemanaDesdeElEncaset());

        var msg = MensajeIncompatible(diag, new TimeOnly(15, 0));

        Assert.Contains("15:00", msg);
        Assert.Contains("2026-06-09", msg);  // el primer día que impondría la hora
        Assert.Contains("2026-06-08", msg);  // el registro que estorba
        Assert.Contains("1 registro", msg);
    }
}
