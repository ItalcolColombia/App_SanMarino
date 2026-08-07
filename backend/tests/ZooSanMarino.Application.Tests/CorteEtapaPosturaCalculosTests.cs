using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del corte entre etapas de postura. Nace del lote K345: 14 días de julio-2025 quedaron
/// registrados en levante Y en producción con el mismo consumo, y todo reporte que suma el ciclo
/// contaba dos veces 16.952 kg de alimento y 10 aves.
/// </summary>
public class CorteEtapaPosturaCalculosTests
{
    private static CorteEtapaPosturaCalculos.AporteDia Dia(decimal kg = 0, int mort = 0, int sel = 0) =>
        new(kg, mort, sel);

    [Fact]
    public void DosDiasConConsumo_EsDobleConteo()
    {
        // El caso K345 exacto: 16-jul-2025, K345A, 1.020,4 kg en las dos tablas.
        var levante = Dia(kg: 1020.4m, mort: 1);
        var produccion = Dia(kg: 1020.4m, mort: 1);

        Assert.True(CorteEtapaPosturaCalculos.HayDobleConteo(levante, produccion));
    }

    [Fact]
    public void FilaDeSoloHuevos_NoBloquea()
    {
        // El arrastre de huevos del levante crea una fila de producción sin consumo ni bajas: es
        // legítima y no debe impedir que el día siga registrando su levante.
        var nuevoLevante = Dia(kg: 900m, mort: 2);
        var arrastreHuevos = CorteEtapaPosturaCalculos.SinAporte;

        Assert.False(CorteEtapaPosturaCalculos.HayDobleConteo(nuevoLevante, arrastreHuevos));
    }

    [Fact]
    public void RegistroVacio_ContraDiaConAporte_NoBloquea()
    {
        // Una fila sin consumo ni bajas (solo pesaje, solo observación) no aporta al doble conteo.
        var nuevoVacio = CorteEtapaPosturaCalculos.SinAporte;
        var otraEtapaConDatos = Dia(kg: 800m, mort: 3, sel: 5);

        Assert.False(CorteEtapaPosturaCalculos.HayDobleConteo(nuevoVacio, otraEtapaConDatos));
    }

    [Theory]
    [InlineData(10.0, 0, 0)]
    [InlineData(0.0, 1, 0)]
    [InlineData(0.0, 0, 1)]
    public void CualquierAporte_CuentaComoAporte(double kg, int mort, int sel)
    {
        Assert.True(Dia((decimal)kg, mort, sel).Aporta);
    }

    [Fact]
    public void SinNada_NoAporta()
    {
        Assert.False(CorteEtapaPosturaCalculos.SinAporte.Aporta);
        Assert.False(Dia().Aporta);
    }

    [Fact]
    public void SoloMortalidadEnAmbas_EsDobleConteo()
    {
        // Sin alimento pero con bajas por los dos lados: el saldo de aves también se duplica.
        Assert.True(CorteEtapaPosturaCalculos.HayDobleConteo(Dia(mort: 4), Dia(sel: 2)));
    }

    [Fact]
    public void ElMensaje_DiceQueElDatoNoSePierde_YComoCorregirlo()
    {
        var msg = CorteEtapaPosturaCalculos.MensajeLevanteChocaConProduccion(new DateTime(2025, 7, 16));

        Assert.Contains("16/07/2025", msg);
        Assert.Contains("producción", msg);
        Assert.Contains("dos veces", msg);
    }

    [Fact]
    public void ElMensaje_NombraLaEtapaCorrectaEnCadaDireccion()
    {
        var dia = new DateTime(2025, 7, 16);

        var desdeLevante = CorteEtapaPosturaCalculos.MensajeLevanteChocaConProduccion(dia);
        var desdeProduccion = CorteEtapaPosturaCalculos.MensajeProduccionChocaConLevante(dia);

        Assert.Contains("ya tiene un registro de producción", desdeLevante);
        Assert.Contains("ya tiene un registro de levante", desdeProduccion);
        Assert.NotEqual(desdeLevante, desdeProduccion);
    }
}
