using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la separación (reserva) de alimento y aves.
///
/// <para>
/// Lo que estos tests fijan es la diferencia de fondo con el descuento: la reserva <b>reescribe</b> y
/// el descuento <b>diferencia</b>. Mientras el registro está pendiente no se movió una sola unidad de
/// inventario, así que editar no puede requerir devoluciones ni compensaciones — y eso hay que poder
/// demostrarlo, porque es lo que le permite al usuario corregir un seguimiento sin trabajo manual
/// por debajo.
/// </para>
/// </summary>
public class ReservaSeguimientoCalculosTests
{
    private static ItemConsumoKey Item(int id, int? silo = null) => new(id, EsItemInventario: true, SiloId: silo);

    // ─── Alimento ─────────────────────────────────────────────────────────────

    [Fact]
    public void LineasDeAlimento_DescartaCerosYNegativos()
    {
        var consumo = new Dictionary<ItemConsumoKey, decimal>
        {
            [Item(10)] = 120m,
            [Item(11)] = 0m,
            [Item(12)] = -3m
        };

        var lineas = ReservaSeguimientoCalculos.LineasDeAlimento(consumo);

        Assert.Single(lineas);
        Assert.Equal(10, lineas[0].Item.Id);
        Assert.Equal(120m, lineas[0].Kg);
    }

    [Fact]
    public void LineasDeAlimento_MismoItemEnDosSilosSonDosLineas()
    {
        // Fase C de silos: dos filas del mismo ítem en silos distintos se descuentan por separado,
        // así que también se separan por separado.
        var consumo = new Dictionary<ItemConsumoKey, decimal>
        {
            [Item(10, silo: 3)] = 100m,
            [Item(10, silo: 4)] = 50m
        };

        var lineas = ReservaSeguimientoCalculos.LineasDeAlimento(consumo);

        Assert.Equal(2, lineas.Count);
        Assert.Equal(150m, lineas.Sum(l => l.Kg));
    }

    [Fact]
    public void LineasDeAlimento_SinConsumoDevuelveVacio()
    {
        Assert.Empty(ReservaSeguimientoCalculos.LineasDeAlimento(new Dictionary<ItemConsumoKey, decimal>()));
        Assert.Empty(ReservaSeguimientoCalculos.LineasDeAlimento(null!));
    }

    [Fact]
    public void EnEdicion_ElResultadoNoDependeDeLoQueHabiaAntes()
    {
        // El invariante de la funcionalidad: editar un pendiente NO hace diff (nuevo − viejo) porque
        // nunca se descontó nada. Con reservas viejas muy distintas, el resultado es el mismo.
        var nuevas = new Dictionary<ItemConsumoKey, decimal> { [Item(20)] = 80m };

        var desdeVacio = ReservaSeguimientoCalculos.ReescribirEnEdicion(
            Array.Empty<ReservaAlimentoLinea>(), nuevas);

        var desdeOtrasMuyDistintas = ReservaSeguimientoCalculos.ReescribirEnEdicion(
            new[]
            {
                new ReservaAlimentoLinea(Item(99), 5000m),
                new ReservaAlimentoLinea(Item(20), 1m)
            },
            nuevas);

        Assert.Equal(desdeVacio, desdeOtrasMuyDistintas);
        Assert.Single(desdeVacio);
        Assert.Equal(80m, desdeVacio[0].Kg);
    }

    [Fact]
    public void EnEdicion_QuitarTodoElAlimentoDejaLaReservaVacia()
    {
        // Corregir a «este día no consumió» libera la separación entera, sin devoluciones.
        var resultado = ReservaSeguimientoCalculos.ReescribirEnEdicion(
            new[] { new ReservaAlimentoLinea(Item(20), 80m) },
            new Dictionary<ItemConsumoKey, decimal>());

        Assert.Empty(resultado);
    }

    // ─── Aves ─────────────────────────────────────────────────────────────────

    [Fact]
    public void LineasDeAves_SumaMortalidadSeleccionYErrorDeSexaje()
    {
        var r = ReservaSeguimientoCalculos.LineasDeAves(
            mortalidadHembras: 3, selHembras: 2, errorSexajeHembras: 1,
            mortalidadMachos: 4, selMachos: 0, errorSexajeMachos: 2,
            loteEsMixto: false);

        Assert.Equal(6, r.Hembras);
        Assert.Equal(6, r.Machos);
        Assert.Equal(0, r.Mixtas);
        Assert.Equal(12, r.Total);
    }

    [Fact]
    public void LineasDeAves_CoincideConElDescuentoReal()
    {
        // Si la reserva y el descuento no usan la misma definición de «baja del día», al validar se
        // liberaría una cantidad distinta a la que se descuenta y el saldo quedaría corrido.
        var (h, m) = RetiroAvesEngordeCalculos.BajasDelDia(3, 2, 1, 4, 0, 2);
        var r = ReservaSeguimientoCalculos.LineasDeAves(3, 2, 1, 4, 0, 2, loteEsMixto: false);

        Assert.Equal(h, r.Hembras);
        Assert.Equal(m, r.Machos);
    }

    [Fact]
    public void LoteMixto_TodoVaAMixtas()
    {
        // En un lote mixto el saldo no está sexado: el disponible contra el que hay que restar es
        // el de mixtas, así que separar por sexo dejaría la resta sin efecto.
        var r = ReservaSeguimientoCalculos.LineasDeAves(
            mortalidadHembras: 10, selHembras: 5, errorSexajeHembras: 0,
            mortalidadMachos: 0, selMachos: 0, errorSexajeMachos: 0,
            loteEsMixto: true);

        Assert.Equal(0, r.Hembras);
        Assert.Equal(0, r.Machos);
        Assert.Equal(15, r.Mixtas);
    }

    [Fact]
    public void DiaSinBajas_NoGeneraReserva()
    {
        var r = ReservaSeguimientoCalculos.LineasDeAves(0, 0, 0, 0, 0, 0, loteEsMixto: false);

        Assert.True(r.EstaVacia);
        Assert.Equal(0, r.Total);
    }

    // ─── Disponible ───────────────────────────────────────────────────────────

    [Fact]
    public void DisponibleAlimento_DescuentaLoSeparado()
    {
        // El caso que motivó la funcionalidad: dos lotes sobre el mismo galpón. El primero separó
        // 300 de 1.000; el segundo tiene que ver 700, no 1.000.
        Assert.Equal(700m, ReservaSeguimientoCalculos.DisponibleAlimento(1000m, 300m));
    }

    [Fact]
    public void DisponibleAlimento_NoSeRecortaACero()
    {
        // Sobre-separado: esconderlo detrás de un 0 borra la señal de que dos lotes se pisaron.
        Assert.Equal(-50m, ReservaSeguimientoCalculos.DisponibleAlimento(100m, 150m));
    }

    [Fact]
    public void DisponibleAves_DescuentaLasBajasSinValidar()
    {
        // Sin esto, un traslado o una venta pueden despachar aves que un registro pendiente ya dio
        // de baja.
        Assert.Equal(4_850, ReservaSeguimientoCalculos.DisponibleAves(5_000, 150));
        Assert.Equal(-10, ReservaSeguimientoCalculos.DisponibleAves(0, 10));
    }

    // ─── Dónde NO va la resta: el riesgo real es contar dos veces ─────────────
    // Tres de las cinco superficies de «aves disponibles» ya traen las bajas sin validar dentro del
    // saldo (engorde y reproductora por `registradas − aplicadas`; levante con lote base porque el
    // resumen las suma desde `seguimiento_diario`). Restarles además la reserva bloquearía traslados
    // de aves que sí existen. El doble descuento no es hipotético: `AvesDisponiblesEngordeCalculos`
    // nació de uno.

    [Fact]
    public void FlagApagado_DisponibleAvesEsElSaldoTalCual()
    {
        // Sin doble validación no hay reservas activas: el número no se mueve ni un ave.
        Assert.Equal(5_000, ReservaSeguimientoCalculos.DisponibleAves(5_000, 0));
    }

    [Fact]
    public void DisponibleAves_SobreUnSaldoQueYaIncluyeLasBajas_RestariaDosVeces()
    {
        // Documenta por qué la resta va SOLO donde el saldo sale del maestro.
        // Maestro 5.000, un registro sin validar con 150 bajas.
        const int maestro = 5_000, bajasSinValidar = 150;

        // Camino correcto: el saldo viene del maestro (no descontado) ⇒ se resta una vez.
        var desdeMaestro = ReservaSeguimientoCalculos.DisponibleAves(maestro, bajasSinValidar);
        Assert.Equal(4_850, desdeMaestro);

        // Camino equivocado: el saldo ya venía con las bajas adentro (4.850) y se vuelve a restar.
        var saldoQueYaLasIncluye = maestro - bajasSinValidar;
        var dobleResta = ReservaSeguimientoCalculos.DisponibleAves(saldoQueYaLasIncluye, bajasSinValidar);

        Assert.Equal(4_700, dobleResta);
        Assert.NotEqual(desdeMaestro, dobleResta);   // 150 aves que existen y no se podrían trasladar
    }
}
