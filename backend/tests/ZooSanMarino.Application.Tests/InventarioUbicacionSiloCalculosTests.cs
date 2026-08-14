using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.InventarioUbicacionSiloCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Dónde vive el stock según el flag de la empresa (Santa Reyes, Fase B — casos 1 a 5 del plan
/// <c>fase_de_desarrollo/santa_reyes_silos_bodegas_inventario_plan.md</c>).
///
/// <para>
/// Lo que estos tests protegen NO es la feature nueva: es que con el flag <b>apagado</b> —o sea,
/// Sanmarino, Ecuador, Panamá y Demo— la ubicación salga <b>idéntica a la que entró</b>. Un error acá
/// no rompe nada visible: mueve el saldo de otra fila y se descubre semanas más tarde en un cuadre.
/// </para>
/// </summary>
public class InventarioUbicacionSiloCalculosTests
{
    // ── El modo sale del flag y de nada más ──────────────────────────────────────

    [Fact]
    public void ResolverModo_FlagApagado_EsClasico() =>
        Assert.Equal(ModoUbicacionInventario.Clasico, ResolverModo(false));

    [Fact]
    public void ResolverModo_FlagPrendido_EsPorSilo() =>
        Assert.Equal(ModoUbicacionInventario.PorSilo, ResolverModo(true));

    // ── Caso 1 — flag OFF, sin silo: la ubicación no se toca ─────────────────────

    [Theory]
    [InlineData("795634", "G0050")]   // Ecuador / Panamá: alimento por galpón
    [InlineData(null, null)]          // Colombia: alimento a nivel granja
    [InlineData("795634", null)]      // núcleo sin galpón: se conserva tal cual, no se "corrige"
    public void Caso1_FlagOff_SinSilo_UbicacionIntacta(string? nucleo, string? galpon)
    {
        Assert.Null(ValidarUbicacion(ModoUbicacionInventario.Clasico, siloId: null, galpon, esAlimento: true));

        var (n, g, s) = NormalizarUbicacion(ModoUbicacionInventario.Clasico, nucleo, galpon, siloId: null);

        Assert.Equal(nucleo, n);
        Assert.Equal(galpon, g);
        Assert.Null(s);
    }

    // ── Caso 2 — flag OFF, CON silo: se rechaza (no se mezclan los dos modelos) ──

    [Fact]
    public void Caso2_FlagOff_ConSilo_SeRechaza()
    {
        var error = ValidarUbicacion(ModoUbicacionInventario.Clasico, siloId: 7, galponId: "G0050", esAlimento: true);

        Assert.Equal(MensajeSiloNoAplica, error);
    }

    [Fact]
    public void Caso2_FlagOff_ConSilo_NormalizarNuncaLoPersiste()
    {
        // Si por cualquier camino el rechazo no se aplicara, la normalización sigue sin escribir el
        // silo: una empresa con el flag apagado no puede terminar con silo_id en la BD.
        var (n, g, s) = NormalizarUbicacion(ModoUbicacionInventario.Clasico, "795634", "G0050", siloId: 7);

        Assert.Equal("795634", n);
        Assert.Equal("G0050", g);
        Assert.Null(s);
    }

    // ── Caso 3 — flag ON, sin silo: mensaje explícito ────────────────────────────

    [Fact]
    public void Caso3_FlagOn_SinSilo_ExigeElSilo()
    {
        var error = ValidarUbicacion(ModoUbicacionInventario.PorSilo, siloId: null, galponId: "G0050", esAlimento: true);

        Assert.Equal(MensajeSiloRequerido, error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Caso3_FlagOn_SiloNoPositivo_SeRechaza(int siloId)
    {
        var error = ValidarUbicacion(ModoUbicacionInventario.PorSilo, siloId, galponId: null, esAlimento: true);

        Assert.Equal(MensajeSiloInvalido, error);
    }

    // ── Caso 4 — flag ON con silo y galpón: manda el silo, el galpón se anula ────

    [Fact]
    public void Caso4_FlagOn_ConSiloYGalpon_AnulaNucleoYGalpon()
    {
        Assert.Null(ValidarUbicacion(ModoUbicacionInventario.PorSilo, siloId: 4, galponId: "G0050", esAlimento: true));

        var (n, g, s) = NormalizarUbicacion(ModoUbicacionInventario.PorSilo, "795634", "G0050", siloId: 4);

        Assert.Null(n);
        Assert.Null(g);
        Assert.Equal(4, s);
    }

    [Fact]
    public void Caso4_FlagOn_ElMismoSiloEnDosGalpones_DaLaMismaClave()
    {
        // El saldo de un silo compartido es UNO: la ubicación resultante no puede depender del
        // galpón desde el que se cargó el movimiento (si dependiera, el silo tendría dos saldos).
        var desdeGalpon1 = NormalizarUbicacion(ModoUbicacionInventario.PorSilo, "N1", "G0001", siloId: 4);
        var desdeGalpon2 = NormalizarUbicacion(ModoUbicacionInventario.PorSilo, "N1", "G0002", siloId: 4);

        Assert.Equal(desdeGalpon1, desdeGalpon2);
    }

    // ── Caso 5 — flag ON, ítem que no es alimento: mismo trato ───────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Caso5_FlagOn_TodoItemExigeUbicacion(bool esAlimento)
    {
        Assert.Equal(
            MensajeSiloRequerido,
            ValidarUbicacion(ModoUbicacionInventario.PorSilo, siloId: null, galponId: null, esAlimento));

        Assert.Null(
            ValidarUbicacion(ModoUbicacionInventario.PorSilo, siloId: 12, galponId: null, esAlimento));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Caso5_FlagOff_ElTipoDeItemTampocoCambiaLaDecision(bool esAlimento)
    {
        Assert.Null(ValidarUbicacion(ModoUbicacionInventario.Clasico, siloId: null, galponId: null, esAlimento));
        Assert.Equal(
            MensajeSiloNoAplica,
            ValidarUbicacion(ModoUbicacionInventario.Clasico, siloId: 12, galponId: null, esAlimento));
    }

    // ── Red de seguridad: modo por silo mal invocado no inventa ubicación ────────

    [Fact]
    public void FlagOn_SinSilo_NormalizarNoInventaUbicacion()
    {
        // Sin silo, la normalización conserva la ubicación que entró en vez de anularla: anularla
        // dejaría el movimiento en una clave natural distinta de la que el usuario pidió.
        var (n, g, s) = NormalizarUbicacion(ModoUbicacionInventario.PorSilo, "795634", "G0050", siloId: null);

        Assert.Equal("795634", n);
        Assert.Equal("G0050", g);
        Assert.Null(s);
    }
}
