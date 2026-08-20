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
///
/// <para>
/// 🔑 20-ago-2026: el mínimo de la regla vigente (sin D4) ya no es el 1 del mes: es
/// <c>MIN(1 del mes, hoy − 15)</c> (<see cref="VentanaFechaRegistroCalculos"/>). Con
/// <c>Hoy = 07/09/2026</c> eso es <c>23/08/2026</c>. Los casos que quieren seguir demostrando que D4
/// EXTIENDE el mínimo (no solo lo iguala) usan encasetamientos anteriores al 23/08, donde la base
/// ampliada todavía no llega.
/// </para>
/// </summary>
public class VentanaFechaMovimientoInventarioExtremosTests
{
    private static readonly DateTime Hoy = new(2026, 9, 7);
    private const int VentanaEmpresa = 10;   // companies.dias_alimento_previo_encaset (default)

    private static (DateTime? Min, DateTime Max) Extremos(DateTime? encaset, int dias = VentanaEmpresa) =>
        VentanaFechaMovimientoInventarioCalculos.ExtremosVentanaIngreso(Hoy, encaset, dias);

    // ─── T1 · Sin encaset, la regla vigente (base ampliada) es la que manda ─────

    [Fact]
    public void T1_SinEncaset_ExtremosDeLaReglaVigente()
    {
        var (min, max) = Extremos(null);
        Assert.Equal(new DateTime(2026, 8, 23), min);   // piso rodante: hoy − 15
        Assert.Equal(Hoy, max);
    }

    // ─── T2 · El caso que motiva todo: D4 extiende más allá del piso rodante ─────

    [Fact]
    public void T2_EncasetProximo_AbreMasAllaDelPisoRodante()
    {
        // Encaset el 15-ago con ventana de 10 días ⇒ el alimento pudo llegar desde el 05-ago, que
        // el piso rodante (23-ago) no alcanza. El tope de 30 días (08-ago) topa la apertura.
        var (min, max) = Extremos(new DateTime(2026, 8, 15));
        Assert.Equal(new DateTime(2026, 8, 8), min);
        Assert.Equal(Hoy, max);
    }

    // ─── T3 · Nunca se achica el mínimo vigente ──────────────────────────────────

    [Fact]
    public void T3_VentanaDentroDelMes_NoMueveElMinimo()
    {
        // Encaset el 20-sep ⇒ desde el 10-sep, que es POSTERIOR a hoy: D4 no aporta nada y el mínimo
        // que queda es el de la base ampliada (23-ago), no el 1 del mes.
        var (min, _) = Extremos(new DateTime(2026, 9, 20));
        Assert.Equal(new DateTime(2026, 8, 23), min);
    }

    // ─── T4 · Un encaset viejo no reabre meses por la puerta de atrás ────────────

    [Fact]
    public void T4_EncasetFueraDelPisoDe30Dias_NoAbreNada()
    {
        // Encaset el 01-ago: su ventana entera termina antes de hoy−30 (08-ago). D4 no aporta nada;
        // el mínimo es el de la base ampliada.
        var (min, _) = Extremos(new DateTime(2026, 8, 1));
        Assert.Equal(new DateTime(2026, 8, 23), min);
    }

    // ─── T5 · El piso de 30 días topa la apertura ────────────────────────────────

    [Fact]
    public void T5_VentanaMasViejaQueElPiso_SeTopaEnElPiso()
    {
        // Encaset el 12-ago con ventana de 30 días ⇒ desde el 13-jul, pero el piso es el 08-ago.
        // El piso de 30 días (08-ago) es más retroactivo que el de la base (23-ago) en este caso, así
        // que el resultado es el mismo con o sin la ampliación.
        var (min, _) = Extremos(new DateTime(2026, 8, 12), dias: 30);
        Assert.Equal(Hoy.AddDays(-VentanaFechaMovimientoInventarioCalculos.DiasMaximosRetroactividadEncaset), min);
    }

    // ─── T6 · Días negativos se normalizan a 0, como en el resto de la clase ─────

    [Fact]
    public void T6_DiasNegativos_SeNormalizanA0()
    {
        // Con dias=0 la ventana es el día del encaset solo: 18-ago, anterior al piso rodante (23-ago)
        // ⇒ D4 extiende el mínimo hasta ahí.
        var (min, _) = Extremos(new DateTime(2026, 8, 18), dias: -5);
        Assert.Equal(new DateTime(2026, 8, 18), min);
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

    // ─── Con el permiso de fecha retroactiva no hay piso ─────────────────────────

    [Fact]
    public void ConPermisoRetroactivo_NoHayMinimo()
    {
        var (min, max) = VentanaFechaMovimientoInventarioCalculos.ExtremosVentanaIngreso(
            Hoy, proximoEncasetEnGalpon: null, VentanaEmpresa, puedeRetroactivar: true);

        Assert.Null(min);
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
        // Encaset el 10-ago con ventana de 10 días ⇒ admitido [31-jul, 10-ago] ∪ [23-ago, 07-sep]
        // (la base ampliada), con un hueco real del 11 al 22 de agosto DENTRO del rango envolvente
        // [08-ago, 07-sep] (el 08-ago sale del tope de 30 días, no del propio encaset).
        var encaset = new DateTime(2026, 8, 10);
        var (min, max) = Extremos(encaset);
        Assert.Equal(new DateTime(2026, 8, 8), min);

        var enElHueco = new DateTime(2026, 8, 15);
        Assert.True(enElHueco > min && enElHueco < max, "el testigo tiene que caer dentro del envolvente");
        Assert.False(VentanaFechaMovimientoInventarioCalculos
            .EsFechaPermitidaConEncasetProximo(enElHueco, Hoy, encaset, VentanaEmpresa));
    }
}
