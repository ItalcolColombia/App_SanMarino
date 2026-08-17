using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Franja válida (semana/día/fecha) y estado + desviación de aplicaciones del módulo Vacunación.
/// Casos: Postura (semana, franja lunes-domingo), Engorde (día puntual de edad), fecha fija sin
/// importar la fase, y los cinco estados posibles frente al umbral configurable de incumplimiento.
/// </summary>
public class VacunacionCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 1, 5); // lunes

    [Fact]
    public void Franja_Semana_UnaSemanaEsSieteDias_LunesADomingo()
    {
        // Semana 1 = lunes 5 ene (día 0) a domingo 11 ene (día 6): rango 0/6.
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 1, null, rangoDiasAntes: 0, rangoDiasDespues: 6);

        Assert.Equal(new DateTime(2026, 1, 5), f.FechaInicio);
        Assert.Equal(new DateTime(2026, 1, 11), f.FechaFin);
    }

    [Fact]
    public void Franja_Semana_SemanaN_OffsetSieteDiasPorSemana()
    {
        // Semana 4 = día 21 a 27 desde encaset.
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 4, null, rangoDiasAntes: 0, rangoDiasDespues: 6);

        Assert.Equal(Encaset.AddDays(21), f.FechaInicio);
        Assert.Equal(Encaset.AddDays(27), f.FechaFin);
    }

    [Fact]
    public void Franja_Dia_Engorde_PuntualConRangoAngosto()
    {
        // Engorde: día 10 de edad, franja angosta 1 día antes/después.
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Dia", 10, null, rangoDiasAntes: 1, rangoDiasDespues: 1);

        Assert.Equal(Encaset.AddDays(9), f.FechaInicio);
        Assert.Equal(Encaset.AddDays(11), f.FechaFin);
    }

    [Fact]
    public void Franja_Fecha_UsaFechaObjetivoSinImportarEncaset()
    {
        var fechaFija = new DateTime(2026, 3, 1);
        var f = VacunacionCalculos.CalcularFranja(null, "Fecha", null, fechaFija, rangoDiasAntes: 0, rangoDiasDespues: 0);

        Assert.Equal(fechaFija, f.FechaInicio);
        Assert.Equal(fechaFija, f.FechaFin);
    }

    [Fact]
    public void EstadoAplicacion_DentroDeFranja_AplicadoATiempo()
    {
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 1, null, 0, 6);
        var r = VacunacionCalculos.CalcularEstadoAplicacion(f, Encaset.AddDays(3), diasUmbralIncumplido: 14);

        Assert.Equal(VacunacionCalculos.EstadoAplicado, r.Estado);
        Assert.Equal(0, r.DiasDesviacion);
        Assert.False(r.Incumplido);
        Assert.False(r.RequiereMotivo);
    }

    [Fact]
    public void EstadoAplicacion_UnaSemanaTarde_TardioPeroNoIncumplido()
    {
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 1, null, 0, 6); // fin = 11 ene
        var r = VacunacionCalculos.CalcularEstadoAplicacion(f, new DateTime(2026, 1, 18), diasUmbralIncumplido: 14);

        Assert.Equal(VacunacionCalculos.EstadoAplicadoTardio, r.Estado);
        Assert.Equal(7, r.DiasDesviacion);
        Assert.False(r.Incumplido); // 7 días < umbral 14
        Assert.True(r.RequiereMotivo);
    }

    [Fact]
    public void EstadoAplicacion_DosSemanasOMasTarde_IncumplidoRojo()
    {
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 1, null, 0, 6); // fin = 11 ene
        var r = VacunacionCalculos.CalcularEstadoAplicacion(f, new DateTime(2026, 1, 25), diasUmbralIncumplido: 14);

        Assert.Equal(VacunacionCalculos.EstadoAplicadoTardio, r.Estado);
        Assert.Equal(14, r.DiasDesviacion);
        Assert.True(r.Incumplido); // 14 días >= umbral 14
        Assert.True(r.RequiereMotivo);
    }

    [Fact]
    public void EstadoAplicacion_UmbralConfigurable_CambiaElCorteDeIncumplido()
    {
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 1, null, 0, 6);
        var r = VacunacionCalculos.CalcularEstadoAplicacion(f, new DateTime(2026, 1, 18), diasUmbralIncumplido: 7);

        Assert.Equal(7, r.DiasDesviacion);
        Assert.True(r.Incumplido); // con umbral 7, 7 días ya es incumplido (antes no lo era con umbral 14)
    }

    [Fact]
    public void EstadoAplicacion_AntesDeQueAbraLaFranja_Adelantado()
    {
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 4, null, 0, 6); // inicio = día 21
        var r = VacunacionCalculos.CalcularEstadoAplicacion(f, Encaset.AddDays(18), diasUmbralIncumplido: 14);

        Assert.Equal(VacunacionCalculos.EstadoAplicadoAdelantado, r.Estado);
        Assert.Equal(-3, r.DiasDesviacion);
        Assert.False(r.Incumplido); // adelantado nunca se marca incumplido en rojo
        Assert.True(r.RequiereMotivo);
    }

    [Fact]
    public void EstadoNoAplicado_SiempreExigeMotivo_NuncaIncumplido()
    {
        var r = VacunacionCalculos.CalcularEstadoNoAplicado();

        Assert.Equal(VacunacionCalculos.EstadoNoAplicado, r.Estado);
        Assert.Equal(0, r.DiasDesviacion);
        Assert.False(r.Incumplido);
        Assert.True(r.RequiereMotivo);
    }

    /// <summary>
    /// W3.0 — la extracción de <c>ProyectarAplicacion</c> tiene que ser NEUTRA: barre 40 días
    /// alrededor de la franja y exige que estado, desviación y "requiere motivo" salgan idénticos
    /// por los dos caminos. Es el gate de que el pre-chequeo de la UI y el guardado no puedan
    /// divergir nunca.
    /// </summary>
    [Fact]
    public void ProyectarAplicacion_EsLaMismaFormulaQueCalcularEstadoAplicacion()
    {
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 4, null, 0, 6);

        for (var offset = 0; offset <= 40; offset++)
        {
            var fecha = Encaset.AddDays(offset);
            var completo = VacunacionCalculos.CalcularEstadoAplicacion(f, fecha, diasUmbralIncumplido: 14);
            var proyeccion = VacunacionCalculos.ProyectarAplicacion(f, fecha);

            Assert.Equal(completo.Estado, proyeccion.Estado);
            Assert.Equal(completo.DiasDesviacion, proyeccion.DiasDesviacion);
            Assert.Equal(completo.RequiereMotivo, proyeccion.RequiereMotivo);
        }
    }

    [Fact]
    public void ProyectarAplicacion_NoDependeDelUmbral_NoDiceNadaDeIncumplimiento()
    {
        // El pre-chequeo de la UI no tiene el umbral de la empresa: por eso la proyección no lo pide.
        var f = VacunacionCalculos.CalcularFranja(Encaset, "Semana", 4, null, 0, 6);
        var p = VacunacionCalculos.ProyectarAplicacion(f, Encaset.AddDays(40));

        Assert.Equal(VacunacionCalculos.EstadoAplicadoTardio, p.Estado);
        Assert.Equal(13, p.DiasDesviacion); // fin de franja = día 27
        Assert.True(p.RequiereMotivo);
    }
}
