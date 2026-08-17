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

    // ─── Aplicabilidad: validar no puede "pasar" sin descontar ────────────────
    // El bug que motivó esto: la separación guardaba el `pais_id` CRUDO del lote (y producción
    // guardaba `null` fijo). Con el país sin resolver el gate devuelve `Ninguno`, la aplicación hacía
    // `continue` y el registro quedaba validado, las reservas APLICADAS y el inventario intacto —con
    // el endpoint informando igual los kilos, porque el total se sumaba antes del bucle—.

    [Fact]
    public void PaisSinResolverConKilos_NoSePuedeAplicar()
    {
        var motivo = ReservaSeguimientoCalculos.MotivoAlimentoNoAplicable(
            ModeloInventarioConsumo.Ninguno, kg: 120m, paisId: 0, loteRef: "K345A");

        Assert.NotNull(motivo);
        Assert.Contains("K345A", motivo);
        Assert.Contains("120", motivo);   // el mensaje dice CUÁNTO quedó sin descontar
    }

    [Fact]
    public void PaisSinResolverSinKilos_NoReclamaNada()
    {
        // Un día sin consumo no tiene nada que descontar: exigirle un país sería bloquear por nada.
        Assert.Null(ReservaSeguimientoCalculos.MotivoAlimentoNoAplicable(
            ModeloInventarioConsumo.Ninguno, kg: 0m, paisId: 0, loteRef: "K345A"));
    }

    [Theory]
    [InlineData(InventarioConsumoGate.PaisColombia, ModeloInventarioConsumo.ModeloBNivelGranja)]
    [InlineData(InventarioConsumoGate.PaisEcuador, ModeloInventarioConsumo.ModeloB)]
    [InlineData(InventarioConsumoGate.PaisPanama, ModeloInventarioConsumo.ModeloB)]
    public void LosTresPaisesResuelvenAUnModelo_YSeAplican(int paisId, ModeloInventarioConsumo esperado)
    {
        // `paises` tiene tres filas y las tres mapean a un modelo. Por eso `Ninguno` NUNCA es un caso
        // legítimo: significa siempre país sin resolver, y contra eso lo correcto es no validar.
        Assert.Equal(esperado, InventarioConsumoGate.ResolverModelo(paisId));
        Assert.Null(ReservaSeguimientoCalculos.MotivoAlimentoNoAplicable(
            esperado, kg: 120m, paisId, loteRef: "A374A"));
    }

    // ─── Aves: validar no puede recortar, porque des-validar no recorta ──────
    // El descuento de aves recorta en cero y ese clamp es histórico (no se toca: movería saldos de
    // todas las empresas). Pero el clamp hace la operación NO reversible, así que la doble validación
    // se niega a validar cuando el saldo no alcanza. Verificado en runtime: un lote en 0 quedó en 5
    // después de validar y des-validar.

    [Fact]
    public void SaldoInsuficiente_NoSePuedeValidar()
    {
        var motivo = ReservaSeguimientoCalculos.MotivoAvesNoAplicable(
            disponibleTotal: 0, bajasTotal: 5, loteRef: "LOTE 235A");

        Assert.NotNull(motivo);
        Assert.Contains("LOTE 235A", motivo);
        Assert.Contains("5", motivo);
    }

    [Fact]
    public void SaldoJusto_SePuedeValidar()
    {
        // El borde exacto sí pasa: descontar las últimas aves del lote es legítimo.
        Assert.Null(ReservaSeguimientoCalculos.MotivoAvesNoAplicable(5, 5, "A374A"));
        Assert.Null(ReservaSeguimientoCalculos.MotivoAvesNoAplicable(5_000, 150, "A374A"));
    }

    [Fact]
    public void DiaSinBajas_NoReclamaSaldo()
    {
        // Un registro que solo carga alimento no tiene por qué exigir aves.
        Assert.Null(ReservaSeguimientoCalculos.MotivoAvesNoAplicable(0, 0, "A374A"));
    }

    [Fact]
    public void ElClampEsLoQueRompeLaReversibilidad()
    {
        // Documenta el porqué del guard, con la aritmética a la vista: saldo 0, bajas 5.
        // Validar recorta a 0; des-validar suma 5 sin recortar ⇒ el lote gana 5 aves que no existen.
        const int saldo = 0, bajas = 5;

        var trasValidar = DescuentoAvesSeguimientoCalculos.AplicarDelta(saldo, -bajas);
        var trasDesvalidar = DescuentoAvesSeguimientoCalculos.AplicarDelta(trasValidar, bajas);

        Assert.Equal(0, trasValidar);
        Assert.Equal(5, trasDesvalidar);
        Assert.NotEqual(saldo, trasDesvalidar);   // el número infló: por eso se rechaza antes

        Assert.NotNull(ReservaSeguimientoCalculos.MotivoAvesNoAplicable(saldo, bajas, "LOTE 235A"));
    }

    [Fact]
    public void ConSaldoSuficiente_ValidarYDesvalidarSonReversibles()
    {
        const int saldo = 7_544, bajas = 5;

        var trasValidar = DescuentoAvesSeguimientoCalculos.AplicarDelta(saldo, -bajas);
        var trasDesvalidar = DescuentoAvesSeguimientoCalculos.AplicarDelta(trasValidar, bajas);

        Assert.Equal(7_539, trasValidar);
        Assert.Equal(saldo, trasDesvalidar);
        Assert.Null(ReservaSeguimientoCalculos.MotivoAvesNoAplicable(saldo, bajas, "A374A"));
    }

    [Fact]
    public void FlagApagado_NoHayReservasYNadaQueAplicar()
    {
        // Sin doble validación no se separa nada, así que la aplicabilidad ni se consulta: el
        // comportamiento previo queda intacto. Se fija acá para que el invariante no dependa de que
        // alguien recuerde no llamar al método.
        Assert.False(ValidacionSeguimientoCalculos.SeparaAlGuardar(empresaRequiereValidacion: false));
        Assert.True(ValidacionSeguimientoCalculos.DescuentaAlGuardar(empresaRequiereValidacion: false));
    }

    // ─── Despacho contra el maestro (venta / traslado de postura) ─────────────

    [Fact]
    public void SinReservas_ElDespachoNuncaSeBloquea_AunquePidaDeMas()
    {
        // El invariante que hace seguro meter el guard en un camino que ya estaba en produccion:
        // con el flag apagado no hay reservas, asi que el resultado es identico al de hoy. El pedido
        // mayor que el saldo es un caso PREEXISTENTE y no es lo que este guard vino a resolver.
        Assert.Null(ReservaSeguimientoCalculos.MotivoDespachoNoDisponible(
            saldoHembras: 100, saldoMachos: 10,
            reservadasHembras: 0, reservadasMachos: 0,
            pedidasHembras: 500, pedidasMachos: 500,
            loteRef: "A374A"));
    }

    [Fact]
    public void ConReservas_ElDespachoQueEntraEnLoDisponible_Pasa()
    {
        Assert.Null(ReservaSeguimientoCalculos.MotivoDespachoNoDisponible(
            saldoHembras: 100, saldoMachos: 20,
            reservadasHembras: 30, reservadasMachos: 5,
            pedidasHembras: 70, pedidasMachos: 15,
            loteRef: "A374A"));
    }

    [Fact]
    public void ConReservas_ElDespachoQueSeComeLoSeparado_SeRechaza()
    {
        var motivo = ReservaSeguimientoCalculos.MotivoDespachoNoDisponible(
            saldoHembras: 100, saldoMachos: 20,
            reservadasHembras: 30, reservadasMachos: 0,
            pedidasHembras: 71, pedidasMachos: 0,
            loteRef: "A374A");

        Assert.NotNull(motivo);
        Assert.Contains("71", motivo);
        Assert.Contains("70", motivo);   // el disponible real: 100 - 30
        Assert.Contains("A374A", motivo);
    }

    [Fact]
    public void LaReservaDeUnSexoNoHabilitaNiBloqueaAlOtro()
    {
        // Se evalua por sexo: 30 hembras separadas no pueden frenar un despacho de machos que si hay,
        // ni tapar uno de hembras que no.
        Assert.Null(ReservaSeguimientoCalculos.MotivoDespachoNoDisponible(
            saldoHembras: 100, saldoMachos: 20,
            reservadasHembras: 30, reservadasMachos: 0,
            pedidasHembras: 0, pedidasMachos: 20,
            loteRef: null));

        Assert.NotNull(ReservaSeguimientoCalculos.MotivoDespachoNoDisponible(
            saldoHembras: 100, saldoMachos: 20,
            reservadasHembras: 0, reservadasMachos: 5,
            pedidasHembras: 0, pedidasMachos: 16,
            loteRef: null));
    }

    [Fact]
    public void ElDisponibleNegativo_BloqueaCualquierDespacho()
    {
        // Se separo mas de lo que hay (dos registros pendientes sobre el mismo lote): el disponible es
        // negativo y no se recorta a cero, asi que ni una sola ave puede salir.
        var motivo = ReservaSeguimientoCalculos.MotivoDespachoNoDisponible(
            saldoHembras: 40, saldoMachos: 0,
            reservadasHembras: 500, reservadasMachos: 0,
            pedidasHembras: 1, pedidasMachos: 0,
            loteRef: "LOTE 235A");

        Assert.NotNull(motivo);
        Assert.Equal(-460, ReservaSeguimientoCalculos.DisponibleAves(40, 500));
    }

    // ─── Referencia de los movimientos de inventario (V7.27) ──────────────────
    //
    // Estos tests no cuidan una cadena bonita: cuidan una CLAVE DE LECTURA. El saldo de alimento de
    // engorde y sus nueve espejos deciden si un INV_INGRESO es «alimento que entró al galpón» o
    // «reversión contable de un consumo» comparando la referencia contra el literal que escribe el
    // Crud del módulo. Cuando validar/desvalidar inventaba el suyo (`Seguimiento engorde #…`, armado
    // con modulo.ToLowerInvariant()), la devolución de una desvalidación entraba al saldo como
    // alimento nuevo: 500 kg devueltos ⇒ +500 kg de saldo y de ingreso_alimento_kg, medido sobre el
    // lote 168 de ItalcolPanama. Si alguien vuelve a tocar el literal, que falle acá.

    [Fact]
    public void ReferenciaDeEngorde_UsaElMismoLiteralQueSuCrud()
    {
        var r = ReservaSeguimientoCalculos.ReferenciaInventario(
            ModuloSeguimiento.Engorde, 123, new DateOnly(2026, 8, 15), devolver: false);

        Assert.Equal("Seguimiento aves engorde #123 2026-08-15 (validado)", r);
    }

    [Fact]
    public void ReferenciaDeEngorde_MatcheaElFiltroDeLaFuncionDelSaldo()
    {
        // `fn_seguimiento_diario_engorde` (y fn_cuadre_alimento_engorde, fn_reporte_diario_costos_engorde,
        // vw_seguimiento_pollo_engorde y las 7 consultas EF espejo) excluyen del saldo los INV_INGRESO
        // cuya referencia empieza con este prefijo. Es el filtro literal, copiado del SQL.
        const string filtroDeLaFn = "Seguimiento aves engorde #";

        var devolucion = ReservaSeguimientoCalculos.ReferenciaInventario(
            ModuloSeguimiento.Engorde, 999, new DateOnly(2026, 8, 15), devolver: true);

        Assert.StartsWith(filtroDeLaFn, devolucion);
        Assert.EndsWith("(devolución por quitar la validación)", devolucion);
    }

    [Fact]
    public void EngordeEcuador_EscribeExactamenteLoMismoQueEngorde()
    {
        // Los dos services escriben la MISMA tabla: dos referencias distintas partirían en dos la
        // atribución de un mismo lote según qué formulario lo cargó.
        var pa = ReservaSeguimientoCalculos.ReferenciaInventario(
            ModuloSeguimiento.Engorde, 7, new DateOnly(2026, 1, 2), devolver: false);
        var ec = ReservaSeguimientoCalculos.ReferenciaInventario(
            ModuloSeguimiento.EngordeEcuador, 7, new DateOnly(2026, 1, 2), devolver: false);

        Assert.Equal(pa, ec);
    }

    [Theory]
    // Cada prefijo es el que escribe el Crud del módulo, verificado contra el código y contra las
    // filas que ya existen en el histórico unificado.
    [InlineData(ModuloSeguimiento.Levante,      "Seguimiento lote levante #")]
    [InlineData(ModuloSeguimiento.Produccion,   "Seguimiento producción #")]   // con tilde
    [InlineData(ModuloSeguimiento.Reproductora, "Seguimiento reproductora #")]
    [InlineData(ModuloSeguimiento.Engorde,      "Seguimiento aves engorde #")]
    public void CadaModuloUsaElPrefijoDeSuCrud(string modulo, string prefijoEsperado)
    {
        var r = ReservaSeguimientoCalculos.ReferenciaInventario(
            modulo, 42, new DateOnly(2026, 3, 4), devolver: false);

        Assert.StartsWith(prefijoEsperado, r);
    }

    [Fact]
    public void LaFechaEsLaDelSeguimiento_YSiempreEnFormatoInvariante()
    {
        // El movimiento se fecha en el día del seguimiento, no en el de la validación: validar cinco
        // días juntos tiene que dejar cinco consumos, uno por día, en el kardex del galpón. Y el
        // formato no puede depender de la cultura del servidor: la referencia se compara por texto.
        var anterior = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-AR");
            var r = ReservaSeguimientoCalculos.ReferenciaInventario(
                ModuloSeguimiento.Engorde, 1, new DateOnly(2026, 12, 31), devolver: false);

            Assert.Contains("2026-12-31", r);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = anterior;
        }
    }
}
