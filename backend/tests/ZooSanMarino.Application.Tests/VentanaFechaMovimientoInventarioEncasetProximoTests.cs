using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// D4 — excepción a la ventana de mes en curso para el alimento que llega ANTES del encasetamiento.
///
/// <para>
/// El alimento llega a la granja 2-7 días antes que los pollitos y contabilidad necesita la fecha
/// REAL de llegada. Con un encaset a principio de mes esa fecha cae en el mes anterior, y la regla
/// del mes en curso (<see cref="VentanaFechaMovimientoInventarioCalculos.EsFechaPermitida"/>) la
/// rechazaba, empujando de vuelta al workaround de fechar el ingreso el primer día de consumo.
/// </para>
///
/// <para>
/// Lo que se prueba acá es que la excepción abre <b>solo</b> la ventana de alimento previo de un
/// encasetamiento real del galpón (nunca el mes anterior entero, nunca el futuro) y que sin encaset
/// el comportamiento es byte a byte el de la regla previa.
/// </para>
///
/// <para>
/// 🔑 20-ago-2026: la ventana BASE (sin D4) se amplió a <c>MIN(1 del mes, hoy − 15)</c>
/// (<see cref="VentanaFechaRegistroCalculos"/>). Con <c>Hoy = 07/08/2026</c> eso es <c>23/07/2026</c>.
/// Varios de los casos que antes solo la excepción D4 admitía ahora los admite la base sola —es la
/// ampliación funcionando— así que los escenarios que quieren seguir probando el LÍMITE propio de D4
/// (no el de la base) se corrieron a fechas anteriores al 23/07, donde la base por sí sola todavía
/// rechaza y la única vía posible es la excepción.
/// </para>
/// </summary>
public class VentanaFechaMovimientoInventarioEncasetProximoTests
{
    private static readonly DateTime Hoy = new(2026, 8, 7);
    private const int VentanaEmpresa = 10;   // companies.dias_alimento_previo_encaset (default)

    private static bool Permitida(DateTime? fecha, DateTime? encaset, int dias = VentanaEmpresa) =>
        VentanaFechaMovimientoInventarioCalculos.EsFechaPermitidaConEncasetProximo(fecha, Hoy, encaset, dias);

    // ─── La regla vigente no se toca: lo que ya pasaba, sigue pasando ────────────

    [Fact]
    public void MesActual_Permitido_SinEncaset()
        => Assert.True(Permitida(new DateTime(2026, 8, 3), null));

    [Fact]
    public void MesActual_Permitido_ConEncaset()
        => Assert.True(Permitida(new DateTime(2026, 8, 3), new DateTime(2026, 8, 10)));

    [Fact]
    public void Hoy_Permitido()
        => Assert.True(Permitida(Hoy, null));

    [Fact]
    public void SinFecha_Permitido()
        => Assert.True(Permitida(null, null));

    [Fact]
    public void SinFecha_PermitidoAunqueHayaEncaset()
        => Assert.True(Permitida(null, new DateTime(2026, 8, 10)));

    // ─── Últimos 15 días: ahora la BASE los admite, sin necesitar D4 ─────────────

    [Fact]
    public void UltimosQuinceDias_SinEncaset_YaLoPermiteLaBase()
    {
        // 31/07 está a 7 días de Hoy (07/08): dentro del piso rodante (23/07) de la ventana base
        // ampliada. Ya no hace falta ningún encaset que lo justifique — es la ampliación pedida por
        // el usuario, no la excepción D4.
        Assert.True(Permitida(new DateTime(2026, 7, 31), null));
    }

    // ─── Sin encaset que la justifique, más allá de los 15 días sigue cerrado ───

    [Fact]
    public void MasAllaDeQuinceDias_SinEncaset_Rechazado()
        // 20/07: 18 días atrás, fuera del piso rodante (23/07) y sin encaset que abra una excepción.
        => Assert.False(Permitida(new DateTime(2026, 7, 20), null));

    [Fact]
    public void MesAnteriorCompleto_SinEncaset_Rechazado()
        => Assert.False(Permitida(new DateTime(2026, 7, 15), null));

    // ─── El caso del usuario: llega el 31/07, encaseta el 07/08 ────────────────

    [Fact]
    public void MesAnterior_ConEncasetASieteDias_Permitido()
    {
        // Alimento recibido el 31/07 para el lote que se encaseta el 07/08: 7 días antes, dentro de
        // la ventana de 10 que la empresa ya tenía configurada. (Con la base ampliada, 31/07 ya lo
        // admite la regla vigente sola; el resultado es el mismo, la excepción es redundante acá y
        // eso está bien.)
        Assert.True(Permitida(new DateTime(2026, 7, 31), new DateTime(2026, 8, 7)));
    }

    [Fact]
    public void MesAnterior_CruceDeMes_ConEncasetAPrincipioDeMes_Permitido()
    {
        // Llega el 29/07, encaset el 01/08. Es el escenario que motivó la excepción originalmente.
        Assert.True(Permitida(new DateTime(2026, 7, 29), new DateTime(2026, 8, 1)));
    }

    [Fact]
    public void BordeExactoDeLaVentanaDeLaEmpresa_MasAllaDelPisoRodante_Permitido()
    {
        // encaset 20/07 − 10 días = 10/07: exactamente en el borde de la ventana propia de D4, y
        // fuera del piso rodante (23/07) — así que solo la excepción lo admite, no la base.
        Assert.True(Permitida(new DateTime(2026, 7, 10), new DateTime(2026, 7, 20)));
    }

    [Fact]
    public void UnDiaAntesDeLaVentanaDeLaEmpresa_MasAllaDelPisoRodante_Rechazado()
        // Un día antes del borde de arriba (09/07): ni la base (fuera del piso rodante) ni D4 (fuera
        // de [10/07, 20/07]) lo admiten.
        => Assert.False(Permitida(new DateTime(2026, 7, 9), new DateTime(2026, 7, 20)));

    [Fact]
    public void PosteriorAlEncaset_YFueraDeTodaVentana_Rechazado()
    {
        // La ventana es [encaset − N, encaset]: un ingreso del 15/07 con encaset el 10/07 ya no es
        // "alimento previo", es un movimiento posterior fechado fuera de toda ventana (base y D4).
        Assert.False(Permitida(new DateTime(2026, 7, 15), new DateTime(2026, 7, 10)));
    }

    // ─── Topes duros: futuro y 30 días ──────────────────────────────────────────

    [Fact]
    public void Futuro_Rechazado_AunConEncasetProximo()
    {
        // Un encaset del 10/08 no habilita fechar el ingreso el 09/08: el alimento todavía no llegó.
        Assert.False(Permitida(new DateTime(2026, 8, 9), new DateTime(2026, 8, 10)));
    }

    [Fact]
    public void Futuro_Rechazado_SinEncaset()
        => Assert.False(Permitida(new DateTime(2026, 8, 8), null));

    [Fact]
    public void MasDeTreintaDiasAtras_Rechazado_AunDentroDeLaVentanaDelEncaset()
    {
        // 05/07 está a 33 días de hoy. Aunque el galpón encasete el 10/07 y la fecha caiga en su
        // ventana, el tope de 30 días manda: sin él un encaset viejo reabriría meses enteros.
        Assert.False(Permitida(new DateTime(2026, 7, 5), new DateTime(2026, 7, 10)));
    }

    [Fact]
    public void ExactamenteTreintaDiasAtras_ConEncasetEnVentana_Permitido()
    {
        // 08/07 = hoy − 30 (inclusive), con encaset el 12/07 → dentro de [02/07, 12/07].
        Assert.True(Permitida(new DateTime(2026, 7, 8), new DateTime(2026, 7, 12)));
    }

    // ─── Normalización de la ventana de la empresa ─────────────────────────────

    [Fact]
    public void VentanaEmpresaCero_SoloElDiaDelEncaset()
    {
        // Corrido a julio (fuera del piso rodante de 23/07) para que la base no ensombrezca el caso.
        var encaset = new DateTime(2026, 7, 10);
        Assert.True(Permitida(new DateTime(2026, 7, 10), encaset, dias: 0));
        Assert.False(Permitida(new DateTime(2026, 7, 9), encaset, dias: 0));
    }

    [Fact]
    public void VentanaEmpresaNegativa_SeNormalizaACero()
    {
        // Mismo criterio que AvisoFechaFueraDeCicloCalculos: Math.Max(0, dias).
        var encaset = new DateTime(2026, 7, 10);
        Assert.True(Permitida(new DateTime(2026, 7, 10), encaset, dias: -5));
        Assert.False(Permitida(new DateTime(2026, 7, 9), encaset, dias: -5));
    }

    [Fact]
    public void LaHoraNoCuenta_SoloElDia()
    {
        Assert.True(Permitida(new DateTime(2026, 7, 31, 23, 59, 59), new DateTime(2026, 8, 7, 12, 0, 0)));
        Assert.True(Permitida(new DateTime(2026, 7, 28, 0, 0, 0), new DateTime(2026, 8, 7, 12, 0, 0)));
    }

    // ─── Con el permiso de fecha retroactiva, D4 ni se llega a evaluar ──────────

    [Fact]
    public void ConPermisoRetroactivo_NoHaceFaltaEncaset()
    {
        var permitida = VentanaFechaMovimientoInventarioCalculos.EsFechaPermitidaConEncasetProximo(
            new DateTime(2024, 1, 1), Hoy, proximoEncasetEnGalpon: null, VentanaEmpresa,
            puedeRetroactivar: true);
        Assert.True(permitida);
    }

    // ─── Mensajes ───────────────────────────────────────────────────────────────

    [Fact]
    public void MensajeSinEncaset_ExplicaQueNoHayEncasetQueLoJustifique()
    {
        var msg = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentanaConEncaset(Hoy, null, VentanaEmpresa);

        Assert.Contains("23/07/2026", msg);           // piso rodante, no el 1 del mes
        Assert.Contains("07/08/2026", msg);
        Assert.Contains("encasetamiento", msg);
    }

    [Fact]
    public void MensajeConEncaset_NombraLosDosExtremosDeLaVentanaDelEncaset()
    {
        var msg = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentanaConEncaset(
            Hoy, new DateTime(2026, 7, 20), VentanaEmpresa);

        Assert.Contains("10/07/2026", msg);          // encaset − 10
        Assert.Contains("20/07/2026", msg);          // encaset
        Assert.Contains("30", msg);                  // tope duro
    }

    [Fact]
    public void ElMensajeConEncasetEsDistintoDelBasico()
    {
        var basico = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentana(Hoy);
        var conEncaset = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentanaConEncaset(
            Hoy, new DateTime(2026, 7, 20), VentanaEmpresa);

        Assert.NotEqual(basico, conEncaset);
        Assert.StartsWith(basico, conEncaset);
    }

    [Fact]
    public void ConPermisoRetroactivo_ElMensajeEsElBasico_SinHablarDeEncaset()
    {
        var basico = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentana(Hoy, puedeRetroactivar: true);
        var conEncaset = VentanaFechaMovimientoInventarioCalculos.MensajeFueraDeVentanaConEncaset(
            Hoy, new DateTime(2026, 7, 20), VentanaEmpresa, puedeRetroactivar: true);

        Assert.Equal(basico, conEncaset);
        Assert.DoesNotContain("encasetamiento", conEncaset);
    }
}
