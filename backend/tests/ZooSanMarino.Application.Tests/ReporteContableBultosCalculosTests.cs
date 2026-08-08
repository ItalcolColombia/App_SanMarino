using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.ReporteContableBultosCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Sección BULTO del Reporte Contable (plan
/// <c>fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md</c>, parte C1).
/// <para>
/// Hasta ago-2026 una fecha con movimientos de alimento y sin dato del lote se descartaba
/// (<c>continue</c> en <c>ReporteContableService</c>) y esos bultos desaparecían del reporte y del
/// saldo. El gate de este fix es doble: la fecha solo-bultos ahora genera fila, y todo lo demás
/// —incluido el saldo acumulado— queda idéntico a como estaba.
/// </para>
/// </summary>
public class ReporteContableBultosCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 3, 10);

    // Ventana de la empresa por defecto (companies.dias_alimento_previo_encaset = 10)
    private static (DateTime Desde, DateTime Hasta) VentanaPorDefecto() =>
        Ventana(Encaset, diasPreviosEmpresa: null, fechaInicioFiltro: null,
                fechaFinReporte: new DateTime(2026, 4, 30));

    private static MovimientoBultosDia Entrada(DateTime fecha, decimal bultos) =>
        new(fecha, Traslados: 0m, Entradas: bultos, Retiros: 0m);

    // ───────────────────────── ¿esta fecha genera fila? ─────────────────────────

    [Fact]
    public void FechaConEntradaDeBultos_YSinDatoDelLote_GeneraFila()
    {
        var (desde, hasta) = VentanaPorDefecto();

        Assert.True(GeneraFilaSoloBultos(
            Entrada(new DateTime(2026, 3, 15), 100m), elPadreYaTieneFila: false, desde, hasta));
    }

    [Fact]
    public void FechaSinNingunMovimiento_SigueSinFila()
    {
        // Fecha presente en el kardex pero con las tres columnas en cero: comportamiento de HOY.
        var (desde, hasta) = VentanaPorDefecto();
        var sinMovimiento = new MovimientoBultosDia(new DateTime(2026, 3, 15), 0m, 0m, 0m);

        Assert.False(TieneMovimiento(sinMovimiento));
        Assert.False(GeneraFilaSoloBultos(sinMovimiento, elPadreYaTieneFila: false, desde, hasta));
    }

    [Fact]
    public void SiElPadreYaTieneFilaEseDia_NoSeDuplica()
    {
        // La fila propia del lote YA lleva los bultos (comportamiento histórico intacto).
        var (desde, hasta) = VentanaPorDefecto();

        Assert.False(GeneraFilaSoloBultos(
            Entrada(new DateTime(2026, 3, 15), 100m), elPadreYaTieneFila: true, desde, hasta));
    }

    [Theory]
    // La ventana por defecto abre 10 días antes del encaset (2026-03-10) y cierra con el reporte.
    [InlineData("2026-02-27", false)] // un día antes de la ventana: fuera (movimiento de otro ciclo)
    [InlineData("2026-02-28", true)]  // 10 días antes: dentro (inclusive)
    [InlineData("2026-03-09", true)]  // víspera del encaset: dentro
    [InlineData("2026-04-30", true)]  // último día del reporte: dentro
    [InlineData("2026-05-01", false)] // después del reporte: fuera
    public void LaVentanaAcotaQueFechasPuedenGenerarFila(string fecha, bool esperado)
    {
        var (desde, hasta) = VentanaPorDefecto();

        Assert.Equal(esperado, GeneraFilaSoloBultos(
            Entrada(DateTime.Parse(fecha), 100m), elPadreYaTieneFila: false, desde, hasta));
    }

    [Fact]
    public void LaVentanaUsaLosDiasDeLaEmpresaYSeRecortaConElFiltroDelUsuario()
    {
        // Sin configurar → default 10 días; configurado 30 → 30; por encima del tope → tope.
        Assert.Equal(new DateTime(2026, 2, 28),
            Ventana(Encaset, null, null, new DateTime(2026, 4, 30)).Desde);
        Assert.Equal(new DateTime(2026, 2, 8),
            Ventana(Encaset, 30, null, new DateTime(2026, 4, 30)).Desde);
        Assert.Equal(new DateTime(2026, 2, 8),
            Ventana(Encaset, 999, null, new DateTime(2026, 4, 30)).Desde);

        // El rango pedido por el usuario recorta la ventana, nunca la extiende.
        Assert.Equal(new DateTime(2026, 3, 20),
            Ventana(Encaset, null, new DateTime(2026, 3, 20), new DateTime(2026, 4, 30)).Desde);
        Assert.Equal(new DateTime(2026, 2, 28),
            Ventana(Encaset, null, new DateTime(2026, 1, 1), new DateTime(2026, 4, 30)).Desde);
    }

    // ───────────────────────── pertenencia a la semana contable ─────────────────────────

    [Fact]
    public void LaPrimeraSemanaAbsorbeLaFilaSoloBultosAnteriorAlEncaset()
    {
        // Semana 1 = [encaset, encaset+6]. Una entrada del 05-mar no cae en ninguna semana salvo que
        // la primera la absorba; sin eso volvería a desaparecer del reporte.
        var inicio = Encaset;
        var fin = Encaset.AddDays(6);
        var previa = new DateTime(2026, 3, 5);

        Assert.False(PerteneceASemana(previa, inicio, fin, absorbeAnteriores: false));
        Assert.True(PerteneceASemana(previa, inicio, fin, absorbeAnteriores: true));

        // La absorción nunca extiende el borde superior.
        Assert.False(PerteneceASemana(fin.AddDays(1), inicio, fin, absorbeAnteriores: true));

        // Y las filas dentro de la semana no cambian de comportamiento.
        Assert.True(PerteneceASemana(inicio, inicio, fin, absorbeAnteriores: false));
        Assert.True(PerteneceASemana(fin, inicio, fin, absorbeAnteriores: false));
    }

    // ───────────────────────── saldo acumulado ─────────────────────────

    /// <summary>
    /// Implementación LEGADA (la que vivía en <c>CalcularSaldosAcumulativos</c> antes de la
    /// extracción), copiada tal cual para comparar contra ella. Es la especificación ejecutable del
    /// "byte a byte": si <see cref="AcumularSaldos"/> se desvía de esto, el test cae.
    /// </summary>
    private static List<(decimal SaldoAnterior, decimal Saldo)> AcumuladorLegado(
        List<(DateTime Fecha, DeltaBultosFila Delta)> filas)
    {
        var salida = new List<(decimal, decimal)>();
        var saldoBultosPorFecha = new Dictionary<DateTime, decimal>();
        decimal saldoBultosAcumulado = 0;

        foreach (var grupoFecha in filas.GroupBy(f => f.Fecha).OrderBy(g => g.Key))
        {
            var fecha = grupoFecha.Key;
            var datosFecha = grupoFecha.ToList();
            var fechaAnterior = fecha.AddDays(-1);

            if (saldoBultosPorFecha.ContainsKey(fechaAnterior))
                saldoBultosAcumulado = saldoBultosPorFecha[fechaAnterior];

            foreach (var (_, d) in datosFecha)
            {
                saldoBultosAcumulado = saldoBultosAcumulado
                    + d.Entradas - d.Traslados - d.Retiros - d.ConsumoHembras - d.ConsumoMachos;

                salida.Add((saldoBultosPorFecha.GetValueOrDefault(fechaAnterior, 0),
                            Math.Max(0, saldoBultosAcumulado)));
            }

            if (datosFecha.Any())
                saldoBultosPorFecha[fecha] = Math.Max(0, saldoBultosAcumulado);
        }

        return salida;
    }

    private static DeltaBultosFila Consumo(decimal hembras, decimal machos = 0m) =>
        new(Entradas: 0m, Traslados: 0m, Retiros: 0m, ConsumoHembras: hembras, ConsumoMachos: machos);

    private static DeltaBultosFila EntradaDelta(decimal bultos) =>
        new(Entradas: bultos, Traslados: 0m, Retiros: 0m, ConsumoHembras: 0m, ConsumoMachos: 0m);

    /// <summary>Escenario sin fechas solo-bultos: exactamente lo que el reporte producía antes.</summary>
    private static List<(DateTime Fecha, DeltaBultosFila Delta)> EscenarioSinSoloBultos() => new()
    {
        (Encaset,                new DeltaBultosFila(1250m, 0m, 0m, 0m, 0m)),  // entrada el día del encaset
        (Encaset,                Consumo(12.5m, 3.2m)),                        // sublote, misma fecha
        (Encaset.AddDays(1),     Consumo(13m, 3.4m)),
        (Encaset.AddDays(2),     Consumo(14m, 3.6m)),
        (Encaset.AddDays(9),     Consumo(20m, 5m)),                            // hueco de fechas
        (Encaset.AddDays(10),    new DeltaBultosFila(0m, 5m, 7m, 21m, 5.2m)),  // traslado + retiro
    };

    [Fact]
    public void SinFechasSoloBultos_ElSaldoAcumuladoEsIdenticoAlLegado()
    {
        var filas = EscenarioSinSoloBultos();

        var nuevo = AcumularSaldos(filas).Select(s => (s.SaldoAnterior, s.Saldo)).ToList();

        Assert.Equal(AcumuladorLegado(filas), nuevo);
    }

    [Fact]
    public void ConFechaSoloBultosIntercalada_ElSaldoSigueSiendoElDelLegado()
    {
        // La fila nueva no necesita un algoritmo nuevo: entra al mismo acumulador, en su fecha.
        var filas = EscenarioSinSoloBultos();
        filas.Insert(0, (Encaset.AddDays(-5), EntradaDelta(300m)));

        var nuevo = AcumularSaldos(filas.OrderBy(f => f.Fecha).ToList())
            .Select(s => (s.SaldoAnterior, s.Saldo)).ToList();

        Assert.Equal(AcumuladorLegado(filas), nuevo);
    }

    [Fact]
    public void EntradaPreviaAlEncaset_ApareceYElSaldoArrancaContandola()
    {
        var previa = Encaset.AddDays(-5);
        var (desde, hasta) = VentanaPorDefecto();

        // 1) la fecha previa genera fila
        Assert.True(GeneraFilaSoloBultos(Entrada(previa, 300m), elPadreYaTieneFila: false, desde, hasta));

        // 2) el saldo la cuenta desde el primer día
        var saldos = AcumularSaldos(new[]
        {
            (previa,             EntradaDelta(300m)),
            (Encaset,            Consumo(12.5m, 3.2m)),
            (Encaset.AddDays(1), Consumo(13m, 3.4m)),
        });

        Assert.Equal(300m, saldos[0].Saldo);                  // día de la llegada real
        Assert.Equal(0m, saldos[0].SaldoAnterior);
        Assert.Equal(300m - 12.5m - 3.2m, saldos[1].Saldo);   // el encaset arranca CON el alimento
        Assert.Equal(300m - 12.5m - 3.2m - 13m - 3.4m, saldos[2].Saldo);

        // 3) sin la fila previa, el mismo lote arrancaba en 0 y quedaba en negativo → clampeado a 0
        var sinPrevia = AcumularSaldos(new[]
        {
            (Encaset,            Consumo(12.5m, 3.2m)),
            (Encaset.AddDays(1), Consumo(13m, 3.4m)),
        });
        Assert.Equal(0m, sinPrevia[0].Saldo);
        Assert.Equal(0m, sinPrevia[1].Saldo);
    }

    [Fact]
    public void MultiFecha_ElSaldoSeIntegraEnOrdenCronologicoYRetomaElDiaAnterior()
    {
        var d1 = new DateTime(2026, 3, 1);

        var saldos = AcumularSaldos(new[]
        {
            (d1,               EntradaDelta(100m)),
            (d1.AddDays(1),    Consumo(10m)),        // día contiguo: retoma el saldo del día anterior
            (d1.AddDays(1),    Consumo(5m)),         // segunda fila de la MISMA fecha: sigue acumulando
            (d1.AddDays(5),    Consumo(20m)),        // hueco: continúa con el acumulado que venía
            (d1.AddDays(6),    EntradaDelta(50m)),
        });

        Assert.Equal(new[] { 100m, 90m, 85m, 65m, 115m }, saldos.Select(s => s.Saldo).ToArray());
        // SaldoAnterior solo existe cuando el DÍA calendario anterior tiene filas (regla histórica).
        Assert.Equal(new[] { 0m, 100m, 100m, 0m, 65m }, saldos.Select(s => s.SaldoAnterior).ToArray());
    }

    [Fact]
    public void ElSaldoPublicadoTienePisoCeroPeroElAcumuladorConservaElNegativo()
    {
        var d1 = new DateTime(2026, 3, 1);

        var saldos = AcumularSaldos(new[]
        {
            (d1,            Consumo(30m)),        // -30 → se publica 0
            (d1.AddDays(2), EntradaDelta(10m)),   // hueco: -30 + 10 = -20 → se publica 0
            (d1.AddDays(3), EntradaDelta(40m)),   // día contiguo: retoma el 0 publicado → 40
        });

        Assert.Equal(new[] { 0m, 0m, 40m }, saldos.Select(s => s.Saldo).ToArray());
    }

    [Fact]
    public void SinFilas_NoRompe()
    {
        Assert.Empty(AcumularSaldos(Array.Empty<(DateTime, DeltaBultosFila)>()));
    }
}
