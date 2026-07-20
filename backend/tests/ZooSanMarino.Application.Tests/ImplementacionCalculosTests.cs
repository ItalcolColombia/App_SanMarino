// tests/ZooSanMarino.Application.Tests/ImplementacionCalculosTests.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class ImplementacionCalculosTests
{
    // ── CalcularResumen ──────────────────────────────────────────────────────

    [Fact]
    public void Resumen_SinTareas_TodoCero()
    {
        var r = ImplementacionCalculos.CalcularResumen(0, 0, 0);
        Assert.Equal(0, r.TotalTareas);
        Assert.Equal(0m, r.PorcentajeAvance);
        Assert.Equal(0m, r.PorcentajeConfirmado);
    }

    [Fact]
    public void Resumen_ConfirmadasCuentanComoAvance()
    {
        // 10 tareas: 3 completadas (check del gestor) + 2 confirmadas → avance 5/10.
        var r = ImplementacionCalculos.CalcularResumen(10, 3, 2);
        Assert.Equal(50m, r.PorcentajeAvance);
        Assert.Equal(20m, r.PorcentajeConfirmado);
    }

    [Theory]
    [InlineData(3, 1, 0, 33.3)]  // 1/3 = 33.33… → 33.3
    [InlineData(3, 2, 0, 66.7)]  // 2/3 = 66.66… → 66.7
    [InlineData(8, 0, 1, 12.5)]  // exacto, sin redondeo
    public void Resumen_RedondeaAUnDecimal(int total, int completadas, int confirmadas, decimal esperado)
    {
        var r = ImplementacionCalculos.CalcularResumen(total, completadas, confirmadas);
        Assert.Equal(esperado, r.PorcentajeAvance);
    }

    [Fact]
    public void Resumen_TodasConfirmadas_Cien()
    {
        var r = ImplementacionCalculos.CalcularResumen(4, 0, 4);
        Assert.Equal(100m, r.PorcentajeAvance);
        Assert.Equal(100m, r.PorcentajeConfirmado);
    }

    // ── DeterminarEstadoPlan ─────────────────────────────────────────────────

    [Fact]
    public void EstadoPlan_CanceladoSeRespeta()
        => Assert.Equal("cancelado", ImplementacionCalculos.DeterminarEstadoPlan("cancelado", 10, 10, 10));

    [Fact]
    public void EstadoPlan_SinTareas_Borrador()
        => Assert.Equal("borrador", ImplementacionCalculos.DeterminarEstadoPlan("en_progreso", 0, 0, 0));

    [Fact]
    public void EstadoPlan_TodasConfirmadas_Completado()
        => Assert.Equal("completado", ImplementacionCalculos.DeterminarEstadoPlan("en_progreso", 5, 5, 5));

    [Fact]
    public void EstadoPlan_AvanceParcial_EnProgreso()
        => Assert.Equal("en_progreso", ImplementacionCalculos.DeterminarEstadoPlan("borrador", 5, 1, 3));

    [Fact]
    public void EstadoPlan_ConTareasSinNingunCheck_Borrador()
        => Assert.Equal("borrador", ImplementacionCalculos.DeterminarEstadoPlan("borrador", 5, 0, 0));

    // ── EsTareaVencida ───────────────────────────────────────────────────────

    private static readonly DateTime Hoy = new(2026, 7, 20);

    [Fact]
    public void Vencida_FechaPasadaYPendiente_True()
        => Assert.True(ImplementacionCalculos.EsTareaVencida(new DateTime(2026, 7, 19), Hoy, "pendiente"));

    [Fact]
    public void Vencida_SinFechaProgramada_False()
        => Assert.False(ImplementacionCalculos.EsTareaVencida(null, Hoy, "pendiente"));

    [Fact]
    public void Vencida_FechaHoy_False()
        => Assert.False(ImplementacionCalculos.EsTareaVencida(new DateTime(2026, 7, 20), Hoy, "pendiente"));

    [Theory]
    [InlineData("completada")]
    [InlineData("confirmada")]
    public void Vencida_YaConCheck_False(string estado)
        => Assert.False(ImplementacionCalculos.EsTareaVencida(new DateTime(2026, 1, 1), Hoy, estado));

    // ── PuedeConfirmar ───────────────────────────────────────────────────────

    private static readonly Guid UsuarioA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UsuarioB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void PuedeConfirmar_AsignadoYCompletada_True()
        => Assert.True(ImplementacionCalculos.PuedeConfirmar("completada", UsuarioA, UsuarioA));

    [Fact]
    public void PuedeConfirmar_OtroUsuario_False()
        => Assert.False(ImplementacionCalculos.PuedeConfirmar("completada", UsuarioA, UsuarioB));

    [Fact]
    public void PuedeConfirmar_TareaPendiente_False()
        => Assert.False(ImplementacionCalculos.PuedeConfirmar("pendiente", UsuarioA, UsuarioA));

    [Fact]
    public void PuedeConfirmar_NullsFailClosed()
    {
        Assert.False(ImplementacionCalculos.PuedeConfirmar("completada", null, UsuarioA));
        Assert.False(ImplementacionCalculos.PuedeConfirmar("completada", UsuarioA, null));
        Assert.False(ImplementacionCalculos.PuedeConfirmar("completada", null, null));
    }

    // ── PlantillaPorDefecto ──────────────────────────────────────────────────

    [Fact]
    public void Plantilla_NoVaciaYOrdenGlobalCreciente()
    {
        var plantilla = ImplementacionCalculos.PlantillaPorDefecto();
        Assert.NotEmpty(plantilla);
        var ordenes = plantilla.Select(t => t.Orden).ToList();
        Assert.Equal(ordenes.OrderBy(o => o).ToList(), ordenes);
        Assert.Equal(ordenes.Count, ordenes.Distinct().Count());
    }

    [Fact]
    public void Plantilla_IncluyeCategoriasDeEntrega()
    {
        var categorias = ImplementacionCalculos.PlantillaPorDefecto().Select(t => t.Categoria).Distinct().ToList();
        Assert.Contains("Parametrizaciones", categorias);
        Assert.Contains("Capacitación", categorias);
        Assert.Contains("Carga de datos", categorias);
        Assert.Contains("Puesta en marcha", categorias);
    }
}
