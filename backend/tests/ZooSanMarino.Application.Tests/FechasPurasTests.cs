// tests/ZooSanMarino.Application.Tests/FechasPurasTests.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class FechasPurasTests
{
    private static readonly DateTime MediodiaUtc21 =
        DateTime.SpecifyKind(new DateTime(2026, 7, 21, 12, 0, 0), DateTimeKind.Utc);

    // ── Casos reales del front ───────────────────────────────────────────────

    [Fact]
    public void MedianocheUtc_ConservaElDiaDigitado()
    {
        // Front legado: "2026-07-21T00:00:00Z" (medianoche UTC) — el caso que se veía un día menos
        var v = DateTime.SpecifyKind(new DateTime(2026, 7, 21, 0, 0, 0), DateTimeKind.Utc);
        Assert.Equal(MediodiaUtc21, FechasPuras.AnclarMediodiaUtc(v));
    }

    [Fact]
    public void MediodiaUtc_QuedaIgual()
    {
        // Front nuevo: ymdToIsoUtcNoon → "2026-07-21T12:00:00Z"
        Assert.Equal(MediodiaUtc21, FechasPuras.AnclarMediodiaUtc(MediodiaUtc21));
    }

    [Fact]
    public void MediodiaLocalBogotaEnUtc_ConservaElDia()
    {
        // Modal seguimiento engorde: ymdToIsoAtNoon desde UTC-5 → "2026-07-21T17:00:00Z"
        var v = DateTime.SpecifyKind(new DateTime(2026, 7, 21, 17, 0, 0), DateTimeKind.Utc);
        Assert.Equal(MediodiaUtc21, FechasPuras.AnclarMediodiaUtc(v));
    }

    [Fact]
    public void Unspecified_TomaLaFechaLiteral()
    {
        // JSON sin zona ("2026-07-21T00:00:00"): la fecha digitada manda, sin conversión de zona
        var v = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Unspecified);
        Assert.Equal(MediodiaUtc21, FechasPuras.AnclarMediodiaUtc(v));
    }

    [Fact]
    public void Local_ConvierteAUtcYTomaEsaFecha()
    {
        // Un Kind=Local se interpreta como instante: la fecha intencional es la del reloj UTC.
        var local = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Local);
        var esperado = DateTime.SpecifyKind(local.ToUniversalTime().Date, DateTimeKind.Utc).AddHours(12);
        Assert.Equal(esperado, FechasPuras.AnclarMediodiaUtc(local));
    }

    // ── Propiedades ──────────────────────────────────────────────────────────

    [Fact]
    public void Nullable_NullDevuelveNull()
    {
        Assert.Null(FechasPuras.AnclarMediodiaUtc((DateTime?)null));
    }

    [Fact]
    public void Nullable_ConValorDelegaAlNoNullable()
    {
        DateTime? v = DateTime.SpecifyKind(new DateTime(2026, 7, 21, 0, 0, 0), DateTimeKind.Utc);
        Assert.Equal(MediodiaUtc21, FechasPuras.AnclarMediodiaUtc(v));
    }

    [Fact]
    public void EsIdempotente()
    {
        var unaVez = FechasPuras.AnclarMediodiaUtc(new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(unaVez, FechasPuras.AnclarMediodiaUtc(unaVez));
    }

    [Fact]
    public void ResultadoSiempreKindUtcYHora12()
    {
        var r = FechasPuras.AnclarMediodiaUtc(new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc));
        Assert.Equal(DateTimeKind.Utc, r.Kind);
        Assert.Equal(12, r.Hour);
        Assert.Equal(new DateTime(2026, 12, 31), r.Date);
    }

    [Fact]
    public void RangoDeDia_MedianocheYMediodiaCaenEnElMismoDia()
    {
        // Regla usada por los filtros desde/hasta: [anclado-12h, anclado+12h) cubre
        // tanto filas legadas (00:00Z) como nuevas (12:00Z) del mismo día.
        var hasta = new DateTime(2026, 7, 21, 0, 0, 0, DateTimeKind.Unspecified);
        var inicio = FechasPuras.AnclarMediodiaUtc(hasta).AddHours(-12);
        var finExcl = FechasPuras.AnclarMediodiaUtc(hasta).AddHours(12);

        var filaLegada = DateTime.SpecifyKind(new DateTime(2026, 7, 21, 0, 0, 0), DateTimeKind.Utc);
        var filaNueva = MediodiaUtc21;

        Assert.True(filaLegada >= inicio && filaLegada < finExcl);
        Assert.True(filaNueva >= inicio && filaNueva < finExcl);
    }
}
