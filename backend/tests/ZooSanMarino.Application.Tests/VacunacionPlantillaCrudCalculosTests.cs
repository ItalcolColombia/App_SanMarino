using ZooSanMarino.Application.Calculos;
using P = ZooSanMarino.Application.Calculos.VacunacionPlantillaCalculos.PlantillaExistente;
using I = ZooSanMarino.Application.Calculos.VacunacionPlantillaCalculos.ItemExistente;
using C = ZooSanMarino.Application.Calculos.VacunacionPlantillaCalculos.Candidata;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Reglas del CRUD de plantillas (W1.3): unicidad, carga doble, unidad por línea y el motivo con que
/// se explica la resolución.
///
/// <para>
/// Ninguna de estas reglas afecta al <i>resultado</i> de <c>ResolverEfectiva</c> —esa función es
/// total y siempre elige—. Lo que protegen es la capacidad de <b>explicar</b> el plan: dos plantillas
/// idénticas, o la misma vacuna dos veces en la misma semana, dejan una pantalla donde nadie puede
/// decir cuál manda ni por qué.
/// </para>
/// </summary>
public class VacunacionPlantillaCrudCalculosTests
{
    private static readonly DateOnly Jul = new(2026, 7, 1);
    private static readonly DateOnly Ago = new(2026, 8, 1);

    // ─── Unicidad de la plantilla ─────────────────────────────────────────────

    [Fact]
    public void MismaLineaRazaYVigencia_EsDuplicada()
    {
        var existentes = new[] { new P(1, "Plan Ross levante", "Levante", "Ross 308", Jul) };

        var motivo = VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", "Ross 308", Jul);

        Assert.NotNull(motivo);
        // El mensaje tiene que NOMBRAR a la que ya existe: sin eso el usuario no sabe cuál editar.
        Assert.Contains("Plan Ross levante", motivo);
    }

    [Fact]
    public void DistintaRaza_DistintaVigencia_ODistintaLinea_NoEsDuplicada()
    {
        var existentes = new[] { new P(1, "Plan Ross levante", "Levante", "Ross 308", Jul) };

        Assert.Null(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", "Lohmann", Jul));
        Assert.Null(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", "Ross 308", Ago));
        Assert.Null(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Produccion", "Ross 308", Jul));
    }

    [Fact]
    public void AlEditarse_LaPlantillaNoEsDuplicadaDeSiMisma()
    {
        var existentes = new[] { new P(7, "Plan Ross levante", "Levante", "Ross 308", Jul) };

        Assert.Null(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", "Ross 308", Jul, idEditando: 7));
        // Pero otra distinta con la misma tupla sí choca.
        Assert.NotNull(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", "Ross 308", Jul, idEditando: 9));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RazaVaciaEsElMismoComodinQueNull(string? razaExistente)
    {
        var existentes = new[] { new P(1, "General levante", "Levante", razaExistente, null) };

        // Vacío, espacios y null son el mismo comodín: si no, se colarían dos "generales de la línea".
        Assert.NotNull(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", null, null));
        Assert.NotNull(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", "  ", null));
    }

    [Fact]
    public void SinVigencia_ChocaSoloConLaOtraSinVigencia()
    {
        var existentes = new[] { new P(1, "General", "Levante", null, null) };

        Assert.NotNull(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", null, null));
        Assert.Null(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(existentes, "Levante", null, Jul));
    }

    [Fact]
    public void SinPlantillasPrevias_NuncaHayDuplicado()
    {
        Assert.Null(VacunacionPlantillaCalculos.MotivoPlantillaDuplicada(Array.Empty<P>(), "Levante", "Ross 308", Jul));
    }

    // ─── Carga doble del mismo ítem ───────────────────────────────────────────

    [Fact]
    public void MismaVacunaEnElMismoObjetivo_EsCargaDoble()
    {
        var existentes = new[] { new I(1, ItemInventarioId: 50, "Semana", 3) };

        var motivo = VacunacionPlantillaCalculos.MotivoItemDuplicado(existentes, 50, "Semana", 3);

        Assert.NotNull(motivo);
        Assert.Contains("semana 3", motivo);
    }

    [Fact]
    public void MismaVacunaEnOtroMomento_EsUnRefuerzoValido()
    {
        var existentes = new[] { new I(1, ItemInventarioId: 50, "Semana", 3) };

        Assert.Null(VacunacionPlantillaCalculos.MotivoItemDuplicado(existentes, 50, "Semana", 8));
        // Y otra vacuna en la misma semana tampoco choca: se aplican varias el mismo día.
        Assert.Null(VacunacionPlantillaCalculos.MotivoItemDuplicado(existentes, 51, "Semana", 3));
    }

    [Fact]
    public void ElItemQueSeEdita_NoChocaConsigoMismo()
    {
        var existentes = new[] { new I(4, ItemInventarioId: 50, "Semana", 3) };

        Assert.Null(VacunacionPlantillaCalculos.MotivoItemDuplicado(existentes, 50, "Semana", 3, idEditando: 4));
        Assert.NotNull(VacunacionPlantillaCalculos.MotivoItemDuplicado(existentes, 50, "Semana", 3, idEditando: 9));
    }

    [Fact]
    public void ElMensajeDeCargaDobleHablaDeDiasEnEngorde()
    {
        var existentes = new[] { new I(1, ItemInventarioId: 50, "Dia", 12) };

        var motivo = VacunacionPlantillaCalculos.MotivoItemDuplicado(existentes, 50, "Dia", 12);

        Assert.NotNull(motivo);
        Assert.Contains("día 12", motivo);
    }

    // ─── Unidad por línea ─────────────────────────────────────────────────────

    [Fact]
    public void EngordeProgramadoPorSemana_SeRechaza()
    {
        var motivo = VacunacionPlantillaCalculos.MotivoUnidadNoCorrespondeALinea("Engorde", "Semana");

        Assert.NotNull(motivo);
        Assert.Contains("día de edad", motivo);
    }

    [Theory]
    [InlineData("Engorde", "Dia")]
    [InlineData("Levante", "Semana")]
    [InlineData("Produccion", "Semana")]
    // Postura por día se PERMITE: la semana es la unidad cómoda, no la única correcta.
    [InlineData("Levante", "Dia")]
    [InlineData("Produccion", "Dia")]
    public void LasCombinacionesValidasPasan(string linea, string unidad)
    {
        Assert.Null(VacunacionPlantillaCalculos.MotivoUnidadNoCorrespondeALinea(linea, unidad));
    }

    // ─── El "por qué" de la resolución ────────────────────────────────────────

    [Fact]
    public void ElegidaPorRaza_LoDice()
    {
        var candidatas = new[] { new C(1, "Levante", null, null, true), new C(2, "Levante", "Ross 308", null, true) };

        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, "Levante", "Ross 308", new DateOnly(2026, 6, 1), idElegida: 2, nombreElegida: "Plan Ross");

        Assert.Contains("Plan Ross", motivo);
        Assert.Contains("Ross 308", motivo);
        Assert.Contains("Le ganó a otras 1", motivo);
    }

    [Fact]
    public void ElegidoElComodin_AclaraQueNoHabiaUnaDeSuRaza()
    {
        var candidatas = new[] { new C(1, "Levante", null, null, true) };

        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, "Levante", "Lohmann", new DateOnly(2026, 6, 1), idElegida: 1, nombreElegida: "Plan general");

        Assert.Contains("general de la línea", motivo);
        Assert.Contains("Lohmann", motivo);
        // Sin competencia no inventa una.
        Assert.DoesNotContain("Le ganó", motivo);
    }

    [Fact]
    public void SinPlantillasDeLaLinea_LoDiceAsi()
    {
        var candidatas = new[] { new C(1, "Engorde", null, null, true) };

        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, "Levante", "Ross 308", new DateOnly(2026, 6, 1), idElegida: null, nombreElegida: null);

        Assert.Contains("no tiene plantillas activas", motivo);
        Assert.Contains("Levante", motivo);
    }

    [Fact]
    public void LoteSinRazaConTodasEspecificas_ExplicaQueFaltaLaRazaDelLote()
    {
        var candidatas = new[] { new C(1, "Levante", "Ross 308", null, true), new C(2, "Levante", "Lohmann", null, true) };

        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, "Levante", null, new DateOnly(2026, 6, 1), idElegida: null, nombreElegida: null);

        Assert.Contains("no tiene raza cargada", motivo);
    }

    [Fact]
    public void RazaSinPlantilla_ListaLasRazasQueSiTienen()
    {
        var candidatas = new[] { new C(1, "Levante", "Ross 308", null, true), new C(2, "Levante", "Lohmann", null, true) };

        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, "Levante", "Cobb", new DateOnly(2026, 6, 1), idElegida: null, nombreElegida: null);

        Assert.Contains("Cobb", motivo);
        Assert.Contains("Ross 308", motivo);
        Assert.Contains("Lohmann", motivo);
    }

    [Fact]
    public void LoteSinEncasetYPlantillasConVigencia_LoAtribuyeALaFechaFaltante()
    {
        var candidatas = new[] { new C(1, "Levante", null, Jul, true) };

        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, "Levante", "Ross 308", fechaEncaset: null, idElegida: null, nombreElegida: null);

        Assert.Contains("no tiene fecha de encasetamiento", motivo);
    }

    [Fact]
    public void EncasetAnteriorALaVigencia_DiceLasDosFechas()
    {
        var candidatas = new[] { new C(1, "Levante", null, Ago, true), new C(2, "Levante", null, Jul, true) };

        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, "Levante", "Ross 308", new DateOnly(2026, 5, 20), idElegida: null, nombreElegida: null);

        // La más temprana es la referencia: es la primera que el lote podría llegar a alcanzar.
        Assert.Contains("01/07/2026", motivo);
        Assert.Contains("20/05/2026", motivo);
    }

    [Fact]
    public void LasApagadasNoCuentanNiParaExplicar()
    {
        var candidatas = new[] { new C(1, "Levante", null, null, Activa: false) };

        var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
            candidatas, "Levante", "Ross 308", new DateOnly(2026, 6, 1), idElegida: null, nombreElegida: null);

        Assert.Contains("no tiene plantillas activas", motivo);
    }
}
