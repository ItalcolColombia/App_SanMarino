using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.VacunacionMaterializadorCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del materializador de vacunación.
///
/// <para>
/// Es la única parte del módulo que escribe sobre lotes vivos, así que lo que estos tests fijan no es
/// «que copie bien» —eso es lo fácil— sino <b>lo que nunca puede pasar</b>: que una vacuna ya aplicada
/// cambie de fecha objetivo, que una corrección hecha a mano se pierda, que aplicar dos veces deje el
/// cronograma duplicado, o que una fila desaparezca del informe sin que nadie se entere.
/// </para>
/// </summary>
public class VacunacionMaterializadorCalculosTests
{
    // ─── Ayudas ───────────────────────────────────────────────────────────────

    private static ItemPlantilla Plan(int id, int vacuna = 100, int semana = 5, int orden = 0,
                                      int antes = 6, int despues = 0, string? notas = null) =>
        new(id, vacuna, "Semana", semana, antes, despues, orden, notas);

    /// <summary>Fila del lote que salió del plan y sigue gobernada por él.</summary>
    private static ItemCronograma Derivada(int id, ItemPlantilla de, bool tieneRegistro = false) =>
        new(id, de.Id, GeneradoAutomatico: true, tieneRegistro,
            de.ItemInventarioId, de.UnidadObjetivo, de.ValorObjetivo,
            de.RangoDiasAntes, de.RangoDiasDespues, de.Orden, de.Notas);

    /// <summary>Lo que devolvería el cronograma después de aplicar un plan: se re-alimenta para probar idempotencia.</summary>
    private static List<ItemCronograma> Materializar(VacunacionMaterializadorCalculos.Plan plan,
                                                     IEnumerable<ItemCronograma>? previas = null)
    {
        var filas = (previas ?? []).ToList();
        var siguienteId = filas.Count == 0 ? 1 : filas.Max(f => f.Id) + 1;

        foreach (var alta in plan.Faltantes)
            filas.Add(Derivada(siguienteId++, alta));

        foreach (var upd in plan.Actualizables)
        {
            var i = filas.FindIndex(f => f.Id == upd.CronogramaItemId);
            filas[i] = Derivada(upd.CronogramaItemId, upd.Plantilla, filas[i].TieneRegistro);
        }

        return filas;
    }

    // ─── Alta e idempotencia ──────────────────────────────────────────────────

    [Fact]
    public void CronogramaVacio_TodoEsAlta()
    {
        var plantilla = new[] { Plan(1, semana: 1), Plan(2, semana: 5), Plan(3, semana: 12) };

        var plan = Planificar(plantilla, []);

        Assert.Equal(3, plan.Faltantes.Count);
        Assert.Empty(plan.Actualizables);
        Assert.Empty(plan.Preservados);
        Assert.Empty(plan.Sobrantes);
        Assert.False(plan.NoEscribeNada);
    }

    [Fact]
    public void AplicarDosVeces_LaSegundaNoEscribeNada()
    {
        var plantilla = new[] { Plan(1, semana: 1), Plan(2, semana: 5), Plan(3, semana: 12) };

        var filas = Materializar(Planificar(plantilla, []));
        var segunda = Planificar(plantilla, filas);

        Assert.True(segunda.NoEscribeNada);
        Assert.Empty(segunda.Faltantes);
        Assert.Empty(segunda.Actualizables);
        Assert.Equal(3, segunda.Preservados.Count);
        Assert.All(segunda.Preservados, p => Assert.Equal(MotivoPreservado.SinCambios, p.Motivo));
    }

    [Fact]
    public void AplicarTresVeces_SigueSinEscribir()
    {
        var plantilla = new[] { Plan(1), Plan(2, semana: 9) };

        var filas = Materializar(Planificar(plantilla, []));
        filas = Materializar(Planificar(plantilla, filas), filas);
        var tercera = Planificar(plantilla, filas);

        Assert.True(tercera.NoEscribeNada);
        Assert.Equal(2, filas.Count); // y sobre todo: no se duplicó
    }

    [Fact]
    public void PlantillaConUnaVacunaNueva_SoloEsaEsAlta()
    {
        var viejos = new[] { Plan(1), Plan(2, semana: 9) };
        var filas = Materializar(Planificar(viejos, []));

        var conNueva = viejos.Append(Plan(3, vacuna: 200, semana: 14)).ToArray();
        var plan = Planificar(conNueva, filas);

        Assert.Single(plan.Faltantes);
        Assert.Equal(3, plan.Faltantes[0].Id);
        Assert.Equal(2, plan.Preservados.Count);
    }

    // ─── El invariante duro: lo aplicado no se toca ───────────────────────────

    [Fact]
    public void ItemYaAplicado_NoSeToca_AunqueLaPlantillaCambieLaSemana()
    {
        var original = Plan(1, semana: 5);
        var fila = Derivada(10, original, tieneRegistro: true);

        var plan = Planificar(new[] { Plan(1, semana: 8) }, new[] { fila });

        Assert.Empty(plan.Actualizables);
        var preservado = Assert.Single(plan.Preservados);
        Assert.Equal(MotivoPreservado.YaAplicado, preservado.Motivo);
        Assert.Equal(10, preservado.CronogramaItemId);
    }

    [Fact]
    public void ItemYaAplicado_GanaSobreCualquierOtroMotivo()
    {
        // Aplicado Y editado a mano Y con la plantilla cambiada: el motivo que se reporta es el que manda.
        var fila = new ItemCronograma(10, OrigenPlantillaItemId: 1, GeneradoAutomatico: false, TieneRegistro: true,
            ItemInventarioId: 100, "Semana", 5, 6, 0, 0, null);

        var plan = Planificar(new[] { Plan(1, semana: 8) }, new[] { fila });

        Assert.Equal(MotivoPreservado.YaAplicado, Assert.Single(plan.Preservados).Motivo);
    }

    [Fact]
    public void VaciarLaPlantillaNoBorraLoAplicado_LoReportaComoSobrante()
    {
        var fila = Derivada(10, Plan(1), tieneRegistro: true);

        var plan = Planificar([], new[] { fila });

        var sobrante = Assert.Single(plan.Sobrantes);
        Assert.Equal(10, sobrante.CronogramaItemId);
        Assert.True(sobrante.TieneRegistro);
        Assert.Equal(MotivoSobrante.PlantillaSinEseItem, sobrante.Motivo);
        Assert.True(plan.NoEscribeNada);
    }

    // ─── El ítem que alguien corrigió a mano ──────────────────────────────────

    [Fact]
    public void ItemEmancipado_NoSePisa_AunqueLaPlantillaDigaOtraCosa()
    {
        var fila = new ItemCronograma(10, OrigenPlantillaItemId: 1, GeneradoAutomatico: false, TieneRegistro: false,
            ItemInventarioId: 100, "Semana", 6, 6, 0, 0, "corregido en campo");

        var plan = Planificar(new[] { Plan(1, semana: 5) }, new[] { fila });

        Assert.Empty(plan.Actualizables);
        Assert.Equal(MotivoPreservado.Manual, Assert.Single(plan.Preservados).Motivo);
    }

    [Fact]
    public void ItemCargadoAMano_EsInvisibleParaElMaterializador()
    {
        // Sin origen: no es del plan. Ni se actualiza, ni cuenta como sobrante, ni bloquea el alta.
        var aMano = new ItemCronograma(10, OrigenPlantillaItemId: null, GeneradoAutomatico: false, TieneRegistro: false,
            ItemInventarioId: 100, "Semana", 5, 6, 0, 0, null);

        var plan = Planificar(new[] { Plan(1, semana: 5) }, new[] { aMano });

        Assert.Single(plan.Faltantes);
        Assert.Empty(plan.Preservados);
        Assert.Empty(plan.Sobrantes);
        Assert.Empty(plan.Actualizables);
    }

    // ─── Qué cuenta como cambio ───────────────────────────────────────────────

    [Theory]
    [InlineData("semana")]
    [InlineData("vacuna")]
    [InlineData("antes")]
    [InlineData("despues")]
    [InlineData("orden")]
    [InlineData("notas")]
    public void CambiarCualquierCampoCopiado_LoVuelveActualizable(string campo)
    {
        var original = Plan(1, vacuna: 100, semana: 5, orden: 2, antes: 6, despues: 0, notas: "vía ocular");
        var fila = Derivada(10, original);

        var nuevo = campo switch
        {
            "semana"  => original with { ValorObjetivo = 6 },
            "vacuna"  => original with { ItemInventarioId = 200 },
            "antes"   => original with { RangoDiasAntes = 3 },
            "despues" => original with { RangoDiasDespues = 2 },
            "orden"   => original with { Orden = 7 },
            "notas"   => original with { Notas = "vía subcutánea" },
            _ => throw new ArgumentOutOfRangeException(nameof(campo)),
        };

        var plan = Planificar(new[] { nuevo }, new[] { fila });

        var upd = Assert.Single(plan.Actualizables);
        Assert.Equal(10, upd.CronogramaItemId);
        Assert.Equal(nuevo, upd.Plantilla);
        Assert.Empty(plan.Preservados);
    }

    [Fact]
    public void ActualizarYVolverAPlanificar_YaNoEscribe()
    {
        var original = Plan(1, semana: 5);
        var filas = new List<ItemCronograma> { Derivada(10, original) };
        var cambiado = new[] { Plan(1, semana: 8) };

        filas = Materializar(Planificar(cambiado, filas), filas);

        Assert.True(Planificar(cambiado, filas).NoEscribeNada);
        Assert.Equal(8, filas.Single().ValorObjetivo);
    }

    [Theory]
    [InlineData("semana", "SEMANA")]
    [InlineData(" Semana ", "Semana")]
    public void LaUnidadNoDistingueMayusculasNiEspacios(string enLaFila, string enElPlan)
    {
        var item = Plan(1) with { UnidadObjetivo = enElPlan };
        var fila = Derivada(10, item) with { UnidadObjetivo = enLaFila };

        Assert.Empty(Planificar(new[] { item }, new[] { fila }).Actualizables);
    }

    [Fact]
    public void NotaVaciaYNotaNula_SonLoMismo()
    {
        var item = Plan(1, notas: null);
        var fila = Derivada(10, item) with { Notas = "   " };

        Assert.Empty(Planificar(new[] { item }, new[] { fila }).Actualizables);
    }

    // ─── Sobrantes ────────────────────────────────────────────────────────────

    [Fact]
    public void QuitarUnaVacunaDelPlan_DejaLaFilaComoSobrante_YNoTocaLasOtras()
    {
        var plantilla = new[] { Plan(1), Plan(2, semana: 9), Plan(3, semana: 14) };
        var filas = Materializar(Planificar(plantilla, []));

        var plan = Planificar(plantilla.Where(p => p.Id != 2), filas);

        var sobrante = Assert.Single(plan.Sobrantes);
        Assert.Equal(2, sobrante.OrigenPlantillaItemId);
        Assert.Equal(MotivoSobrante.PlantillaSinEseItem, sobrante.Motivo);
        Assert.Equal(2, plan.Preservados.Count);
        Assert.True(plan.NoEscribeNada);
    }

    [Fact]
    public void DosFilasReclamandoElMismoItem_ManaLaPrimeraYLaOtraSeReporta()
    {
        var item = Plan(1);
        var primera = Derivada(10, item);
        var duplicada = Derivada(11, item);

        var plan = Planificar(new[] { item }, new[] { duplicada, primera });

        Assert.Equal(MotivoPreservado.SinCambios, Assert.Single(plan.Preservados).Motivo);
        Assert.Equal(10, plan.Preservados[0].CronogramaItemId);

        var sobrante = Assert.Single(plan.Sobrantes);
        Assert.Equal(11, sobrante.CronogramaItemId);
        Assert.Equal(MotivoSobrante.Duplicado, sobrante.Motivo);

        Assert.True(plan.NoEscribeNada); // ni una escritura: el duplicado se informa, no se corrige solo
    }

    // ─── Totalidad y determinismo ─────────────────────────────────────────────

    [Fact]
    public void NingunaFilaSePierdeEnElCamino()
    {
        var plantilla = new[] { Plan(1), Plan(2, semana: 9), Plan(3, semana: 14) };
        var filas = new List<ItemCronograma>
        {
            Derivada(10, Plan(1)),                                    // sin cambios
            Derivada(11, Plan(2, semana: 8)),                         // actualizable
            Derivada(12, Plan(9)),                                    // sobrante
            new(13, null, false, false, 100, "Semana", 3, 6, 0, 0, null), // a mano: no entra
        };

        var plan = Planificar(plantilla, filas);

        var contadas = plan.Actualizables.Select(a => a.CronogramaItemId)
            .Concat(plan.Preservados.Select(p => p.CronogramaItemId))
            .Concat(plan.Sobrantes.Select(s => s.CronogramaItemId))
            .ToList();

        Assert.Equal(contadas.Count, contadas.Distinct().Count());   // listas disjuntas
        Assert.Equal(new[] { 10, 11, 12 }, contadas.Order());        // todas las del plan, ninguna más
        Assert.Equal(3, Assert.Single(plan.Faltantes).Id);
    }

    [Fact]
    public void ElOrdenDeEntradaNoCambiaElResultado()
    {
        var plantilla = new[] { Plan(3, semana: 14), Plan(1), Plan(2, semana: 9) };
        var filas = new List<ItemCronograma> { Derivada(12, Plan(9)), Derivada(10, Plan(1)) };

        var a = Planificar(plantilla, filas);
        var b = Planificar(plantilla.Reverse(), Enumerable.Reverse(filas));

        Assert.Equal(a.Faltantes, b.Faltantes);
        Assert.Equal(a.Actualizables, b.Actualizables);
        Assert.Equal(a.Preservados, b.Preservados);
        Assert.Equal(a.Sobrantes, b.Sobrantes);
    }

    [Fact]
    public void SinPlantillaYSinCronograma_NoHayNadaQueHacer()
    {
        var plan = Planificar([], []);

        Assert.True(plan.NoEscribeNada);
        Assert.Empty(plan.Sobrantes);
        Assert.Empty(plan.Preservados);
    }

    [Fact]
    public void EntradasNulas_NoRevientan()
    {
        var plan = Planificar(null, null);

        Assert.True(plan.NoEscribeNada);
        Assert.Empty(plan.Sobrantes);
    }
}
