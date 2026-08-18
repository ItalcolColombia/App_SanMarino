using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato ejecutable de la atribución del alimento marcado «para el próximo ciclo» — FASE B del plan
/// <c>fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md</c>.
/// <para>
/// El helper <see cref="Galpon"/> construye un galpón COMPLETO (ciclos con encaset, primer y último
/// seguimiento, y si están congelados), que es la única forma de expresar «destino sin seguimiento»,
/// «cedente sin respaldo», «ciclos que conviven» o «extremo liquidado». El intento anterior falló
/// justamente por probar predicados sueltos: 17 tests verdes sobre un espejo C# que ningún llamador de
/// producción alcanzaba.
/// </para>
/// <para>
/// ⚠️ Estos tests son la compuerta de la <b>atribución</b>, no la del saldo: <c>pt_calc</c> no tiene
/// espejo C#. La compuerta del saldo es el gate SQL (<c>verificar_entrega_ciclo_engorde.sql</c>).
/// </para>
/// </summary>
public class EntregaAlimentoCicloEngordeCalculosTests
{
    private const int DiasPrevios = 10;

    private static CicloGalponEngorde Ciclo(
        int loteId,
        string encaset,
        string? segMin = null,
        string? segMax = null,
        bool congelado = false)
        => new(loteId,
               DateTime.Parse(encaset),
               segMin is null ? null : DateTime.Parse(segMin),
               segMax is null ? null : DateTime.Parse(segMax),
               congelado);

    private static CicloGalponEngorde[] Galpon(params CicloGalponEngorde[] ciclos) => ciclos;

    private static MovimientoMarcadoEngorde Mov(
        string fecha,
        decimal kg = 3000m,
        bool esEntrada = true,
        bool anulado = false,
        bool tieneGalpon = true)
        => new(DateTime.Parse(fecha), kg, esEntrada, anulado, tieneGalpon);

    private static AtribucionEntregaEngorde Clasificar(MovimientoMarcadoEngorde m, CicloGalponEngorde[] g)
        => EntregaAlimentoCicloEngordeCalculos.Clasificar(m, g, DiasPrevios);

    // Cadena secuencial con HUECO, que es el caso real del feature: el ciclo 1 termina el 20-mar y el
    // ciclo 2 encaseta el 01-may (ventana desde el 21-abr), asi que un ingreso del 05-abr cae en el
    // hueco Y antes de la ventana: nadie lo ve como propio.
    private static CicloGalponEngorde[] CadenaConHueco() => Galpon(
        Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20"),
        Ciclo(70, "2026-05-01", "2026-05-03", "2026-06-20"));

    // ─── Caso 2 — el camino feliz: secuencial con destino operativo ──────────────────────────────

    [Fact]
    public void Caso2_IngresoEnElHuecoYAntesDeLaVentana_EsVIGENTE()
    {
        var r = Clasificar(Mov("2026-04-05"), CadenaConHueco());

        Assert.Equal(EstadoEntregaAlimentoCiclo.Vigente, r.Estado);
        Assert.Equal(53, r.LoteCedenteId);
        Assert.Equal(70, r.LoteDestinoId);
        // El ultimo dia visible del cedente: el anterior al arranque del destino.
        Assert.Equal(new DateTime(2026, 5, 2), r.FechaEntrega);
    }

    [Fact]
    public void Caso2_LaFechaDeEntregaNuncaEsAnteriorAlMovimiento()
    {
        // Si lo fuera, el saldo del cedente bajaria ANTES de subir y la fila quedaria negativa. La
        // invariante se sostiene sola: VIGENTE exige d < destino.SegMin, y la entrega se escribe en
        // destino.SegMin - 1. Se prueba en el dia MAS TARDIO que puede dar VIGENTE (20-abr: el 21 ya
        // lo alcanza la ventana previa al encaset).
        foreach (var f in new[] { "2026-03-21", "2026-04-05", "2026-04-20" })
        {
            var r = Clasificar(Mov(f), CadenaConHueco());

            Assert.Equal(EstadoEntregaAlimentoCiclo.Vigente, r.Estado);
            Assert.True(r.FechaEntrega!.Value >= DateTime.Parse(f), $"la entrega del {f} quedo fechada antes del movimiento");
        }
    }

    // ─── Casos 3, 4 y 9 — PENDIENTE: hay intencion, todavia no hay a quien entregarle ────────────

    [Fact]
    public void Caso4_SinNingunCicloPosterior_QuedaPENDIENTE()
    {
        var r = Clasificar(Mov("2026-04-05"), Galpon(Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20")));

        Assert.Equal(EstadoEntregaAlimentoCiclo.Pendiente, r.Estado);
        Assert.Equal(53, r.LoteCedenteId);
        Assert.Null(r.LoteDestinoId);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoSinDestino, r.Motivo);
    }

    [Fact]
    public void Caso3_DestinoSinSeguimientoTodavia_QuedaPENDIENTE_NoVIGENTE()
    {
        var r = Clasificar(Mov("2026-04-05"), Galpon(
            Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20"),
            Ciclo(70, "2026-05-01")));

        Assert.Equal(EstadoEntregaAlimentoCiclo.Pendiente, r.Estado);
        Assert.Equal(70, r.LoteDestinoId);
        Assert.Equal(0m, r.KgEntregados);
    }

    [Fact]
    public void Caso9_CedenteSinSeguimiento_QuedaPENDIENTE()
    {
        // 96/PA-67: los 4 lotes del galpon nunca cargaron seguimiento. Sin rango del cedente no hay
        // dia donde escribir la entrega.
        var r = Clasificar(Mov("2026-04-05"), Galpon(
            Ciclo(119, "2026-01-07"),
            Ciclo(120, "2026-03-15"),
            Ciclo(121, "2026-05-17")));

        Assert.Equal(EstadoEntregaAlimentoCiclo.Pendiente, r.Estado);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoCedenteSinSeguimiento, r.Motivo);
    }

    [Fact]
    public void SinCedente_ElAlimentoLlegoAntesDelPrimerEncaset_QuedaPENDIENTE()
    {
        var r = Clasificar(Mov("2026-01-01"), CadenaConHueco());

        Assert.Equal(EstadoEntregaAlimentoCiclo.Pendiente, r.Estado);
        Assert.Null(r.LoteCedenteId);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoSinCedente, r.Motivo);
    }

    // ─── Caso 1 (R1) — CONVIVENCIA: el alimento es de los DOS ────────────────────────────────────

    [Fact]
    public void Caso1_R1_CiclosQueConviven_EsINERTE()
    {
        // 105/G0491: lotes 175 y 176 solapados. Es la topologia de Panama, y es la que los 4 guards de
        // la v15 rompian: le quitaban el movimiento a los dos.
        var r = Clasificar(Mov("2026-07-18"), Galpon(
            Ciclo(175, "2026-07-17", "2026-07-16", "2026-07-27"),
            Ciclo(176, "2026-07-20", "2026-07-19", "2026-07-27")));

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(0m, r.KgEntregados);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoConvivencia, r.Motivo);
    }

    [Fact]
    public void R1_LaConvivenciaTienePRECEDENCIASobreElMotivoDelCedente()
    {
        // Dos ciclos que conviven cumplen ADEMAS «el movimiento cae dentro del rango del cedente»: las
        // dos guardas dan INERTE, pero la que explica de verdad lo que pasa es R1. Si este test se
        // pone en rojo es que alguien reordeno las guardas y la bandeja empezo a mentir.
        var r = Clasificar(Mov("2026-07-18"), Galpon(
            Ciclo(175, "2026-07-17", "2026-07-16", "2026-07-27"),
            Ciclo(176, "2026-07-20", "2026-07-19", "2026-07-27")));

        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoConvivencia, r.Motivo);
        Assert.NotEqual(EntregaAlimentoCicloEngordeCalculos.MotivoDentroDelCedente, r.Motivo);
    }

    [Fact]
    public void R1_ConvivenEsSimetrico()
    {
        var a = Ciclo(175, "2026-07-17", "2026-07-16", "2026-07-27");
        var b = Ciclo(176, "2026-07-20", "2026-07-19", "2026-07-27");

        Assert.True(EntregaAlimentoCicloEngordeCalculos.Conviven(a, b));
        Assert.True(EntregaAlimentoCicloEngordeCalculos.Conviven(b, a));
    }

    [Fact]
    public void R1_UnCicloSinSeguimientoNuncaConvive()
    {
        var conSeg = Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20");
        var sinSeg = Ciclo(70, "2026-02-01");

        Assert.False(EntregaAlimentoCicloEngordeCalculos.Conviven(conSeg, sinSeg));
        Assert.False(EntregaAlimentoCicloEngordeCalculos.Conviven(sinSeg, conSeg));
    }

    // ─── Casos 5 y 5b — extremo LIQUIDADO: una foto congelada no se reescribe ────────────────────

    [Fact]
    public void Caso5_DestinoLiquidado_EsINERTE()
    {
        var r = Clasificar(Mov("2026-04-05"), Galpon(
            Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20"),
            Ciclo(70, "2026-05-01", "2026-05-03", "2026-06-20", congelado: true)));

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoDestinoCongelado, r.Motivo);
    }

    [Fact]
    public void Caso5b_CedenteLiquidado_EsINERTE_PorqueNoPodriaEmitirLaSalida()
    {
        // Sin contraparte del lado del cedente, el destino recibiria kg de la nada: suma != 0. Es el
        // bloqueante 2 del NO-GO (Sigma galpon 8.640 -> 11.640, +3.000 kg creados).
        var r = Clasificar(Mov("2026-04-05"), Galpon(
            Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20", congelado: true),
            Ciclo(70, "2026-05-01", "2026-05-03", "2026-06-20")));

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoCedenteCongelado, r.Motivo);
    }

    // ─── Caso 6 — el TOPE: no se puede entregar lo que ya se comio ───────────────────────────────

    [Fact]
    public void Caso6_ConRespaldoDeSobra_EntregaTodoYNoDejaResiduo()
    {
        var clas = Clasificar(Mov("2026-04-05", kg: 3000m), CadenaConHueco());
        var r = EntregaAlimentoCicloEngordeCalculos.AplicarTope(clas, 3000m, saldoCedenteEnFechaEntrega: 4100m);

        Assert.Equal(EstadoEntregaAlimentoCiclo.Vigente, r.Estado);
        Assert.Equal(3000m, r.KgEntregados);
        Assert.Equal(0m, r.KgNoDiferible);
    }

    [Fact]
    public void Caso6_ConRespaldoParcial_EntregaLoQueHayYSENALAElResto()
    {
        var clas = Clasificar(Mov("2026-04-05", kg: 3000m), CadenaConHueco());
        var r = EntregaAlimentoCicloEngordeCalculos.AplicarTope(clas, 3000m, saldoCedenteEnFechaEntrega: 1100m);

        Assert.Equal(EstadoEntregaAlimentoCiclo.Vigente, r.Estado);
        Assert.Equal(1100m, r.KgEntregados);
        Assert.Equal(1900m, r.KgNoDiferible);   // R2: se senala, no se compensa
    }

    [Fact]
    public void Caso6_SinNadaQueEntregar_DegradaAINERTE_YNoEscribeFechaNiKg()
    {
        var clas = Clasificar(Mov("2026-04-05", kg: 3000m), CadenaConHueco());
        var r = EntregaAlimentoCicloEngordeCalculos.AplicarTope(clas, 3000m, saldoCedenteEnFechaEntrega: 0m);

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(0m, r.KgEntregados);
        Assert.Equal(3000m, r.KgNoDiferible);
        Assert.Null(r.FechaEntrega);
    }

    [Fact]
    public void Tope_SaldoNegativoSeTrataComoCero_NuncaEntregaDeMas()
    {
        var (entregados, noDiferible) = EntregaAlimentoCicloEngordeCalculos.TopeEntrega(3000m, -8840m);

        Assert.Equal(0m, entregados);
        Assert.Equal(3000m, noDiferible);
    }

    [Fact]
    public void Tope_NoSeAplicaSiLaClasificacionNoEsVIGENTE()
    {
        var clas = Clasificar(Mov("2026-04-05"), Galpon(Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20")));
        var r = EntregaAlimentoCicloEngordeCalculos.AplicarTope(clas, 3000m, saldoCedenteEnFechaEntrega: 99999m);

        Assert.Equal(EstadoEntregaAlimentoCiclo.Pendiente, r.Estado);
        Assert.Equal(0m, r.KgEntregados);
    }

    // ─── Casos 7, 8 y 11 — el movimiento no es atribuible ────────────────────────────────────────

    [Fact]
    public void Caso7_MovimientoAnulado_QuedaANULADA()
    {
        var r = Clasificar(Mov("2026-04-05", anulado: true), CadenaConHueco());

        Assert.Equal(EstadoEntregaAlimentoCiclo.Anulada, r.Estado);
        Assert.Equal(0m, r.KgEntregados);
    }

    [Fact]
    public void Caso8_SalidaMarcada_EsINERTE_NoUnCreditoNegativo()
    {
        // Defecto vivo de la v15: incluia INV_TRASLADO_SALIDA en el disyunto de la marca, asi que una
        // salida marcada entraba a la apertura del destino como delta NEGATIVO.
        var r = Clasificar(Mov("2026-04-05", esEntrada: false), CadenaConHueco());

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoNoEsEntrada, r.Motivo);
    }

    [Fact]
    public void Caso11_MovimientoSinGalpon_EsINERTE()
    {
        var r = Clasificar(Mov("2026-04-05", tieneGalpon: false), CadenaConHueco());

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoSinGalpon, r.Motivo);
    }

    // ─── Caso 10 y el estado extra «ya visible en el destino» ────────────────────────────────────

    [Fact]
    public void Caso10_ElMovimientoCaeDentroDelRangoDelDestino_EsINERTE()
    {
        // `SegMin` puede PRECEDER al encaset (lote 175). Si el destino ya lo ve como fila propia,
        // diferirlo lo haria desaparecer de esa fila.
        var r = Clasificar(Mov("2026-05-04"), Galpon(
            Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20"),
            Ciclo(70, "2026-05-05", "2026-05-03", "2026-06-20")));

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoDentroDelDestino, r.Motivo);
    }

    [Fact]
    public void YaVisibleEnLaAperturaNaturalDelDestino_EsINERTE_OSeContariaDosVeces()
    {
        // Encaset 01-may, ventana de 10 dias => desde el 21-abr el destino YA lo toma por fecha.
        // Diferirlo ademas lo sumaria dos veces: es lo que mantiene la conservacion en 0,00.
        var r = Clasificar(Mov("2026-04-25"), CadenaConHueco());

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoYaVisibleEnDestino, r.Motivo);
    }

    [Fact]
    public void ElBordeDeLaVentana_ElPrimerDiaQueLaVentanaAlcanzaYaEsINERTE()
    {
        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, Clasificar(Mov("2026-04-21"), CadenaConHueco()).Estado);
        Assert.Equal(EstadoEntregaAlimentoCiclo.Vigente, Clasificar(Mov("2026-04-20"), CadenaConHueco()).Estado);
    }

    // ─── El estado extra que nacio del NO-GO: dentro del ciclo CEDENTE ───────────────────────────

    [Fact]
    public void DentroDelCicloCedente_EsINERTE_AunqueHayaDestinoOperativo()
    {
        // 43/G0055: el lote 86 «cierra con 1.100 kg de saldo» que son un fantasma contable — el stock
        // fisico del galpon coincide EXACTO con el saldo del ciclo activo. Entregarlos movia el cuadre
        // de 1 a 2 galpones descuadrados.
        var r = Clasificar(Mov("2026-03-01"), CadenaConHueco());

        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, r.Estado);
        Assert.Equal(EntregaAlimentoCicloEngordeCalculos.MotivoDentroDelCedente, r.Motivo);
    }

    [Fact]
    public void ElBordeDelCedente_SuUltimoDiaDeSeguimientoTodaviaEsSuyo()
    {
        Assert.Equal(EstadoEntregaAlimentoCiclo.Inerte, Clasificar(Mov("2026-03-20"), CadenaConHueco()).Estado);
        Assert.Equal(EstadoEntregaAlimentoCiclo.Vigente, Clasificar(Mov("2026-03-21"), CadenaConHueco()).Estado);
    }

    // ─── Resolucion de extremos: los desempates ──────────────────────────────────────────────────

    [Fact]
    public void Cedente_EsElDeEncasetMasRecienteAnteriorOIgual()
    {
        var g = Galpon(
            Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20"),
            Ciclo(70, "2026-04-01", "2026-04-03", "2026-05-20"),
            Ciclo(189, "2026-07-30", "2026-07-31", "2026-08-07"));

        Assert.Equal(70, EntregaAlimentoCicloEngordeCalculos.ResolverCedente(g, new DateTime(2026, 6, 1))!.Value.LoteId);
        Assert.Equal(53, EntregaAlimentoCicloEngordeCalculos.ResolverCedente(g, new DateTime(2026, 3, 1))!.Value.LoteId);
    }

    [Fact]
    public void Cedente_ElDiaDelEncasetElGalponYaEsDelCicloNuevo()
    {
        var g = Galpon(
            Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20"),
            Ciclo(70, "2026-04-01", "2026-04-03", "2026-05-20"));

        Assert.Equal(70, EntregaAlimentoCicloEngordeCalculos.ResolverCedente(g, new DateTime(2026, 4, 1))!.Value.LoteId);
    }

    [Fact]
    public void Destino_EsElDeEncasetMinimoESTRICTAMENTEPosterior()
    {
        var g = Galpon(
            Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20"),
            Ciclo(70, "2026-04-01", "2026-04-03", "2026-05-20"),
            Ciclo(189, "2026-07-30", "2026-07-31", "2026-08-07"));

        Assert.Equal(70, EntregaAlimentoCicloEngordeCalculos.ResolverDestino(g, new DateTime(2026, 3, 25))!.Value.LoteId);
        Assert.Equal(189, EntregaAlimentoCicloEngordeCalculos.ResolverDestino(g, new DateTime(2026, 6, 1))!.Value.LoteId);
        Assert.Null(EntregaAlimentoCicloEngordeCalculos.ResolverDestino(g, new DateTime(2026, 8, 1)));
    }

    [Fact]
    public void Destino_ConDosEncasetsElMismoDiaGanaElDeIdMenor()
    {
        // Desempate deterministico: sin el, el mismo movimiento podria atribuirse a un lote distinto
        // en cada corrida y el hecho persistido dejaria de ser reproducible.
        var g = Galpon(
            Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20"),
            Ciclo(122, "2026-05-01", "2026-05-03", "2026-06-20"),
            Ciclo(121, "2026-05-01", "2026-05-03", "2026-06-20"));

        Assert.Equal(121, EntregaAlimentoCicloEngordeCalculos.ResolverDestino(g, new DateTime(2026, 4, 5))!.Value.LoteId);
    }

    // ─── Sellado: lo que impide reabrir el handoff partido ───────────────────────────────────────

    [Fact]
    public void Sellado_SiCualquieraDeLosDosExtremosEstaCongelado()
    {
        var congelados = new HashSet<int> { 70 };

        Assert.True(EntregaAlimentoCicloEngordeCalculos.DebeSellarse(53, 70, congelados));
        Assert.True(EntregaAlimentoCicloEngordeCalculos.DebeSellarse(70, 189, congelados));
        Assert.False(EntregaAlimentoCicloEngordeCalculos.DebeSellarse(53, 189, congelados));
    }

    [Fact]
    public void Sellado_UnaEntregaSelladaNoSePuedeAnular()
    {
        var sellada = new AlimentoEntregaCicloEngorde { Estado = EstadoEntregaAlimentoCiclo.Vigente, Sellada = true };
        var viva = new AlimentoEntregaCicloEngorde { Estado = EstadoEntregaAlimentoCiclo.Vigente, Sellada = false };
        var yaAnulada = new AlimentoEntregaCicloEngorde { Estado = EstadoEntregaAlimentoCiclo.Anulada, Sellada = false };

        Assert.False(EntregaAlimentoCicloEngordeCalculos.PuedeAnular(sellada));
        Assert.True(EntregaAlimentoCicloEngordeCalculos.PuedeAnular(viva));
        Assert.False(EntregaAlimentoCicloEngordeCalculos.PuedeAnular(yaAnulada));
    }

    // ─── Fail-closed (D3b): nada termina en VIGENTE por accidente ────────────────────────────────

    [Fact]
    public void FailClosed_NingunaTopologiaDegeneradaProduceVIGENTE()
    {
        var degeneradas = new[]
        {
            Galpon(),                                                        // galpon vacio
            Galpon(Ciclo(53, "2026-06-01")),                                 // solo un ciclo futuro, sin seg
            Galpon(Ciclo(53, "2026-01-25")),                                 // cedente sin seguimiento
            Galpon(Ciclo(53, "2026-01-25", "2026-01-27", "2026-03-20")),     // sin destino
        };

        foreach (var g in degeneradas)
            Assert.NotEqual(EstadoEntregaAlimentoCiclo.Vigente, Clasificar(Mov("2026-04-05"), g).Estado);
    }

    [Fact]
    public void FailClosed_TodoEstadoDistintoDeVIGENTEDejaLosKgEnCero()
    {
        var casos = new[]
        {
            Clasificar(Mov("2026-04-05", tieneGalpon: false), CadenaConHueco()),
            Clasificar(Mov("2026-04-05", anulado: true), CadenaConHueco()),
            Clasificar(Mov("2026-04-05", esEntrada: false), CadenaConHueco()),
            Clasificar(Mov("2026-03-01"), CadenaConHueco()),
            Clasificar(Mov("2026-04-25"), CadenaConHueco()),
        };

        foreach (var r in casos)
        {
            Assert.NotEqual(EstadoEntregaAlimentoCiclo.Vigente, r.Estado);
            Assert.Equal(0m, r.KgEntregados);
            Assert.Null(r.FechaEntrega);
            Assert.False(string.IsNullOrWhiteSpace(r.Motivo));
        }
    }

    [Fact]
    public void ElMotivoSiempreExplicaAlgo_NuncaSeDevuelveVacio()
    {
        var fechas = new[] { "2026-01-01", "2026-03-01", "2026-03-25", "2026-04-05", "2026-04-25", "2026-05-04", "2026-06-01" };

        foreach (var f in fechas)
            Assert.False(string.IsNullOrWhiteSpace(Clasificar(Mov(f), CadenaConHueco()).Motivo));
    }
}
