// tests/ZooSanMarino.Application.Tests/HistoriaCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la HISTORIA (épica de ItalJira). Lo que se fija acá:
///  - el correlativo <c>HIS-AAAA-NNNN</c> no se reutiliza ni se contamina entre años;
///  - el avance NUNCA divide por cero (una historia recién creada no puede decir 100 %);
///  - un CASO se traduce al vocabulario de las tareas para poder contarlo como trabajo;
///  - las reglas de fecha son EXACTAMENTE las de las tareas (delegación, no copia).
/// </summary>
public class HistoriaCalculosTests
{
    private const string BACKLOG = TicketTareaEstados.Backlog;
    private const string CURSO   = TicketTareaEstados.EnCurso;
    private const string LISTO   = TicketTareaEstados.Listo;

    // ─────────────────────────── Código correlativo ───────────────────────────

    [Fact]
    public void GenerarCodigo_primera_historia_del_anio()
    {
        Assert.Equal("HIS-2026-0001", HistoriaCalculos.GenerarCodigo(2026, 1));
    }

    [Theory]
    [InlineData(0,    "HIS-2026-0001")]   // consecutivo inválido no puede producir HIS-2026-0000
    [InlineData(-5,   "HIS-2026-0001")]
    [InlineData(42,   "HIS-2026-0042")]
    [InlineData(1234, "HIS-2026-1234")]
    [InlineData(12345,"HIS-2026-12345")]  // más de 4 dígitos: crece, no se trunca
    public void GenerarCodigo_formatea_y_protege_el_consecutivo(int consecutivo, string esperado)
    {
        Assert.Equal(esperado, HistoriaCalculos.GenerarCodigo(2026, consecutivo));
    }

    [Fact]
    public void SiguienteConsecutivo_sin_codigos_arranca_en_uno()
    {
        Assert.Equal(1, HistoriaCalculos.SiguienteConsecutivo(Array.Empty<string?>(), 2026));
    }

    [Fact]
    public void SiguienteConsecutivo_toma_el_maximo_aunque_haya_huecos()
    {
        // El hueco (0002) es una historia borrada: el correlativo NO se reutiliza.
        var codigos = new string?[] { "HIS-2026-0001", "HIS-2026-0003" };
        Assert.Equal(4, HistoriaCalculos.SiguienteConsecutivo(codigos, 2026));
    }

    [Fact]
    public void SiguienteConsecutivo_ignora_otros_anios_y_codigos_corruptos()
    {
        var codigos = new string?[]
        {
            "HIS-2025-0099",   // otro año
            "TK-2026-000123",  // código de caso
            "HIS-2026-XX",     // corrupto
            null, "", "   ",
            "HIS-2026-0007"
        };
        Assert.Equal(8, HistoriaCalculos.SiguienteConsecutivo(codigos, 2026));
    }

    [Fact]
    public void SiguienteConsecutivo_es_case_insensitive_en_el_prefijo()
    {
        Assert.Equal(6, HistoriaCalculos.SiguienteConsecutivo(new string?[] { "his-2026-0005" }, 2026));
    }

    // ─────────────────────────── Normalización ───────────────────────────

    [Theory]
    [InlineData(null,        BACKLOG)]
    [InlineData("",          BACKLOG)]
    [InlineData("   ",       BACKLOG)]
    [InlineData("cualquiera",BACKLOG)]
    [InlineData("en_curso",  CURSO)]
    [InlineData("LISTO",     LISTO)]
    public void NormalizarEstado_cae_a_backlog_y_respeta_mayusculas(string? entrada, string esperado)
    {
        Assert.Equal(esperado, HistoriaCalculos.NormalizarEstado(entrada));
    }

    [Theory]
    [InlineData(null,      TicketPrioridades.Media)]
    [InlineData("basura",  TicketPrioridades.Media)]
    [InlineData("critica", TicketPrioridades.Critica)]
    [InlineData("Alta",    TicketPrioridades.Alta)]
    public void NormalizarPrioridad_cae_a_media(string? entrada, string esperado)
    {
        Assert.Equal(esperado, HistoriaCalculos.NormalizarPrioridad(entrada));
    }

    // ─────────────────────────── Fechas reales ───────────────────────────

    [Fact]
    public void SellarFechasReales_al_entrar_en_curso_sella_el_inicio_una_sola_vez()
    {
        var t0 = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2026, 8, 5, 10, 0, 0, DateTimeKind.Utc);

        var (inicio1, fin1) = HistoriaCalculos.SellarFechasReales(CURSO, null, null, t0);
        Assert.Equal(t0, inicio1);
        Assert.Null(fin1);

        // Re-entrar a EN_CURSO no pisa el inicio original.
        var (inicio2, _) = HistoriaCalculos.SellarFechasReales(CURSO, inicio1, fin1, t1);
        Assert.Equal(t0, inicio2);
    }

    [Fact]
    public void SellarFechasReales_listo_sella_el_fin_y_salir_de_listo_lo_borra()
    {
        var inicio = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var cierre = new DateTime(2026, 8, 9, 10, 0, 0, DateTimeKind.Utc);

        var (_, fin) = HistoriaCalculos.SellarFechasReales(LISTO, inicio, null, cierre);
        Assert.Equal(cierre, fin);

        // La historia se reabre: el fin deja de existir porque volvió a estar en juego.
        var (_, finReabierta) = HistoriaCalculos.SellarFechasReales(CURSO, inicio, fin, cierre);
        Assert.Null(finReabierta);
    }

    [Fact]
    public void SellarFechasReales_delega_en_la_regla_de_las_tareas()
    {
        var ahora = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

        // Equivalencia explícita: si esto se rompe, es que alguien duplicó la regla.
        Assert.Equal(
            TicketTareaCalculos.SellarFechasReales(LISTO, null, null, ahora),
            HistoriaCalculos.SellarFechasReales(LISTO, null, null, ahora));
    }

    // ─────────────────────────── Traducción caso → trabajo ───────────────────────────

    [Theory]
    [InlineData(TicketEstados.Abierto,          BACKLOG)]
    [InlineData(TicketEstados.EnAnalisis,       TicketTareaEstados.Analisis)]
    [InlineData(TicketEstados.EnDocumentacion,  TicketTareaEstados.Documentacion)]
    [InlineData(TicketEstados.EnImplementacion, CURSO)]
    [InlineData(TicketEstados.EnRevision,       TicketTareaEstados.EnRevision)]
    [InlineData(TicketEstados.Solucionado,      LISTO)]
    [InlineData(TicketEstados.Cerrado,          LISTO)]
    [InlineData(TicketEstados.Transferido,      TicketTareaEstados.Bloqueada)]
    [InlineData(TicketEstados.Suspendido,       TicketTareaEstados.Bloqueada)]
    public void EstadoTrabajoDeCaso_mapea_las_nueve_fases(string estadoCaso, string esperado)
    {
        Assert.Equal(esperado, HistoriaCalculos.EstadoTrabajoDeCaso(estadoCaso));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("INVENTADO")]
    public void EstadoTrabajoDeCaso_desconocido_no_cuenta_como_terminado(string? estadoCaso)
    {
        var traducido = HistoriaCalculos.EstadoTrabajoDeCaso(estadoCaso);
        Assert.Equal(BACKLOG, traducido);
        Assert.False(TicketTareaEstados.EsTerminal(traducido));
    }

    [Fact]
    public void EstadoTrabajoDeCaso_acepta_minusculas()
    {
        Assert.Equal(LISTO, HistoriaCalculos.EstadoTrabajoDeCaso("solucionado"));
    }

    // ─────────────────────────── Avance ───────────────────────────

    [Fact]
    public void AvancePorTareas_sin_trabajos_usa_el_estado_propio()
    {
        // Una historia recién creada NO puede decir 100 %: no hay 0/0 que dividir.
        Assert.Equal(0,   HistoriaCalculos.AvancePorTareas(Array.Empty<string>(), BACKLOG));
        Assert.Equal(0,   HistoriaCalculos.AvancePorTareas(Array.Empty<string>(), CURSO));
        Assert.Equal(100, HistoriaCalculos.AvancePorTareas(Array.Empty<string>(), LISTO));
        Assert.Equal(0,   HistoriaCalculos.AvancePorTareas(Array.Empty<string>(), null));
    }

    [Fact]
    public void AvancePorTareas_cuenta_solo_las_terminadas()
    {
        var estados = new[] { LISTO, LISTO, LISTO, CURSO, BACKLOG };
        Assert.Equal(60, HistoriaCalculos.AvancePorTareas(estados, CURSO));
    }

    [Fact]
    public void AvancePorTareas_todas_listas_da_cien_aunque_la_historia_siga_en_curso()
    {
        var estados = new[] { LISTO, LISTO };
        Assert.Equal(100, HistoriaCalculos.AvancePorTareas(estados, CURSO));
    }

    [Fact]
    public void AvancePorTareas_redondea_alejandose_del_cero()
    {
        // 1/3 = 33,33 % → 33 ; 2/3 = 66,66 % → 67
        Assert.Equal(33, HistoriaCalculos.AvancePorTareas(new[] { LISTO, CURSO, CURSO }, CURSO));
        Assert.Equal(67, HistoriaCalculos.AvancePorTareas(new[] { LISTO, LISTO, CURSO }, CURSO));
    }

    [Fact]
    public void ConteoAvance_devuelve_terminados_sobre_total()
    {
        var (terminados, total) = HistoriaCalculos.ConteoAvance(new[] { LISTO, CURSO, LISTO, BACKLOG });
        Assert.Equal(2, terminados);
        Assert.Equal(4, total);
    }

    [Fact]
    public void ConteoAvance_sin_trabajos_da_cero_sobre_cero()
    {
        var (terminados, total) = HistoriaCalculos.ConteoAvance(Array.Empty<string>());
        Assert.Equal(0, terminados);
        Assert.Equal(0, total);
    }

    // ─────────────────────────── Rango del roadmap ───────────────────────────

    private static DateOnly D(int mes, int dia) => new(2026, mes, dia);

    [Fact]
    public void RangoPlanDerivado_toma_el_minimo_de_inicios_y_el_maximo_de_fines()
    {
        var trabajos = new (DateOnly?, DateOnly?)[]
        {
            (D(3, 10), D(3, 20)),
            (D(2, 1),  D(2, 28)),
            (D(4, 5),  D(5, 30)),
        };

        var (inicio, fin) = HistoriaCalculos.RangoPlanDerivado(trabajos);
        Assert.Equal(D(2, 1), inicio);
        Assert.Equal(D(5, 30), fin);
    }

    [Fact]
    public void RangoPlanDerivado_sin_fechas_no_inventa_una_barra()
    {
        var trabajos = new (DateOnly?, DateOnly?)[] { (null, null), (null, null) };
        var (inicio, fin) = HistoriaCalculos.RangoPlanDerivado(trabajos);
        Assert.Null(inicio);
        Assert.Null(fin);
    }

    [Fact]
    public void RangoPlanDerivado_usa_los_extremos_que_existan()
    {
        var trabajos = new (DateOnly?, DateOnly?)[] { (D(6, 1), null), (null, D(7, 15)) };
        var (inicio, fin) = HistoriaCalculos.RangoPlanDerivado(trabajos);
        Assert.Equal(D(6, 1), inicio);
        Assert.Equal(D(7, 15), fin);
    }

    [Fact]
    public void RangoEfectivo_las_fechas_propias_de_la_historia_mandan()
    {
        var trabajos = new (DateOnly?, DateOnly?)[] { (D(2, 1), D(9, 30)) };
        var (inicio, fin) = HistoriaCalculos.RangoEfectivo(D(1, 1), D(12, 31), trabajos);
        Assert.Equal(D(1, 1), inicio);
        Assert.Equal(D(12, 31), fin);
    }

    [Fact]
    public void RangoEfectivo_completa_solo_el_extremo_que_falta()
    {
        var trabajos = new (DateOnly?, DateOnly?)[] { (D(2, 1), D(9, 30)) };

        var (inicio1, fin1) = HistoriaCalculos.RangoEfectivo(D(1, 1), null, trabajos);
        Assert.Equal(D(1, 1), inicio1);
        Assert.Equal(D(9, 30), fin1);

        var (inicio2, fin2) = HistoriaCalculos.RangoEfectivo(null, D(12, 31), trabajos);
        Assert.Equal(D(2, 1), inicio2);
        Assert.Equal(D(12, 31), fin2);
    }

    [Fact]
    public void RangoEfectivo_nunca_devuelve_una_barra_invertida()
    {
        // Fin propio anterior al inicio derivado: la barra se degrada a un punto, no se invierte.
        var trabajos = new (DateOnly?, DateOnly?)[] { (D(6, 1), D(6, 30)) };
        var (inicio, fin) = HistoriaCalculos.RangoEfectivo(null, D(3, 1), trabajos);
        Assert.Equal(D(6, 1), inicio);
        Assert.Equal(D(6, 1), fin);
    }

    [Fact]
    public void RangoEfectivo_sin_nada_no_dibuja()
    {
        var (inicio, fin) = HistoriaCalculos.RangoEfectivo(null, null, Array.Empty<(DateOnly?, DateOnly?)>());
        Assert.Null(inicio);
        Assert.Null(fin);
    }

    // ─────────────────────────── Retrocompatibilidad del tablero ───────────────────────────

    [Fact]
    public void Reordenar_sirve_igual_para_historias_que_para_tareas()
    {
        // El tablero de historias reutiliza TicketTareaCalculos.Reordenar: la estructura no sabe
        // si la fila es tarea o historia. Este test es el que impide que alguien la duplique.
        var actuales = new[]
        {
            new TicketTareaCalculos.Posicion(1, BACKLOG, 0),
            new TicketTareaCalculos.Posicion(2, BACKLOG, 1),
            new TicketTareaCalculos.Posicion(3, CURSO,   0),
        };

        var cambios = TicketTareaCalculos.Reordenar(actuales, idMovido: 2, estadoDestino: CURSO, indiceDestino: 0);

        var movida = Assert.Single(cambios, c => c.Id == 2);
        Assert.Equal(CURSO, movida.Estado);
        Assert.Equal(0, movida.Orden);

        // La 3 baja a la posición 1 y la columna origen queda compactada (la 1 ya estaba en 0).
        Assert.Contains(cambios, c => c.Id == 3 && c.Orden == 1);
    }
}
