using ZooSanMarino.Application.Calculos;
using C = ZooSanMarino.Application.Calculos.VacunacionPlantillaCalculos.Candidata;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la resolución de plantilla de vacunación.
///
/// <para>
/// Lo que estos tests fijan no es "que elija bien" sino que elija <b>siempre lo mismo</b>. Sin una
/// regla total, cuál plantilla gana depende del orden en que la base devuelva las filas, y el
/// síntoma sería dos lotes iguales con cronogramas distintos sin que nadie pueda explicar por qué.
/// Por eso varios tests corren la MISMA entrada en dos órdenes y exigen el mismo resultado.
/// </para>
/// </summary>
public class VacunacionPlantillaCalculosTests
{
    private static readonly DateOnly Encaset = new(2026, 6, 15);

    // ─── Especificidad: la raza le gana al comodín ────────────────────────────

    [Fact]
    public void LaRazaExacta_LeGanaAlComodin()
    {
        var comodin  = new C(1, "Levante", null, null, true);
        var especial = new C(2, "Levante", "Ross 308", null, true);

        Assert.Equal(2, Resolver(new[] { comodin, especial })!.Value.Id);
        // Y al revés: el orden de entrada no puede cambiar el resultado.
        Assert.Equal(2, Resolver(new[] { especial, comodin })!.Value.Id);
    }

    [Fact]
    public void SinPlantillaDeSuRaza_CaeAlComodin()
    {
        var comodin = new C(1, "Levante", null, null, true);
        var deOtra  = new C(2, "Levante", "Lohmann", null, true);

        Assert.Equal(1, Resolver(new[] { comodin, deOtra })!.Value.Id);
    }

    [Fact]
    public void UnLoteSinRaza_NoPuedeTomarUnaPlantillaDeRaza()
    {
        // Adivinar seria inventarle un plan sanitario, que es peor que no tener plan: se ve igual de
        // correcto en pantalla.
        var deRaza = new C(1, "Levante", "Ross 308", null, true);

        Assert.Null(VacunacionPlantillaCalculos.ResolverEfectiva(
            new[] { deRaza }, "Levante", raza: null, fechaEncaset: Encaset));
    }

    // ─── Vigencia ─────────────────────────────────────────────────────────────

    [Fact]
    public void AIgualEspecificidad_GanaLaVigenciaMasReciente()
    {
        var vieja = new C(1, "Levante", null, new DateOnly(2026, 1, 1), true);
        var nueva = new C(2, "Levante", null, new DateOnly(2026, 5, 1), true);

        Assert.Equal(2, Resolver(new[] { vieja, nueva })!.Value.Id);
        Assert.Equal(2, Resolver(new[] { nueva, vieja })!.Value.Id);
    }

    [Fact]
    public void UnaPlantillaQueEmpiezaDESPUES_DelEncaset_NoAplica()
    {
        // El lote se encaseto el 15/06 con el plan de entonces; cambiar el plan en julio no puede
        // reescribirle el cronograma hacia atras.
        var futura = new C(1, "Levante", null, new DateOnly(2026, 7, 1), true);

        Assert.Null(Resolver(new[] { futura }));
    }

    [Fact]
    public void LaVigenciaQueEMPIEZA_ElMismoDiaDelEncaset_SI_Aplica()
    {
        var mismaFecha = new C(1, "Levante", null, Encaset, true);

        Assert.Equal(1, Resolver(new[] { mismaFecha })!.Value.Id);
    }

    [Fact]
    public void UnLoteSinFechaDeEncaset_SoloTomaPlantillasSinVigencia()
    {
        var conVigencia = new C(1, "Levante", null, new DateOnly(2026, 1, 1), true);
        var sinVigencia = new C(2, "Levante", null, null, true);

        Assert.Equal(2, VacunacionPlantillaCalculos.ResolverEfectiva(
            new[] { conVigencia, sinVigencia }, "Levante", null, fechaEncaset: null)!.Value.Id);

        Assert.Null(VacunacionPlantillaCalculos.ResolverEfectiva(
            new[] { conVigencia }, "Levante", null, fechaEncaset: null));
    }

    [Fact]
    public void LaEspecificidadPESA_MAS_QueLaVigencia()
    {
        // Una comodin recien versionada no le gana a la de la raza del lote: primero se mira a quien
        // apunta la plantilla, despues desde cuando rige.
        var comodinNueva  = new C(1, "Levante", null, new DateOnly(2026, 6, 1), true);
        var razaAntigua   = new C(2, "Levante", "Ross 308", new DateOnly(2026, 1, 1), true);

        Assert.Equal(2, Resolver(new[] { comodinNueva, razaAntigua })!.Value.Id);
    }

    // ─── Totalidad y determinismo ─────────────────────────────────────────────

    [Fact]
    public void EmpateTotal_DesempataElIdMayor_YEsEstable()
    {
        var a = new C(7, "Levante", "Ross 308", new DateOnly(2026, 1, 1), true);
        var b = new C(9, "Levante", "Ross 308", new DateOnly(2026, 1, 1), true);

        Assert.Equal(9, Resolver(new[] { a, b })!.Value.Id);
        Assert.Equal(9, Resolver(new[] { b, a })!.Value.Id);
    }

    [Fact]
    public void UnaPlantillaAPAGADA_NoCompite()
    {
        var apagadaEspecifica = new C(1, "Levante", "Ross 308", null, Activa: false);
        var activaComodin     = new C(2, "Levante", null, null, true);

        Assert.Equal(2, Resolver(new[] { apagadaEspecifica, activaComodin })!.Value.Id);
    }

    [Fact]
    public void UnaPlantillaDeOtraLinea_NoAplica()
    {
        var deEngorde = new C(1, "Engorde", null, null, true);

        Assert.Null(Resolver(new[] { deEngorde }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SinLineaProductiva_DevuelveNull(string? linea)
    {
        var alguna = new C(1, "Levante", null, null, true);

        Assert.Null(VacunacionPlantillaCalculos.ResolverEfectiva(new[] { alguna }, linea, null, Encaset));
    }

    [Fact]
    public void SinCandidatas_DevuelveNull_YEsoSignificaSinCronogramaAutomatico()
    {
        Assert.Null(Resolver(Array.Empty<C>()));
        Assert.Null(VacunacionPlantillaCalculos.ResolverEfectiva(null!, "Levante", null, Encaset));
    }

    [Fact]
    public void LaRazaSeComparaSinDistinguirMayusculasNiEspacios()
    {
        var plantilla = new C(1, "levante", "  ross 308 ", null, true);

        Assert.Equal(1, VacunacionPlantillaCalculos.ResolverEfectiva(
            new[] { plantilla }, "Levante", "Ross 308", Encaset)!.Value.Id);
    }

    [Fact]
    public void UnaRazaVACIA_EnLaPlantilla_EsComodin_NoUnaRazaLlamadaVacio()
    {
        var conEspacios = new C(1, "Levante", "   ", null, true);

        Assert.Equal(1, Resolver(new[] { conEspacios })!.Value.Id);
    }

    // ─── Unidad por línea ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("Engorde", "Dia")]
    [InlineData("engorde", "Dia")]
    [InlineData("Levante", "Semana")]
    [InlineData("Produccion", "Semana")]
    [InlineData(null, "Semana")]
    public void LaUnidadPorDefecto_DependeDeLaLinea(string? linea, string esperada)
    {
        // Un ciclo de engorde entero dura menos que 7 semanas: una franja semanal no distinguiria nada.
        Assert.Equal(esperada, VacunacionPlantillaCalculos.UnidadPorDefecto(linea));
    }

    // ─── Validación de ítems ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Semana")]
    [InlineData("Dia")]
    public void UnItemBienFormado_NoTieneMotivoDeRechazo(string unidad)
    {
        Assert.Null(VacunacionPlantillaCalculos.MotivoItemInvalido(unidad, 5, 3, 3));
    }

    [Fact]
    public void LaUnidadFECHA_SeRechazaEnUnaPlantilla()
    {
        var motivo = VacunacionPlantillaCalculos.MotivoItemInvalido("Fecha", 5, 0, 0);

        Assert.NotNull(motivo);
        Assert.Contains("fecha fija", motivo);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(5, -1, 0)]
    [InlineData(5, 0, -1)]
    public void LosNegativosSeRechazan(int valor, int antes, int despues)
    {
        Assert.NotNull(VacunacionPlantillaCalculos.MotivoItemInvalido("Semana", valor, antes, despues));
    }

    private static VacunacionPlantillaCalculos.Candidata? Resolver(IEnumerable<C> candidatas) =>
        VacunacionPlantillaCalculos.ResolverEfectiva(candidatas, "Levante", "Ross 308", Encaset);
}
