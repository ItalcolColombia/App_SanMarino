using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Las dos decisiones puras que sostienen la prevención de descuadres de alimento de engorde
/// (jul-2026): cómo se lee el invariante del cuadre y cuándo se avisa que un movimiento se está
/// fechando fuera del ciclo vigente del galpón.
/// </summary>
public class PrevencionDescuadresAlimentoTests
{
    // ─── Clasificación del cuadre ─────────────────────────────────────────────

    [Fact]
    public void Cuadre_SinDescuadreNiNegativos_EsOk()
        => Assert.Equal(EstadoCuadreAlimento.Ok,
                        CuadreAlimentoEngordeCalculos.Clasificar(0m, 0));

    [Theory]
    [InlineData(0.5)]
    [InlineData(-0.5)]
    [InlineData(1)]
    [InlineData(-1)]
    public void Cuadre_DentroDeTolerancia_EsOk(decimal descuadre)
        => Assert.Equal(EstadoCuadreAlimento.Ok,
                        CuadreAlimentoEngordeCalculos.Clasificar(descuadre, 0));

    [Theory]
    [InlineData(1.01)]
    [InlineData(-1.01)]
    [InlineData(480)]      // el menor descuadre real medido en produccion (CAROLINA G0058)
    [InlineData(-7960)]    // el caso testigo: Kilometro 22 / G0036
    [InlineData(37880)]    // el peor historico: Kilometro 22 / G0035
    public void Cuadre_FueraDeTolerancia_EsDescuadrado(decimal descuadre)
        => Assert.Equal(EstadoCuadreAlimento.Descuadrado,
                        CuadreAlimentoEngordeCalculos.Clasificar(descuadre, 0));

    [Fact]
    public void Cuadre_CuadraPeroConDiasNegativos_EsSaldoNegativo()
        => Assert.Equal(EstadoCuadreAlimento.SaldoNegativo,
                        CuadreAlimentoEngordeCalculos.Clasificar(0m, 3));

    [Fact]
    public void Cuadre_ElDescuadrePesaMasQueElNegativo()
    {
        // Un descuadre es un defecto; un negativo es informacion de la operacion.
        Assert.Equal(EstadoCuadreAlimento.Descuadrado,
                     CuadreAlimentoEngordeCalculos.Clasificar(5000m, 12));
    }

    [Fact]
    public void Cuadre_RequiereAtencion_SoloCuandoNoEsOk()
    {
        Assert.False(CuadreAlimentoEngordeCalculos.RequiereAtencion(0m, 0));
        Assert.True(CuadreAlimentoEngordeCalculos.RequiereAtencion(0m, 1));
        Assert.True(CuadreAlimentoEngordeCalculos.RequiereAtencion(2000m, 0));
    }

    [Fact]
    public void Cuadre_ElMensajeDistingueDeMasDeDeMenos()
    {
        Assert.Contains("de MÁS", CuadreAlimentoEngordeCalculos.Describir(7960m, 0));
        Assert.Contains("de MENOS", CuadreAlimentoEngordeCalculos.Describir(-7960m, 0));
        Assert.Contains("negativo", CuadreAlimentoEngordeCalculos.Describir(0m, 2));
        Assert.Contains("Cuadra", CuadreAlimentoEngordeCalculos.Describir(0m, 0));
    }

    // ─── Aviso de fecha fuera de ciclo ────────────────────────────────────────

    private const int DiasPrevios = 10;   // ItalcolEcuador

    /// <summary>Kilometro 22 / G0036 tal cual está en producción.</summary>
    private static readonly CicloGalpon[] G0036 =
    [
        new(19, "2601", new DateTime(2026, 2,  3), new DateTime(2026, 4,  4)),
        new(65, "2602", new DateTime(2026, 4, 17), new DateTime(2026, 6,  1)),
        new(98, "2603", new DateTime(2026, 6, 16), new DateTime(2026, 7, 28)),
    ];

    [Fact]
    public void Aviso_FechaDentroDelCicloVigente_NoAvisa()
        => Assert.Null(AvisoFechaFueraDeCicloCalculos.Evaluar(
            new DateTime(2026, 7, 10), G0036, DiasPrevios));

    [Fact]
    public void Aviso_FechaDeHoyPosteriorAlUltimoSeguimiento_NoAvisa()
    {
        // El caso corriente: registrar hoy el alimento que llegó hoy, con el seguimiento aún sin cargar.
        Assert.Null(AvisoFechaFueraDeCicloCalculos.Evaluar(
            new DateTime(2026, 8, 5), G0036, DiasPrevios));
    }

    [Fact]
    public void Aviso_FechaEnLaVentanaPreviaAlEncaset_NoAvisa()
    {
        // El preiniciador llega antes que los pollitos: es justamente lo que la ventana v9 permite.
        // Ciclo vigente arranca el 16/06 y la ventana son 10 días ⇒ desde el 06/06.
        Assert.Null(AvisoFechaFueraDeCicloCalculos.Evaluar(
            new DateTime(2026, 6, 8), G0036, DiasPrevios));
    }

    [Fact]
    public void Aviso_FechaDentroDeUnCicloYaCerrado_AvisaEIdentificaCual()
    {
        var aviso = AvisoFechaFueraDeCicloCalculos.Evaluar(
            new DateTime(2026, 5, 4), G0036, DiasPrevios);

        Assert.NotNull(aviso);
        Assert.Contains("2602", aviso);      // el ciclo al que pertenece la fecha
        Assert.Contains("2603", aviso);      // el ciclo vigente, para contraste
        Assert.Contains("01/06/2026", aviso); // cuándo cerró
    }

    [Fact]
    public void Aviso_FechaEnElHuecoEntreCiclos_AvisaQueNadieLoVaAReflejar()
    {
        // Entre el cierre del 2602 (01/06) y la ventana del 2603 (06/06) no hay ningún ciclo.
        // Es el patrón que dejó a Kilometro 86 / G0040 con 9.020 kg de déficit.
        var aviso = AvisoFechaFueraDeCicloCalculos.Evaluar(
            new DateTime(2026, 6, 3), G0036, DiasPrevios);

        Assert.NotNull(aviso);
        Assert.Contains("Ningún lote", aviso);
    }

    [Fact]
    public void Aviso_GalponSinLotesDeEngorde_NoAvisa()
        => Assert.Null(AvisoFechaFueraDeCicloCalculos.Evaluar(
            new DateTime(2026, 5, 4), [], DiasPrevios));

    [Fact]
    public void Aviso_UnSoloCiclo_LaVentanaPreviaSigueValiendo()
    {
        CicloGalpon[] unico = [new(43, "2602", new DateTime(2026, 6, 5), new DateTime(2026, 7, 21))];

        Assert.Null(AvisoFechaFueraDeCicloCalculos.Evaluar(new DateTime(2026, 5, 27), unico, DiasPrevios));
        Assert.NotNull(AvisoFechaFueraDeCicloCalculos.Evaluar(new DateTime(2026, 5, 20), unico, DiasPrevios));
    }

    [Fact]
    public void Aviso_SinVentanaPrevia_ElCorteEsElPrimerSeguimiento()
    {
        CicloGalpon[] unico = [new(43, "2602", new DateTime(2026, 6, 5), new DateTime(2026, 7, 21))];

        Assert.Null(AvisoFechaFueraDeCicloCalculos.Evaluar(new DateTime(2026, 6, 5), unico, 0));
        Assert.NotNull(AvisoFechaFueraDeCicloCalculos.Evaluar(new DateTime(2026, 6, 4), unico, 0));
    }

    // ─── El cuadre y la doble validación ──────────────────────────────────────
    // Ninguna fn del esquema mira `validado`, así que `fn_seguimiento_diario_engorde` ya descuenta el
    // consumo de un registro PENDIENTE mientras el inventario todavía no lo movió. La reserva ACTIVA
    // es exactamente ese movimiento pendiente: el stock comparable es `stock − reservado`.

    [Fact]
    public void UnRegistroPendiente_NoEsUnDescuadre()
    {
        // El galpón separó 80 kg sin validar: la fn los descontó, el inventario no. Crudo da -80.
        const decimal descuadreCrudo = -80m, reservado = 80m;

        var ajustado = CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas(descuadreCrudo, reservado);

        Assert.Equal(0m, ajustado);
        Assert.Equal(EstadoCuadreAlimento.Ok, CuadreAlimentoEngordeCalculos.Clasificar(ajustado, 0));
        // Sin el ajuste, el mismo galpón se reportaba como defecto.
        Assert.Equal(EstadoCuadreAlimento.Descuadrado,
            CuadreAlimentoEngordeCalculos.Clasificar(descuadreCrudo, 0));
    }

    [Fact]
    public void FlagApagado_ElDescuadreEsExactamenteElDeAntes()
    {
        // Sin doble validación no hay reservas activas ⇒ reservado = 0 ⇒ el número no se mueve.
        foreach (var crudo in new[] { 0m, -480m, 37_880m, 0.4m })
            Assert.Equal(crudo, CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas(crudo, 0m));
    }

    [Fact]
    public void UnDescuadreREAL_SigueSiendoDescuadreAunqueHayaReservas()
    {
        // 80 kg separados y además 480 kg que faltan de verdad: el ajuste no puede taparlos.
        var ajustado = CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas(-560m, 80m);

        Assert.Equal(-480m, ajustado);
        Assert.Equal(EstadoCuadreAlimento.Descuadrado, CuadreAlimentoEngordeCalculos.Clasificar(ajustado, 0));
    }
}
