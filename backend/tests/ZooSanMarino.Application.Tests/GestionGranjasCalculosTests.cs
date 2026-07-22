using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.GestionGranjasCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Reglas puras del módulo "Gestión de Granjas":
///  - visibilidad de núcleos/galpones = granja asignada AND granja activa (mismo alcance que la tab Granjas),
///  - cascada al eliminar = deshabilitar solo lo que aún está activo (idempotente).
/// </summary>
public class GestionGranjasCalculosTests
{
    // ── Visibilidad por granja ───────────────────────────────────────────────
    [Theory]
    [InlineData(10, true)]   // asignada (10,20) + activa (10,30) → visible
    [InlineData(20, false)]  // asignada pero NO activa (eliminada) → no visible
    [InlineData(30, false)]  // activa pero NO asignada → no visible
    [InlineData(99, false)]  // ni asignada ni activa → no visible
    public void EsVisiblePorGranja_RequiereAsignadaYActiva(int granjaId, bool esperado)
    {
        var asignadas = new HashSet<int> { 10, 20 };
        var activas   = new HashSet<int> { 10, 30 };
        Assert.Equal(esperado, EsVisiblePorGranja(granjaId, asignadas, activas));
    }

    [Fact]
    public void FiltrarVisiblesPorGranja_DejaSoloAsignadasYActivas()
    {
        var items = new[]
        {
            (Id: "A", GranjaId: 10), // asignada + activa → queda
            (Id: "B", GranjaId: 20), // asignada, granja eliminada → fuera
            (Id: "C", GranjaId: 30), // activa pero no asignada → fuera
            (Id: "D", GranjaId: 10), // asignada + activa → queda
        };
        var asignadas = new HashSet<int> { 10, 20 };
        var activas   = new HashSet<int> { 10, 30 };

        var visibles = FiltrarVisiblesPorGranja(items, x => x.GranjaId, asignadas, activas)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal(new[] { "A", "D" }, visibles);
    }

    [Fact]
    public void FiltrarVisiblesPorGranja_SinGranjasAsignadas_DevuelveVacio()
    {
        var items = new[] { (Id: "A", GranjaId: 10) };
        var visibles = FiltrarVisiblesPorGranja(items, x => x.GranjaId, new HashSet<int>(), new HashSet<int> { 10 });
        Assert.Empty(visibles);
    }

    // ── Cascada: qué se inhabilita ───────────────────────────────────────────
    [Fact]
    public void RequiereInhabilitar_SoloActivos()
    {
        Assert.True(RequiereInhabilitar(null));                       // activo → sí
        Assert.False(RequiereInhabilitar(new DateTime(2026, 1, 1)));  // ya eliminado → no (idempotente)
    }
}
