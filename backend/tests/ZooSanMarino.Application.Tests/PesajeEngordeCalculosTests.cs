using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Día de pesaje obligatorio en pollo engorde: diario la primera semana y después al CIERRE de cada
/// semana. Con la regla de la hora de llegada apagada el set de días tiene que quedar idéntico al
/// histórico (se evaluaba sobre la edad cruda).
/// </summary>
public class PesajeEngordeCalculosTests
{
    // Caso del reporte: granja DAYLAND, lote "13 - 1", encaset 2026-06-08 sin hora informada.
    private static readonly DateTime Encaset = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime Junio(int dia) => new(2026, 6, dia, 0, 0, 0, DateTimeKind.Utc);

    // ── La regla sobre el número de día ──────────────────────────────────────
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    public void PrimeraSemana_ElPesajeEsDiario(int dia) =>
        Assert.True(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(dia));

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(13)]
    [InlineData(20)]
    public void FueraDelCierreDeSemana_NoSePidePeso(int dia) =>
        Assert.False(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(dia));

    [Theory]
    [InlineData(14)]
    [InlineData(21)]
    [InlineData(28)]
    [InlineData(35)]
    public void DesdeLaSegundaSemana_SePidePesoAlCierre(int dia) =>
        Assert.True(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(dia));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-7)]
    public void DiasAnterioresAlPrimerRegistro_NuncaPidenPeso(int dia) =>
        Assert.False(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(dia));

    // ── Elección del número según la empresa ─────────────────────────────────
    [Fact]
    public void ReglaActiva_SePesaAlCierreDeLaSemana()
    {
        // 14/06 es el día 7 (séptimo día de vida contando el encaset como día 1) ⇒ se pesa.
        Assert.True(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(14), Encaset, null, reglaHoraActiva: true));
        // 15/06 es el día 8: arranca la semana 2, no se pesa.
        Assert.False(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(15), Encaset, null, reglaHoraActiva: true));
        // 21/06 es el día 14 ⇒ cierre de la semana 2.
        Assert.True(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(21), Encaset, null, reglaHoraActiva: true));
    }

    [Fact]
    public void ReglaActiva_ElDiaDelEncasetTambienSePesa()
    {
        // Es el día 1: dentro de la primera semana, donde el pesaje es diario. Antes caía en la
        // edad 0 y quedaba fuera de la regla.
        Assert.True(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(8), Encaset, null, reglaHoraActiva: true));
    }

    [Fact]
    public void ReglaActiva_LoteTardio_LaSemanaArrancaAlDiaSiguiente()
    {
        var hora = new TimeOnly(15, 0);

        // El propio día del encaset no admite registro ⇒ día 0 ⇒ no se pide peso.
        Assert.False(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(8), Encaset, hora, reglaHoraActiva: true));
        // 09/06 es su día 1 y 15/06 su día 7 (cierre de semana).
        Assert.True(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(9), Encaset, hora, reglaHoraActiva: true));
        Assert.True(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(15), Encaset, hora, reglaHoraActiva: true));
        Assert.False(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(16), Encaset, hora, reglaHoraActiva: true));
    }

    // ── Regresión: sin el flag, el comportamiento histórico intacto ──────────
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(21)]
    [InlineData(30)]
    public void ReglaApagada_MismoSetDeDiasQueAntes_Regresion(int edad)
    {
        // Expresión histórica, evaluada sobre la EDAD cruda.
        var esperado = (edad >= 1 && edad <= 7) || (edad > 7 && edad % 7 == 0);

        Assert.Equal(esperado, PesajeEngordeCalculos.EsDiaDePesajeObligatorio(
            Encaset.AddDays(edad), Encaset, null, reglaHoraActiva: false));
    }

    [Fact]
    public void ReglaApagada_LaHoraDelLoteNoAlteraNada_Regresion()
    {
        // Aunque el lote traiga una hora tardía cargada, sin el flag la empresa se comporta igual.
        var conHora = PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(15), Encaset, new TimeOnly(18, 0), reglaHoraActiva: false);
        var sinHora = PesajeEngordeCalculos.EsDiaDePesajeObligatorio(Junio(15), Encaset, null, reglaHoraActiva: false);

        Assert.Equal(sinHora, conHora);
        Assert.True(conHora); // 15/06 es la edad 7: día de pesaje bajo la regla histórica
    }

    [Fact]
    public void DiaParaReglaDePesaje_EligeSegunElFlag()
    {
        Assert.Equal(7, PesajeEngordeCalculos.DiaParaReglaDePesaje(edad: 7, diaDeNegocio: 8, reglaHoraActiva: false));
        Assert.Equal(8, PesajeEngordeCalculos.DiaParaReglaDePesaje(edad: 7, diaDeNegocio: 8, reglaHoraActiva: true));
    }
}
