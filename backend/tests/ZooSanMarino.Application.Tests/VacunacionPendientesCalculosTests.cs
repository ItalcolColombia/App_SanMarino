using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Clasificación de la bandeja "hoy me toca" (W3.1). Los casos que importan son los <b>bordes</b>:
/// el día en que abre la franja, el día en que cierra, el día siguiente al cierre y el límite del
/// horizonte. Esta clase es la especificación ejecutable de <c>fn_vacunacion_pendientes</c>.
/// </summary>
public class VacunacionPendientesCalculosTests
{
    private static readonly DateTime Inicio = new(2026, 8, 10);
    private static readonly DateTime Fin = new(2026, 8, 16);

    [Fact]
    public void HoyDentroDeLaFranja_EsEnFranja_SinDias()
    {
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, new DateTime(2026, 8, 13), 7);

        Assert.NotNull(c);
        Assert.Equal(VacunacionPendientesCalculos.SituacionEnFranja, c!.Value.Situacion);
        Assert.Equal(0, c.Value.Dias);
    }

    [Fact]
    public void HoyEsElPrimerDiaDeLaFranja_YaCuenta()
    {
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Inicio, 7);

        Assert.Equal(VacunacionPendientesCalculos.SituacionEnFranja, c!.Value.Situacion);
        Assert.Equal(0, c.Value.Dias);
    }

    [Fact]
    public void HoyEsElUltimoDiaDeLaFranja_TodaviaCumple_NoEstaVencido()
    {
        // Frontera clave: coincide con ProyectarAplicacion, que ese día NO exige motivo.
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Fin, 7);

        Assert.Equal(VacunacionPendientesCalculos.SituacionEnFranja, c!.Value.Situacion);
        Assert.Equal(0, c.Value.Dias);
    }

    [Fact]
    public void ElDiaSiguienteAlFin_YaEsVencidoPorUnDia()
    {
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Fin.AddDays(1), 7);

        Assert.Equal(VacunacionPendientesCalculos.SituacionVencido, c!.Value.Situacion);
        Assert.Equal(1, c.Value.Dias);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(30, 30)]
    [InlineData(365, 365)]
    public void Vencido_LosDiasSonElAtrasoContraElFinDeFranja(int diasDespues, int esperado)
    {
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Fin.AddDays(diasDespues), 7);

        Assert.Equal(VacunacionPendientesCalculos.SituacionVencido, c!.Value.Situacion);
        Assert.Equal(esperado, c.Value.Dias);
    }

    [Fact]
    public void Vencido_NoLoTapaElHorizonte_SiempreSale()
    {
        // Un vencido de hace un año sigue siendo pendiente: el horizonte sólo recorta lo que VIENE.
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Fin.AddDays(400), diasHorizonte: 0);

        Assert.Equal(VacunacionPendientesCalculos.SituacionVencido, c!.Value.Situacion);
    }

    [Fact]
    public void Proximo_DentroDelHorizonte_LosDiasSonNegativos()
    {
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Inicio.AddDays(-3), 7);

        Assert.Equal(VacunacionPendientesCalculos.SituacionProximo, c!.Value.Situacion);
        Assert.Equal(-3, c.Value.Dias);
    }

    [Fact]
    public void Proximo_ElBordeExactoDelHorizonteEntra()
    {
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Inicio.AddDays(-7), diasHorizonte: 7);

        Assert.Equal(VacunacionPendientesCalculos.SituacionProximo, c!.Value.Situacion);
        Assert.Equal(-7, c.Value.Dias);
    }

    [Fact]
    public void UnDiaMasAllaDelHorizonte_NoEntraEnLaBandeja()
    {
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Inicio.AddDays(-8), diasHorizonte: 7);

        Assert.Null(c);
    }

    [Fact]
    public void HorizonteCero_SoloVencidosYEnFranja()
    {
        Assert.Null(VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Inicio.AddDays(-1), diasHorizonte: 0));
        Assert.Equal(
            VacunacionPendientesCalculos.SituacionEnFranja,
            VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Inicio, diasHorizonte: 0)!.Value.Situacion);
    }

    [Fact]
    public void HorizonteNegativo_SeTrataComoCero_NoRevienta()
    {
        Assert.Null(VacunacionPendientesCalculos.Clasificar(Inicio, Fin, Inicio.AddDays(-1), diasHorizonte: -5));
    }

    [Fact]
    public void LaHoraSeIgnora_SoloCuentaElDia()
    {
        var conHora = Fin.AddHours(23).AddMinutes(59);
        var c = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, conHora, 7);

        Assert.Equal(VacunacionPendientesCalculos.SituacionEnFranja, c!.Value.Situacion);
    }

    [Fact]
    public void FranjaDeUnSoloDia_SeComportaIgualEnLosTresBordes()
    {
        var dia = new DateTime(2026, 8, 12);

        Assert.Equal(VacunacionPendientesCalculos.SituacionProximo,
            VacunacionPendientesCalculos.Clasificar(dia, dia, dia.AddDays(-1), 7)!.Value.Situacion);
        Assert.Equal(VacunacionPendientesCalculos.SituacionEnFranja,
            VacunacionPendientesCalculos.Clasificar(dia, dia, dia, 7)!.Value.Situacion);
        Assert.Equal(VacunacionPendientesCalculos.SituacionVencido,
            VacunacionPendientesCalculos.Clasificar(dia, dia, dia.AddDays(1), 7)!.Value.Situacion);
    }

    [Fact]
    public void FranjaInvertida_NoRompe_MandaElFinDeFranja()
    {
        // Dato imposible por construcción (los rangos son >= 0), pero la función es total:
        // con el fin antes que el inicio, gana la comparación contra el fin.
        var c = VacunacionPendientesCalculos.Clasificar(Fin, Inicio, new DateTime(2026, 8, 20), 7);

        Assert.Equal(VacunacionPendientesCalculos.SituacionVencido, c!.Value.Situacion);
    }

    [Fact]
    public void ConcuerdaConProyectarAplicacion_EnLosDiasQueExigenMotivo()
    {
        // El contrato cruzado: "vencido" ⇔ aplicar hoy sería tardío ⇒ ProyectarAplicacion exige motivo.
        var franja = new VacunacionCalculos.Franja(Inicio, Fin);

        foreach (var offset in new[] { -1, 0, 1, 5, 6, 7, 10 })
        {
            var hoy = Inicio.AddDays(offset);
            var clasificacion = VacunacionPendientesCalculos.Clasificar(Inicio, Fin, hoy, 30);
            var proyeccion = VacunacionCalculos.ProyectarAplicacion(franja, hoy);

            var enFranja = clasificacion!.Value.Situacion == VacunacionPendientesCalculos.SituacionEnFranja;
            Assert.Equal(enFranja, !proyeccion.RequiereMotivo);
            if (clasificacion.Value.Situacion == VacunacionPendientesCalculos.SituacionVencido)
                Assert.Equal(clasificacion.Value.Dias, proyeccion.DiasDesviacion);
        }
    }
}
