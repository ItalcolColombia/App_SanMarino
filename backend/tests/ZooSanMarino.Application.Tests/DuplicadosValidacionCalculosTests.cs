using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fija la regla con la que se revierten los consumos que la validación aplicó dos veces.
/// Los casos con datos reales usan los 8 pares medidos en ItalcolPanama (ago-2026, 19.677,24 kg).
/// </summary>
public class DuplicadosValidacionCalculosTests
{
    private static MovimientoDuplicable Mov(long id, string refe, decimal kg,
        int farm = 107, string? galpon = "G0471", int item = 223, string? nucleo = null) =>
        new(id, refe, farm, nucleo, galpon, item, kg);

    [Fact]
    public void SinDuplicados_NoDevuelveNada()
    {
        var movs = new[]
        {
            Mov(1, "Seguimiento aves engorde #12680 2026-08-21 (validado)", 2268m),
            Mov(2, "Seguimiento aves engorde #12681 2026-08-22 (validado)", 2268m),
        };

        Assert.Empty(DuplicadosValidacionCalculos.Reversiones(movs));
    }

    [Fact]
    public void ParDuplicado_ConservaElDeMenorId_YRevierteElOtro()
    {
        var movs = new[]
        {
            Mov(13806, "Seguimiento aves engorde #12680 2026-08-21 (validado)", 2268m),
            Mov(13803, "Seguimiento aves engorde #12680 2026-08-21 (validado)", 2268m),
        };

        var r = Assert.Single(DuplicadosValidacionCalculos.Reversiones(movs));

        Assert.Equal(13806, r.IdARevertir);
        Assert.Equal(13803, r.IdQueSeConserva);
        Assert.Equal(2268m, r.KgADevolver);
    }

    [Fact]
    public void TresCopias_RevierteDos_YConservaUna()
    {
        var movs = new[]
        {
            Mov(30, "ref", 100m), Mov(10, "ref", 100m), Mov(20, "ref", 100m),
        };

        var rs = DuplicadosValidacionCalculos.Reversiones(movs);

        Assert.Equal(2, rs.Count);
        Assert.All(rs, r => Assert.Equal(10, r.IdQueSeConserva));
        Assert.Equal(new long[] { 20, 30 }, rs.Select(r => r.IdARevertir).ToArray());
        Assert.Equal(200m, DuplicadosValidacionCalculos.TotalKgDeMas(rs));
    }

    [Fact]
    public void MismaReferenciaEnGalponesDistintos_NoEsDuplicado()
    {
        // Dos galpones pueden consumir el mismo día el mismo ítem: son dos consumos legítimos.
        var movs = new[]
        {
            Mov(1, "ref", 500m, galpon: "G0471"),
            Mov(2, "ref", 500m, galpon: "G0481"),
        };

        Assert.Empty(DuplicadosValidacionCalculos.Reversiones(movs));
    }

    [Fact]
    public void MismaReferenciaConCantidadDistinta_NoEsDuplicado()
    {
        // Dos líneas de alimento distintas del mismo seguimiento no son una duplicación.
        var movs = new[] { Mov(1, "ref", 500m), Mov(2, "ref", 700m) };

        Assert.Empty(DuplicadosValidacionCalculos.Reversiones(movs));
    }

    [Fact]
    public void UbicacionNula_YCadenaVacia_SonLaMismaUbicacion()
    {
        var movs = new[]
        {
            Mov(1, "ref", 100m, galpon: null, nucleo: null),
            Mov(2, "ref", 100m, galpon: "  ", nucleo: ""),
        };

        var r = Assert.Single(DuplicadosValidacionCalculos.Reversiones(movs));
        Assert.Equal(2, r.IdARevertir);
    }

    [Fact]
    public void LosOchoParesDePanama_DanLos1967724Kg()
    {
        // Los 8 pares medidos en la copia de producción el 31-ago-2026.
        var pares = new (string Refe, int Farm, string Galpon, int Item, decimal Kg)[]
        {
            ("#11993 2026-08-17 (validado)", 105, "G0491", 213, 2758m),
            ("#12004 2026-08-11 (validado)", 105, "G0492", 213, 2041m),
            ("#12635 2026-08-21 (validado)", 105, "G0494", 213, 5670m),
            ("#12660 2026-08-25 (validado)", 106, "G0482", 213, 1361m),
            ("#12666 2026-08-24 (validado)", 107, "G0461", 223, 1542.24m),
            ("#12680 2026-08-21 (validado)", 107, "G0471", 223, 2268m),
            ("#12681 2026-08-22 (validado)", 107, "G0471", 223, 2268m),
            ("#12770 2026-08-26 (validado)", 106, "G0481", 213, 1769m),
        };

        long id = 1;
        var movs = pares
            .SelectMany(p => new[]
            {
                new MovimientoDuplicable(id++, p.Refe, p.Farm, null, p.Galpon, p.Item, p.Kg),
                new MovimientoDuplicable(id++, p.Refe, p.Farm, null, p.Galpon, p.Item, p.Kg),
            })
            .ToList();

        var rs = DuplicadosValidacionCalculos.Reversiones(movs);

        Assert.Equal(8, rs.Count);
        Assert.Equal(19677.24m, DuplicadosValidacionCalculos.TotalKgDeMas(rs));
    }

    [Fact]
    public void KgPorUbicacion_SumaLosDosParesDelMismoGalpon()
    {
        // G0471 tiene DOS pares duplicados (#12680 y #12681): al stock hay que devolverle 4.536 kg
        // de una vez, no dos veces 2.268 en filas separadas.
        var movs = new[]
        {
            Mov(1, "#12680 2026-08-21 (validado)", 2268m),
            Mov(2, "#12680 2026-08-21 (validado)", 2268m),
            Mov(3, "#12681 2026-08-22 (validado)", 2268m),
            Mov(4, "#12681 2026-08-22 (validado)", 2268m),
        };

        var porUbicacion = DuplicadosValidacionCalculos.KgPorUbicacion(
            DuplicadosValidacionCalculos.Reversiones(movs));

        var u = Assert.Single(porUbicacion);
        Assert.Equal(107, u.FarmId);
        Assert.Equal("G0471", u.GalponId);
        Assert.Equal(223, u.ItemId);
        Assert.Equal(4536m, u.KgADevolver);
    }

    [Fact]
    public void KgPorUbicacion_SeparaGalponesEItemsDistintos()
    {
        var movs = new[]
        {
            Mov(1, "ref", 100m, farm: 105, galpon: "G0491", item: 213),
            Mov(2, "ref", 100m, farm: 105, galpon: "G0491", item: 213),
            Mov(3, "ref", 50m,  farm: 106, galpon: "G0481", item: 223),
            Mov(4, "ref", 50m,  farm: 106, galpon: "G0481", item: 223),
        };

        var porUbicacion = DuplicadosValidacionCalculos.KgPorUbicacion(
            DuplicadosValidacionCalculos.Reversiones(movs));

        Assert.Equal(2, porUbicacion.Count);
        Assert.Equal(100m, porUbicacion.Single(u => u.GalponId == "G0491").KgADevolver);
        Assert.Equal(50m, porUbicacion.Single(u => u.GalponId == "G0481").KgADevolver);
    }
}
