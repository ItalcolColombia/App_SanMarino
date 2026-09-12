using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Hora de llegada de las aves → primer día con registro. Corte 13:00 INCLUSIVE; sin hora, el
/// comportamiento debe quedar idéntico al previo (los lotes existentes no tienen hora).
/// </summary>
public class EncasetamientoCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SinHora_ElPrimerDiaEsElEncaset_Regresion()
    {
        Assert.False(EncasetamientoCalculos.LlegadaTardia(null));
        Assert.Equal(Encaset, EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, null));
        Assert.Equal(0, EncasetamientoCalculos.EdadMinimaConRegistro(null));
        Assert.Null(EncasetamientoCalculos.MotivoDesplazamiento(null));
    }

    [Theory]
    [InlineData(0, 0)]    // 00:00
    [InlineData(6, 0)]    // 06:00
    [InlineData(11, 59)]  // justo antes del mediodía
    [InlineData(12, 0)]   // mediodía: NO es tardío (el corte es 13:00)
    [InlineData(12, 59)]  // último minuto temprano
    public void LlegadaTemprana_ElPrimerConsumoVaElMismoDia(int hora, int minuto)
    {
        var h = new TimeOnly(hora, minuto);

        Assert.False(EncasetamientoCalculos.LlegadaTardia(h));
        Assert.Equal(new DateTime(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc),
                     EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, h));
        Assert.Equal(0, EncasetamientoCalculos.EdadMinimaConRegistro(h));
    }

    [Theory]
    [InlineData(13, 0)]   // corte INCLUSIVE
    [InlineData(13, 1)]
    [InlineData(18, 30)]
    [InlineData(23, 59)]
    public void LlegadaTardia_ElPrimerConsumoVaAlDiaSiguiente(int hora, int minuto)
    {
        var h = new TimeOnly(hora, minuto);

        Assert.True(EncasetamientoCalculos.LlegadaTardia(h));
        Assert.Equal(new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc),
                     EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, h));
        Assert.Equal(1, EncasetamientoCalculos.EdadMinimaConRegistro(h));
    }

    [Fact]
    public void PrimerDiaConRegistro_ConservaKindYHoraDelEncaset()
    {
        var primer = EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, new TimeOnly(15, 0));

        Assert.Equal(DateTimeKind.Utc, primer.Kind);
        Assert.Equal(12, primer.Hour); // sigue anclado a mediodía UTC, como las fechas puras del sistema
    }

    [Fact]
    public void LlegadaTardia_CruzaFinDeMes()
    {
        var encaset = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);

        var primer = EncasetamientoCalculos.PrimerDiaConRegistro(encaset, new TimeOnly(18, 0));

        Assert.Equal(new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc), primer);
    }

    [Fact]
    public void LlegadaTardia_CruzaAlDia29EnAnioBisiesto()
    {
        var encaset = new DateTime(2028, 2, 28, 12, 0, 0, DateTimeKind.Utc);

        var primer = EncasetamientoCalculos.PrimerDiaConRegistro(encaset, new TimeOnly(14, 0));

        Assert.Equal(new DateTime(2028, 2, 29, 12, 0, 0, DateTimeKind.Utc), primer);
    }

    [Fact]
    public void MotivoDesplazamiento_ExplicaLaHoraAlUsuario()
    {
        var motivo = EncasetamientoCalculos.MotivoDesplazamiento(new TimeOnly(15, 30));

        Assert.NotNull(motivo);
        Assert.Contains("15:30", motivo);
        Assert.Contains("13:00", motivo);
    }

    // ── Ventana de captura de la reproductora ────────────────────────────────
    [Fact]
    public void EdadSeguimiento_LoteTemprano_AceptaDesdeLaEdadCero_Regresion()
    {
        Assert.True(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(0));
        Assert.True(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(7));
        Assert.False(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(-1));
        Assert.False(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(8));
    }

    [Fact]
    public void EdadSeguimiento_LoteTardio_RechazaElDiaDelEncaset()
    {
        const int edadMinima = 1;

        Assert.False(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(0, edadMinima: edadMinima));
        Assert.True(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(1, edadMinima: edadMinima));
        Assert.True(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(7, edadMinima: edadMinima));
        Assert.False(ReproductoraEngordeCalculos.EsEdadSeguimientoValida(8, edadMinima: edadMinima));
    }

    // ── Día de negocio: el primer día CON REGISTRO es el día 1 ───────────────
    // Caso del reporte: granja DAYLAND, lote "13 - 1", encaset 2026-06-08. La tabla mostraba
    // «Edad 0» el propio 08/06 y el usuario espera «Día 1».
    private static readonly DateTime Dayland = new(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(8, 1)]    // el día del encaset es el día 1, no el 0
    [InlineData(9, 2)]
    [InlineData(14, 7)]   // cierre de la primera semana
    [InlineData(15, 8)]   // arranque de la segunda
    public void DiaDeNegocio_SinHora_ArrancaEnUno(int diaDelMes, int esperado)
    {
        var fecha = new DateTime(2026, 6, diaDelMes, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(esperado, EncasetamientoCalculos.DiaDeNegocio(fecha, Dayland, null));
    }

    [Theory]
    [InlineData(8, 0)]    // el día del encaset ya no admite registro ⇒ queda fuera (≤ 0)
    [InlineData(9, 1)]    // el primer día CON REGISTRO es el día 1 igual que en un lote temprano
    [InlineData(15, 7)]   // y su semana 1 también son 7 días
    public void DiaDeNegocio_LlegadaTardia_CorreElDiaUno(int diaDelMes, int esperado)
    {
        var fecha = new DateTime(2026, 6, diaDelMes, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(esperado, EncasetamientoCalculos.DiaDeNegocio(fecha, Dayland, new TimeOnly(15, 0)));
    }

    [Fact]
    public void DiaDeNegocio_HoraTemprana_NoCorreNada()
    {
        var fecha = new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(1, EncasetamientoCalculos.DiaDeNegocio(fecha, Dayland, new TimeOnly(9, 30)));
    }

    [Theory]
    [InlineData(0, 0)]    // anterior al primer registro
    [InlineData(-3, 0)]
    [InlineData(1, 1)]
    [InlineData(7, 1)]    // la semana 1 son los días 1..7
    [InlineData(8, 2)]
    [InlineData(14, 2)]
    [InlineData(15, 3)]
    public void SemanaDeNegocio_AgrupaDeSieteEnSiete(int dia, int esperado)
    {
        Assert.Equal(esperado, EncasetamientoCalculos.SemanaDeNegocio(dia));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(13)]
    [InlineData(41)]
    public void SemanaDeNegocio_SinDesplazamiento_CoincideConLaDeLaFnSql_Regresion(int edad)
    {
        // fn_seguimiento_diario_engorde calcula la semana como ceil((edad + 1) / 7). Con la
        // numeración 1-based (dia = edad + 1) el resultado tiene que ser el MISMO número.
        var semanaFn = (int)Math.Ceiling((edad + 1) / 7.0);

        Assert.Equal(semanaFn, EncasetamientoCalculos.SemanaDeNegocio(edad + 1));
    }

    // ─── El flag de empresa quedó SOLO para el día de pesaje (28-ago-2026) ──────────────────────
    // El primer día con registro lo decide la HORA DEL LOTE en todas las empresas: el formulario
    // ofrece el campo con su leyenda a todas, y con el gate puesto ItalcolEcuador lo llenó 16 veces
    // —todas ≥ 13:00— sin que el backend lo mirara. Estos tests fijan que las dos reglas ya no
    // comparten interruptor.

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PrimerDiaConRegistro_NoDependeDelFlagDeEmpresa(bool flagEmpresa)
    {
        // La hora llega CRUDA al cálculo del primer día: el flag no participa.
        var primerDia = EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, new TimeOnly(23, 58));

        Assert.Equal(Encaset.AddDays(1), primerDia);
        // Y el flag sigue existiendo, pero solo para el pesaje: con él apagado la hora se anula.
        Assert.Equal(
            flagEmpresa ? new TimeOnly(23, 58) : null,
            EncasetamientoCalculos.HoraEfectiva(new TimeOnly(23, 58), flagEmpresa));
    }

    [Fact]
    public void FlagApagado_PrimerDiaSeCorre_PeroElDiaDePesajeNoSeMueve()
    {
        var hora = new TimeOnly(14, 0);                       // llegada tardía, empresa sin flag
        var septimoDiaDeVida = Encaset.AddDays(7);            // edad 7

        // El primer día con registro SÍ se corre: es lo que pidió el ticket.
        Assert.Equal(Encaset.AddDays(1), EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, hora));

        // El día de pesaje NO: con el flag apagado se sigue evaluando sobre la edad cruda, que es
        // como está tabulada la guía genética de Ecuador.
        Assert.True(PesajeEngordeCalculos.EsDiaDePesajeObligatorio(
            septimoDiaDeVida, Encaset, hora, reglaHoraActiva: false));
        Assert.Equal(
            PesajeEngordeCalculos.EsDiaDePesajeObligatorio(septimoDiaDeVida, Encaset, null, reglaHoraActiva: false),
            PesajeEngordeCalculos.EsDiaDePesajeObligatorio(septimoDiaDeVida, Encaset, hora, reglaHoraActiva: false));
    }

    [Fact]
    public void SinHora_ElDesplazamientoEsCero_AsiQueLasEmpresasQueNoUsanElCampoNoCambian()
    {
        // Es la garantía de que ungatear no toca a Sanmarino, Demo ni Santa Reyes: ninguno de sus
        // lotes tiene hora informada.
        Assert.Equal(0, EncasetamientoCalculos.DiasDesplazamiento(null));
        Assert.Equal(Encaset, EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, null));
        Assert.Null(EncasetamientoCalculos.MotivoDesplazamiento(null));
    }

    // ── Hora efectiva del lote reproductora (ticket Panamá 31-ago-2026: lote 95-1, hora 21:33) ──
    // La hora se captura en el formulario del lote POLLO ENGORDE; las reproductoras quedan con NULL
    // (0 de 142 en prod). Sin herencia, el primer registro de un lote tardío se numeraba «día 2» y
    // el guarda del día del encasetamiento nunca disparaba.

    [Fact]
    public void HoraEfectivaReproductora_SinHoraPropia_HeredaLaDelLoteEngorde()
    {
        var horaEngorde = new TimeOnly(21, 33);

        var efectiva = EncasetamientoCalculos.HoraEfectivaReproductora(null, horaEngorde);

        Assert.Equal(horaEngorde, efectiva);
        // Con la heredada tardía, el primer registro va al día siguiente del encaset (edad mínima 1).
        Assert.Equal(1, EncasetamientoCalculos.EdadMinimaConRegistro(efectiva));
    }

    [Fact]
    public void HoraEfectivaReproductora_LaHoraPropiaGanaSobreLaDelEngorde()
    {
        var propia = new TimeOnly(9, 0);
        var engorde = new TimeOnly(21, 33);

        var efectiva = EncasetamientoCalculos.HoraEfectivaReproductora(propia, engorde);

        Assert.Equal(propia, efectiva);
        Assert.Equal(0, EncasetamientoCalculos.EdadMinimaConRegistro(efectiva));
    }

    [Fact]
    public void HoraEfectivaReproductora_AmbasNull_QuedaNull_ComportamientoPrevio()
    {
        var efectiva = EncasetamientoCalculos.HoraEfectivaReproductora(null, null);

        Assert.Null(efectiva);
        Assert.Equal(0, EncasetamientoCalculos.EdadMinimaConRegistro(efectiva));
    }

    [Fact]
    public void HoraEfectivaReproductora_HeredadaTemprana_NoDesplazaElPrimerDia()
    {
        var efectiva = EncasetamientoCalculos.HoraEfectivaReproductora(null, new TimeOnly(8, 30));

        Assert.Equal(new TimeOnly(8, 30), efectiva);
        Assert.Equal(0, EncasetamientoCalculos.EdadMinimaConRegistro(efectiva));
        Assert.Equal(Encaset, EncasetamientoCalculos.PrimerDiaConRegistro(Encaset, efectiva));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DesplazamientoCruce — espejo puro de fn_cruce_reproductora_a_engorde.
    // Ticket Panamá 11-sep-2026 (lote 255): el cruce corría la serie DOS veces cuando la
    // reproductora ya arrancaba en la edad 1 por la misma llegada tardía.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DesplazamientoCruce_LlegadaTardiaConRegistroEnLaEdadCero_CorreLaSerieUnDia()
    {
        // El caso para el que se escribió la regla: el consumo del día del encaset es real y
        // pertenece al día siguiente. Comportamiento IDÉNTICO al previo (lotes 215 y 216).
        Assert.Equal(1, EncasetamientoCalculos.DesplazamientoCruce(new TimeOnly(21, 35), 0));
        Assert.Equal(1, EncasetamientoCalculos.DesplazamientoCruce(new TimeOnly(13, 0), 0));
    }

    [Fact]
    public void DesplazamientoCruce_LlegadaTardiaYLaReproductoraYaArrancoEnLaEdadUno_NoVuelveACorrer()
    {
        // EL CASO DEL TICKET (lote 255, encaset 03-sep 21:35, reproductora desde el 04-sep):
        // el día ya está corrido en el origen; sumar otro lo mandaba al 05-sep.
        Assert.Equal(0, EncasetamientoCalculos.DesplazamientoCruce(new TimeOnly(21, 35), 1));
    }

    [Fact]
    public void DesplazamientoCruce_PrimeraEdadMayorQueElDesplazamiento_NuncaCorreHaciaAtras()
    {
        // El Math.Max es la regla, no una defensa: un hueco de 2 días son días que nadie capturó.
        Assert.Equal(0, EncasetamientoCalculos.DesplazamientoCruce(new TimeOnly(21, 35), 2));
        Assert.Equal(0, EncasetamientoCalculos.DesplazamientoCruce(null, 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void DesplazamientoCruce_SinHora_SiempreCero_ComportamientoPrevio(int primeraEdad)
    {
        // Los 35 lotes sin hora de la copia de producción: el SQL queda byte a byte como antes.
        Assert.Equal(0, EncasetamientoCalculos.DesplazamientoCruce(null, primeraEdad));
    }

    [Fact]
    public void DesplazamientoCruce_HoraTemprana_NoDesplaza()
    {
        Assert.Equal(0, EncasetamientoCalculos.DesplazamientoCruce(new TimeOnly(12, 59), 0));
    }

    [Fact]
    public void DesplazamientoCruce_SinNingunaEdadGenerada_DevuelveElDesplazamientoTeorico()
    {
        // El cruce no escribe nada todavía; el número que aplicaría es el de la edad 0.
        Assert.Equal(1, EncasetamientoCalculos.DesplazamientoCruce(new TimeOnly(13, 0), null));
        Assert.Equal(0, EncasetamientoCalculos.DesplazamientoCruce(null, null));
    }

    [Fact]
    public void DesplazamientoCruce_ElPrimerDiaGeneradoCoincideConElGuardaDeCaptura()
    {
        // La contradicción que delató el defecto: el guarda de C# decía 04-sep y la fn escribía 05-sep.
        var encaset = new DateTime(2026, 9, 3, 12, 0, 0, DateTimeKind.Utc);
        var hora = new TimeOnly(21, 35);
        const int primeraEdad = 1;

        var destinoDelCruce = encaset.AddDays(EncasetamientoCalculos.DesplazamientoCruce(hora, primeraEdad) + primeraEdad);

        Assert.Equal(EncasetamientoCalculos.PrimerDiaConRegistro(encaset, hora), destinoDelCruce);
        Assert.Equal(new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc), destinoDelCruce);
    }
}
