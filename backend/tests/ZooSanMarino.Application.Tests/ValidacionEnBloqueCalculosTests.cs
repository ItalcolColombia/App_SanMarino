using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del «validar todos los pendientes del lote».
///
/// <para>
/// Los dos bloques que más importan son <c>Orden_*</c> y <c>Invariante_*</c>. El del orden no es
/// ergonomía: la guarda de aves compara totales mientras el descuento recorta por bucket, así que hay
/// lotes que validan enteros en un orden y cortan en el otro. El invariante es lo que evita que el
/// front tenga que restar para saber cuántos quedaron sin intentar.
/// </para>
/// </summary>
public class ValidacionEnBloqueCalculosTests
{
    private static PendienteValidacion P(int dia, long id) =>
        new(id, new DateOnly(2026, 8, dia));

    // ─── Orden y recorte ──────────────────────────────────────────────────────

    [Fact]
    public void Orden_EsCronologicoDelMasViejoAlMasNuevo()
    {
        var (sel, fuera) = ValidacionEnBloqueCalculos.OrdenDeValidacion(
            new[] { P(20, 3), P(10, 1), P(15, 2) });

        Assert.Equal(new[] { 1L, 2L, 3L }, sel.Select(s => s.SeguimientoId));
        Assert.Empty(fuera);
    }

    /// <summary>
    /// Dos filas del mismo día no pueden depender del orden en que las devolvió la consulta: sin el
    /// desempate, el resultado del bloque cambiaría entre corridas idénticas.
    /// </summary>
    [Fact]
    public void Orden_DesempataPorIdCuandoLaFechaEmpata()
    {
        var (sel, _) = ValidacionEnBloqueCalculos.OrdenDeValidacion(
            new[] { P(10, 99), P(10, 7), P(10, 42) });

        Assert.Equal(new[] { 7L, 42L, 99L }, sel.Select(s => s.SeguimientoId));
    }

    [Fact]
    public void Orden_ListaVaciaONulaNoRompe()
    {
        var (selVacia, fueraVacia) = ValidacionEnBloqueCalculos.OrdenDeValidacion(
            Array.Empty<PendienteValidacion>());
        Assert.Empty(selVacia);
        Assert.Empty(fueraVacia);

        var (selNula, fueraNula) = ValidacionEnBloqueCalculos.OrdenDeValidacion(null);
        Assert.Empty(selNula);
        Assert.Empty(fueraNula);
    }

    /// <summary>
    /// Pasado el tope se toman los <b>primeros cronológicos</b>, no los últimos: son los que bloquean
    /// el alta de días nuevos, así que validarlos es el progreso que el operario necesita.
    /// </summary>
    [Fact]
    public void Orden_PasadoElTopeTomaLosMasViejosYElRestoQuedaFuera()
    {
        var muchos = Enumerable.Range(1, 10).Select(i => P(i, i)).ToArray();

        var (sel, fuera) = ValidacionEnBloqueCalculos.OrdenDeValidacion(muchos, tope: 4);

        Assert.Equal(new[] { 1L, 2L, 3L, 4L }, sel.Select(s => s.SeguimientoId));
        Assert.Equal(new[] { 5L, 6L, 7L, 8L, 9L, 10L }, fuera.Select(s => s.SeguimientoId));
    }

    [Fact]
    public void Orden_ElTopeSuperaElPicoMedidoDeTreintaYCuatroDias()
    {
        // El caso que motiva el feature: ItalcolPanama cargó 34 días en una sesión. Un tope que no lo
        // cubriera dejaría afuera justamente el escenario para el que se construyó esto.
        Assert.True(ValidacionEnBloqueCalculos.MaxRegistrosPorBloque >= 34);
    }

    // ─── Clasificación de cada registro ───────────────────────────────────────

    [Fact]
    public void Item_ValidadoConEfectoSeDistingueDelSinEfecto()
    {
        var conEfecto = ValidacionEnBloqueCalculos.ItemAplicado(P(10, 1), 2, 850m, 12, yaEstabaValidado: false);
        Assert.Equal(DesenlaceValidacionEnBloque.Validado, conEfecto.Resultado);

        var sinEfecto = ValidacionEnBloqueCalculos.ItemAplicado(P(10, 2), 0, 0m, 0, yaEstabaValidado: false);
        Assert.Equal(DesenlaceValidacionEnBloque.ValidadoSinEfecto, sinEfecto.Resultado);
    }

    /// <summary>
    /// Sin el dato de «ya estaba validado», este caso y el de arriba son el mismo <c>(0, 0, 0)</c> y
    /// el conteo del bloque mentiría diciendo que validó algo que ya estaba.
    /// </summary>
    [Fact]
    public void Item_YaValidadoNoCuentaComoValidadoAhoraNiAportaEfecto()
    {
        var item = ValidacionEnBloqueCalculos.ItemAplicado(P(10, 1), 5, 999m, 30, yaEstabaValidado: true);

        Assert.Equal(DesenlaceValidacionEnBloque.YaValidado, item.Resultado);
        Assert.Equal(0, item.ItemsAplicados);
        Assert.Equal(0m, item.KgAplicados);
        Assert.Equal(0, item.AvesDescontadas);
    }

    [Fact]
    public void Item_FallidoConservaElMotivoYToleraUnoVacio()
    {
        Assert.Equal("Sin stock", ValidacionEnBloqueCalculos.ItemFallido(P(10, 1), "  Sin stock  ").Motivo);
        Assert.False(string.IsNullOrWhiteSpace(ValidacionEnBloqueCalculos.ItemFallido(P(10, 1), "   ").Motivo));
    }

    // ─── Resumen e invariante ─────────────────────────────────────────────────

    /// <summary>
    /// <c>Validados + YaValidados + Fallidos + NoIntentados == Solicitados</c>. Es lo que permite al
    /// front decir «falló 1 y quedan 14 sin intentar» en vez de «fallaron 15»: sin la separación, el
    /// operario cree que tiene que revisar quince registros y en realidad tiene que corregir uno.
    /// </summary>
    [Fact]
    public void Invariante_LosCuatroConteosSumanLosSolicitados()
    {
        var items = new List<ItemValidacionEnBloque>
        {
            ValidacionEnBloqueCalculos.ItemAplicado(P(1, 1), 1, 100m, 5, false),
            ValidacionEnBloqueCalculos.ItemAplicado(P(2, 2), 0, 0m, 0, false),
            ValidacionEnBloqueCalculos.ItemAplicado(P(3, 3), 0, 0m, 0, true),
            ValidacionEnBloqueCalculos.ItemFallido(P(4, 4), "Sin stock"),
            ValidacionEnBloqueCalculos.ItemNoIntentado(P(5, 5)),
            ValidacionEnBloqueCalculos.ItemNoIntentado(P(6, 6)),
        };

        var r = ValidacionEnBloqueCalculos.Resumir(items);

        Assert.Equal(6, r.Solicitados);
        Assert.Equal(2, r.Validados);      // con efecto + sin efecto
        Assert.Equal(1, r.YaValidados);
        Assert.Equal(1, r.Fallidos);
        Assert.Equal(2, r.NoIntentados);
        Assert.Equal(r.Solicitados, r.Validados + r.YaValidados + r.Fallidos + r.NoIntentados);
    }

    [Fact]
    public void Resumen_LosKilosYAvesSumanSoloLoValidadoAhora()
    {
        var items = new List<ItemValidacionEnBloque>
        {
            ValidacionEnBloqueCalculos.ItemAplicado(P(1, 1), 1, 100.5m, 5, false),
            ValidacionEnBloqueCalculos.ItemAplicado(P(2, 2), 1, 200m, 7, false),
            ValidacionEnBloqueCalculos.ItemAplicado(P(3, 3), 9, 999m, 99, true),   // ya validado: cero
            ValidacionEnBloqueCalculos.ItemNoIntentado(P(4, 4)),
        };

        var r = ValidacionEnBloqueCalculos.Resumir(items);

        Assert.Equal(300.5m, r.KgAplicados);
        Assert.Equal(12, r.AvesDescontadas);
    }

    [Fact]
    public void Resumen_ElCorteQuedaIdentificadoConSuFechaYMotivo()
    {
        var items = new List<ItemValidacionEnBloque>
        {
            ValidacionEnBloqueCalculos.ItemAplicado(P(1, 1), 1, 100m, 5, false),
            ValidacionEnBloqueCalculos.ItemFallido(P(8, 77), "Sin stock del ítem"),
            ValidacionEnBloqueCalculos.ItemNoIntentado(P(9, 9)),
        };

        var r = ValidacionEnBloqueCalculos.Resumir(items);

        Assert.Equal(77L, r.SeguimientoCorte);
        Assert.Equal(new DateOnly(2026, 8, 8), r.FechaCorte);
        Assert.Equal("Sin stock del ítem", r.MotivoCorte);
    }

    [Fact]
    public void Resumen_SinItemsNoRompeYLoDiceEnCastellano()
    {
        var r = ValidacionEnBloqueCalculos.Resumir(null);

        Assert.Equal(0, r.Solicitados);
        Assert.Null(r.SeguimientoCorte);
        Assert.Equal("El lote no tiene registros pendientes de validar.", r.Mensaje);
    }

    // ─── El mensaje, byte a byte ──────────────────────────────────────────────
    //
    // Va fijado literal por el mismo motivo que el mensaje de bloqueo por vencidos: el defecto que
    // aquel test fija era «un registro … que superaron». La concordancia se arma entera —sustantivo y
    // verbo— y el front no concatena nada.

    [Fact]
    public void Mensaje_SinPendientes()
    {
        Assert.Equal(
            "El lote no tiene registros pendientes de validar.",
            ValidacionEnBloqueCalculos.MensajeResultado(0, 0, 0, 0, 0, 0m, 0, null, null));
    }

    [Fact]
    public void Mensaje_UnoSoloValidado_VaEnSingular()
    {
        Assert.Equal(
            "Se validó 1 registro. Se aplicaron 850 kg de alimento y 12 aves.",
            ValidacionEnBloqueCalculos.MensajeResultado(1, 1, 0, 0, 0, 850m, 12, null, null));
    }

    [Fact]
    public void Mensaje_VariosValidados_VaEnPlural()
    {
        Assert.Equal(
            "Se validaron 34 registros. Se aplicaron 12500.75 kg de alimento y 210 aves.",
            ValidacionEnBloqueCalculos.MensajeResultado(34, 34, 0, 0, 0, 12500.75m, 210, null, null));
    }

    [Theory]
    [InlineData(1, "El registro ya estaba validado.")]
    [InlineData(4, "Los 4 registros ya estaban validados.")]
    public void Mensaje_TodosYaEstabanValidados(int yaValidados, string esperado)
    {
        Assert.Equal(esperado,
            ValidacionEnBloqueCalculos.MensajeResultado(
                yaValidados, 0, yaValidados, 0, 0, 0m, 0, null, null));
    }

    [Fact]
    public void Mensaje_CorteEnElMedio_DiceQueSeHizoQueFalloYQueQueda()
    {
        var esperado =
            "Se validaron 19 de 34 registros. El del 08/07/2026 no se pudo validar: Sin stock del ítem." +
            " Quedaron 14 registros sin intentar. Corregí ese registro y volvé a validar.";

        Assert.Equal(esperado,
            ValidacionEnBloqueCalculos.MensajeResultado(
                34, 19, 0, 1, 14, 5000m, 90,
                new DateOnly(2026, 7, 8), "Sin stock del ítem."));
    }

    [Fact]
    public void Mensaje_CorteEnElPrimero_NoDiceQueValidoNinguno()
    {
        var esperado =
            "No se validó ninguno de los 5 registros. El del 08/07/2026 no se pudo validar: Sin stock." +
            " Quedaron 4 registros sin intentar. Corregí ese registro y volvé a validar.";

        Assert.Equal(esperado,
            ValidacionEnBloqueCalculos.MensajeResultado(
                5, 0, 0, 1, 4, 0m, 0, new DateOnly(2026, 7, 8), "Sin stock."));
    }

    [Fact]
    public void Mensaje_CorteEnElUltimo_NoInventaPendientes()
    {
        var esperado =
            "Se validaron 4 de 5 registros. El del 08/07/2026 no se pudo validar: Sin stock." +
            " Corregí ese registro y volvé a validar.";

        Assert.Equal(esperado,
            ValidacionEnBloqueCalculos.MensajeResultado(
                5, 4, 0, 1, 0, 100m, 3, new DateOnly(2026, 7, 8), "Sin stock."));
    }

    [Fact]
    public void Mensaje_CorteConUnSoloPendiente_VaEnSingular()
    {
        var m = ValidacionEnBloqueCalculos.MensajeResultado(
            3, 1, 0, 1, 1, 10m, 1, new DateOnly(2026, 7, 8), "Sin stock.");

        Assert.Contains("Se validó 1 de 3 registros.", m);
        Assert.Contains("Quedó 1 registro sin intentar.", m);
    }

    /// <summary>
    /// El motivo del choque de concurrencia tiene que ser legible para un operario: el texto crudo de
    /// una excepción de EF nombra índices y tablas que no le dicen nada.
    /// </summary>
    [Fact]
    public void Mensaje_ElConflictoConcurrenteSeExplicaEnCastellano()
    {
        var m = ValidacionEnBloqueCalculos.MotivoConflictoConcurrente();

        Assert.Contains("Otro usuario", m);
        Assert.DoesNotContain("Exception", m);
        Assert.DoesNotContain("uq_", m);
    }

    /// <summary>El punto final del motivo no se duplica ni se pierde según cómo venga.</summary>
    [Fact]
    public void Mensaje_ElMotivoSeInsertaTalCualSinReescribirlo()
    {
        var m = ValidacionEnBloqueCalculos.MensajeResultado(
            2, 1, 0, 1, 0, 0m, 0, new DateOnly(2026, 7, 8), "  Sin stock del ítem X.  ");

        Assert.Contains("no se pudo validar: Sin stock del ítem X.", m);
    }
}
