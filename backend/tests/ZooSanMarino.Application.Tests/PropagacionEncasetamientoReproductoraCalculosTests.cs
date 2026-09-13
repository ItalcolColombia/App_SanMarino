using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Propagación de la fecha/hora de encasetamiento del lote pollo engorde a sus lotes reproductora:
/// se rechaza ANTES de escribir si la fecha nueva dejaría registros fuera de la semana de recogida.
/// </summary>
public class PropagacionEncasetamientoReproductoraCalculosTests
{
    private static PropagacionEncasetamientoReproductoraCalculos.LoteHijo Hijo(
        int id, string nombre, params string[] fechas) =>
        new(id, nombre, fechas.Select(f => DateTime.SpecifyKind(DateTime.Parse(f), DateTimeKind.Utc)).ToList());

    private static DateTime Fecha(string ymd) =>
        DateTime.SpecifyKind(DateTime.Parse(ymd), DateTimeKind.Utc).AddHours(12);

    [Fact]
    public void SinFechaNueva_NoHayNadaQuePropagar()
    {
        var diag = PropagacionEncasetamientoReproductoraCalculos.Diagnosticar(
            null, null, new[] { Hijo(1, "35", "2026-09-04") });

        Assert.True(diag.Compatible);
        Assert.Empty(diag.Fuera);
    }

    [Fact]
    public void SinHijos_EsCompatible()
    {
        var diag = PropagacionEncasetamientoReproductoraCalculos.Diagnosticar(
            Fecha("2026-09-03"), new TimeOnly(21, 35),
            Array.Empty<PropagacionEncasetamientoReproductoraCalculos.LoteHijo>());

        Assert.True(diag.Compatible);
    }

    [Fact]
    public void ElCasoDelTicket_LaFechaRealDelLote255_EsCompatible()
    {
        // Lote 255: encaset 03-sep 21:35, reproductora capturó del 04 al 09-sep (edades 1..6).
        var diag = PropagacionEncasetamientoReproductoraCalculos.Diagnosticar(
            Fecha("2026-09-03"), new TimeOnly(21, 35),
            new[] { Hijo(160, "36", "2026-09-04", "2026-09-05", "2026-09-06", "2026-09-07", "2026-09-08", "2026-09-09") });

        Assert.True(diag.Compatible);
    }

    [Fact]
    public void FechaQueDejaRegistrosAntesDelEncaset_SeRechaza()
    {
        var diag = PropagacionEncasetamientoReproductoraCalculos.Diagnosticar(
            Fecha("2026-09-06"), null, new[] { Hijo(160, "36", "2026-09-04", "2026-09-05", "2026-09-07") });

        Assert.False(diag.Compatible);
        Assert.Equal(2, diag.Fuera.Count);
        Assert.All(diag.Fuera, f => Assert.True(f.Edad < 0));
    }

    [Fact]
    public void FechaQueEmpujaRegistrosMasAllaDeLaSemanaDeRecogida_SeRechaza()
    {
        // Mover el encaset hacia atrás alarga la edad: con 8 días ya no cruza (la fn consolida 0..7).
        var diag = PropagacionEncasetamientoReproductoraCalculos.Diagnosticar(
            Fecha("2026-08-27"), null, new[] { Hijo(160, "36", "2026-09-04") });

        Assert.False(diag.Compatible);
        Assert.Equal(8, diag.Fuera.Single().Edad);
    }

    [Fact]
    public void HoraTardia_SubeElMinimoDeLaVentana_YElDiaDelEncasetYaNoVale()
    {
        var registrosElDiaDelEncaset = new[] { Hijo(160, "36", "2026-09-03") };

        Assert.True(PropagacionEncasetamientoReproductoraCalculos
            .Diagnosticar(Fecha("2026-09-03"), null, registrosElDiaDelEncaset).Compatible);

        var conHoraTardia = PropagacionEncasetamientoReproductoraCalculos
            .Diagnosticar(Fecha("2026-09-03"), new TimeOnly(21, 35), registrosElDiaDelEncaset);

        Assert.False(conHoraTardia.Compatible);
        Assert.Equal(0, conHoraTardia.Fuera.Single().Edad);
    }

    [Fact]
    public void ElMensajeNombraElLote_LaFechaYLaEdad()
    {
        var nuevaFecha = Fecha("2026-09-06");
        var diag = PropagacionEncasetamientoReproductoraCalculos.Diagnosticar(
            nuevaFecha, null, new[] { Hijo(160, "36", "2026-09-04") });

        var mensaje = PropagacionEncasetamientoReproductoraCalculos.MensajeIncompatible(diag, nuevaFecha);

        Assert.Contains("'36'", mensaje);
        Assert.Contains("2026-09-04", mensaje);
        Assert.Contains("2026-09-06", mensaje);
    }

    [Fact]
    public void ElMensajeResumeCuandoSonMuchos()
    {
        var fechas = Enumerable.Range(1, 9).Select(d => $"2026-08-{d:00}").ToArray();
        var nuevaFecha = Fecha("2026-09-06");
        var diag = PropagacionEncasetamientoReproductoraCalculos.Diagnosticar(
            nuevaFecha, null, new[] { Hijo(160, "36", fechas) });

        var mensaje = PropagacionEncasetamientoReproductoraCalculos.MensajeIncompatible(diag, nuevaFecha);

        Assert.False(diag.Compatible);
        Assert.Equal(9, diag.Fuera.Count);
        Assert.Contains("y 4 más", mensaje);
    }
}
