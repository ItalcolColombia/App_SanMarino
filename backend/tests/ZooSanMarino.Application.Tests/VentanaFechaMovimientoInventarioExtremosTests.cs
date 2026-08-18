using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Extremos que la pantalla ofrece para la fecha de un INGRESO (plan
/// `ventana_fecha_ingreso_alimento_previo_ui_plan.md`, T1-T7).
///
/// <para>
/// El conjunto admitido no es contiguo, así que <see cref="VentanaFechaMovimientoInventarioCalculos.ExtremosVentanaIngreso"/>
/// devuelve el rango ENVOLVENTE: la pantalla puede ofrecer de más —el controller rechaza el hueco con
/// <see cref="VentanaFechaMovimientoInventarioCalculos.EsFechaPermitidaConEncasetProximo"/>—, pero
/// nunca de menos, porque recortar el mínimo es justo lo que impedía tipear la fecha real del
/// alimento previo al encasetamiento.
/// </para>
/// </summary>
public class VentanaFechaMovimientoInventarioExtremosTests
{
    private static readonly DateTime Hoy = new(2026, 9, 7);
    private const int VentanaEmpresa = 10;   // companies.dias_alimento_previo_encaset (default)

    private static (DateTime Min, DateTime Max) Extremos(DateTime? encaset, int dias = VentanaEmpresa) =>
        VentanaFechaMovimientoInventarioCalculos.ExtremosVentanaIngreso(Hoy, encaset, dias);

    // ─── T1 · Sin encaset la regla vigente no se mueve ───────────────────────────

    [Fact]
    public void T1_SinEncaset_ExtremosDeLaReglaVigente()
    {
        var (min, max) = Extremos(null);
        Assert.Equal(new DateTime(2026, 9, 1), min);
        Assert.Equal(Hoy, max);
    }

    // ─── T2 · El caso que motiva todo: encaset a principio de mes ────────────────

    [Fact]
    public void T2_EncasetProximo_AbreElMesAnterior()
    {
        // Encaset el 09-sep con ventana de 10 días ⇒ el alimento pudo llegar desde el 30-ago.
        var (min, max) = Extremos(new DateTime(2026, 9, 9));
        Assert.Equal(new DateTime(2026, 8, 30), min);
        Assert.Equal(Hoy, max);
    }

    // ─── T3 · Nunca se achica el mínimo vigente ──────────────────────────────────

    [Fact]
    public void T3_VentanaDentroDelMes_NoMueveElMinimo()
    {
        // Encaset el 20-sep ⇒ desde el 10-sep, que ya está DENTRO del mes en curso.
        var (min, _) = Extremos(new DateTime(2026, 9, 20));
        Assert.Equal(new DateTime(2026, 9, 1), min);
    }

    // ─── T4 · Un encaset viejo no reabre meses por la puerta de atrás ────────────

    [Fact]
    public void T4_EncasetFueraDelPisoDe30Dias_NoAbreNada()
    {
        // Encaset el 01-ago: su ventana entera termina antes de hoy−30 (08-ago).
        var (min, _) = Extremos(new DateTime(2026, 8, 1));
        Assert.Equal(new DateTime(2026, 9, 1), min);
    }

    // ─── T5 · El piso de 30 días topa la apertura ────────────────────────────────

    [Fact]
    public void T5_VentanaMasViejaQueElPiso_SeTopaEnElPiso()
    {
        // Encaset el 12-ago con ventana de 30 días ⇒ desde el 13-jul, pero el piso es el 08-ago.
        var (min, _) = Extremos(new DateTime(2026, 8, 12), dias: 30);
        Assert.Equal(Hoy.AddDays(-VentanaFechaMovimientoInventarioCalculos.DiasMaximosRetroactividadEncaset), min);
    }

    // ─── T6 · Días negativos se normalizan a 0, como en el resto de la clase ─────

    [Fact]
    public void T6_DiasNegativos_SeNormalizanA0()
    {
        // Con dias=0 la ventana es el día del encaset solo: 31-ago, que es del mes anterior.
        var (min, _) = Extremos(new DateTime(2026, 8, 31), dias: -5);
        Assert.Equal(new DateTime(2026, 8, 31), min);
    }

    // ─── T7 · El futuro no lo abre nadie ─────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("2026-09-09")]
    [InlineData("2026-12-31")]
    public void T7_MaximoEsSiempreHoy(string? encasetIso)
    {
        var encaset = encasetIso is null ? (DateTime?)null : DateTime.Parse(encasetIso);
        var (_, max) = Extremos(encaset);
        Assert.Equal(Hoy, max);
    }

    // ─── Coherencia con la regla que manda ───────────────────────────────────────

    [Fact]
    public void ElMinimoOfrecidoEsSiempreUnaFechaQueElControllerPuedeAceptar()
    {
        var encaset = new DateTime(2026, 9, 9);
        var (min, _) = Extremos(encaset);
        Assert.True(VentanaFechaMovimientoInventarioCalculos.EsFechaPermitidaConEncasetProximo(
            min, Hoy, encaset, VentanaEmpresa));
    }

    [Fact]
    public void ElHuecoEntreLosDosTramosLoSigueRechazandoElController()
    {
        // Encaset el 28-ago con ventana de 10 días ⇒ admitido [18-ago, 28-ago] ∪ [01-sep, 07-sep],
        // o sea un hueco real del 29 al 31 de agosto DENTRO del rango envolvente [18-ago, 07-sep].
        var encaset = new DateTime(2026, 8, 28);
        var (min, max) = Extremos(encaset);
        Assert.Equal(new DateTime(2026, 8, 18), min);

        var enElHueco = new DateTime(2026, 8, 30);
        Assert.True(enElHueco > min && enElHueco < max, "el testigo tiene que caer dentro del envolvente");
        Assert.False(VentanaFechaMovimientoInventarioCalculos
            .EsFechaPermitidaConEncasetProximo(enElHueco, Hoy, encaset, VentanaEmpresa));
    }
}
