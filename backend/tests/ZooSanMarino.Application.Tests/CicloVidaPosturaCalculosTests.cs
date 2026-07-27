using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del cálculo puro del ciclo de vida Levante → Producción.
/// <para>
/// El núcleo es <see cref="CicloVidaPosturaCalculos.EsRegistroDeUsuario"/>: de él depende que una
/// reapertura de levante borre solo lo que el propio cierre generó y NUNCA captura del usuario. Por
/// eso los casos cubren las dos filas que crea el sistema (arrastre de huevos y traslado de aves),
/// el merge del usuario sobre ellas, y el comportamiento fail-closed ante filas ambiguas.
/// </para>
/// </summary>
public class CicloVidaPosturaCalculosTests
{
    private static readonly DateTime Dia = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>Fila neutra: todo en cero con el marcador de sistema. Los tests van moviendo un campo.</summary>
    private static RegistroProduccionResumen Fila(
        string? tipoAlimento = CicloVidaPosturaCalculos.TipoAlimentoSistema,
        decimal consKgH = 0m,
        decimal consKgM = 0m,
        int mortalidadH = 0,
        int selM = 0,
        int huevoTot = 0,
        int huevoTotArrastrado = 0,
        DateTime? fecha = null,
        int id = 1) =>
        new(id, fecha ?? Dia, tipoAlimento, consKgH, consKgM, mortalidadH, selM, huevoTot, huevoTotArrastrado);

    // ── Filas que genera el SISTEMA al cerrar el levante ───────────────────────────────────────

    [Fact]
    public void Fila_de_arrastre_de_huevos_pura_es_de_sistema()
    {
        // ArrastreHuevosLevanteService: tipo_alimento "N/A", sin consumo, y los huevos de la fila
        // son exactamente los que la marca declara haber volcado.
        var fila = Fila(huevoTot: 520, huevoTotArrastrado: 520);

        Assert.False(CicloVidaPosturaCalculos.EsRegistroDeUsuario(fila));
    }

    [Fact]
    public void Fila_de_traslado_de_aves_pura_es_de_sistema()
    {
        // MovimientoAvesService escribe en SelH y MortalidadM (positivos en la entrada, negativos en
        // el descuento) y deja el resto en cero. MortalidadH y SelM quedan intactas.
        var fila = Fila();

        Assert.False(CicloVidaPosturaCalculos.EsRegistroDeUsuario(fila));
    }

    [Fact]
    public void Fila_en_cero_sin_alimento_es_de_sistema()
    {
        Assert.False(CicloVidaPosturaCalculos.EsRegistroDeUsuario(Fila(tipoAlimento: null)));
        Assert.False(CicloVidaPosturaCalculos.EsRegistroDeUsuario(Fila(tipoAlimento: "")));
        Assert.False(CicloVidaPosturaCalculos.EsRegistroDeUsuario(Fila(tipoAlimento: "   ")));
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("n/a")]
    [InlineData(" N/A ")]
    public void El_marcador_de_sistema_se_reconoce_sin_importar_caja_ni_espacios(string marcador)
    {
        Assert.False(CicloVidaPosturaCalculos.EsRegistroDeUsuario(Fila(tipoAlimento: marcador)));
    }

    // ── Filas del USUARIO (bloquean la reapertura) ─────────────────────────────────────────────

    [Theory]
    [InlineData("Standard")]
    [InlineData("Postura")]
    [InlineData("Premium")]
    public void Alimento_real_marca_la_fila_como_del_usuario(string alimento)
    {
        // Caso del MERGE: el usuario registró el día sobre la fila del arrastre y ProduccionService
        // sobrescribió tipo_alimento. La fila conserva la marca de arrastre pero ya es suya.
        var fila = Fila(tipoAlimento: alimento, huevoTot: 520, huevoTotArrastrado: 520);

        Assert.True(CicloVidaPosturaCalculos.EsRegistroDeUsuario(fila));
    }

    [Fact]
    public void Consumo_capturado_marca_la_fila_como_del_usuario()
    {
        Assert.True(CicloVidaPosturaCalculos.EsRegistroDeUsuario(Fila(consKgH: 0.5m)));
        Assert.True(CicloVidaPosturaCalculos.EsRegistroDeUsuario(Fila(consKgM: 0.5m)));
    }

    [Fact]
    public void Mortalidad_de_hembras_o_seleccion_de_machos_marcan_la_fila_como_del_usuario()
    {
        // El sistema nunca toca estas dos columnas: si tienen valor, lo puso el usuario.
        Assert.True(CicloVidaPosturaCalculos.EsRegistroDeUsuario(Fila(mortalidadH: 1)));
        Assert.True(CicloVidaPosturaCalculos.EsRegistroDeUsuario(Fila(selM: 1)));
    }

    [Fact]
    public void Huevos_por_encima_del_arrastre_marcan_la_fila_como_del_usuario()
    {
        var fila = Fila(huevoTot: 600, huevoTotArrastrado: 520);

        Assert.True(CicloVidaPosturaCalculos.EsRegistroDeUsuario(fila));
    }

    [Fact]
    public void Huevos_sin_marca_de_arrastre_son_del_usuario_fail_closed()
    {
        // Sin marca en metadata no hay nada que pruebe que esos huevos los puso el cierre ⇒ ante la
        // duda se protege el dato y se bloquea la reapertura.
        var fila = Fila(huevoTot: 300, huevoTotArrastrado: 0);

        Assert.True(CicloVidaPosturaCalculos.EsRegistroDeUsuario(fila));
    }

    [Fact]
    public void Menos_huevos_que_los_arrastrados_no_alcanza_para_ser_del_usuario()
    {
        // El usuario pudo borrar categorías, pero eso no agrega captura nueva: sigue siendo la fila
        // del cierre. Lo que la volvería suya es el alimento real, que el merge ya escribe.
        var fila = Fila(huevoTot: 100, huevoTotArrastrado: 520);

        Assert.False(CicloVidaPosturaCalculos.EsRegistroDeUsuario(fila));
    }

    // ── Filtrado ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FiltrarRegistrosDeUsuario_deja_solo_los_del_usuario_ordenados_por_fecha()
    {
        var filas = new[]
        {
            Fila(id: 1, fecha: Dia.AddDays(2), tipoAlimento: "Postura"),   // usuario
            Fila(id: 2, fecha: Dia, huevoTot: 520, huevoTotArrastrado: 520), // sistema (arrastre)
            Fila(id: 3, fecha: Dia.AddDays(1), consKgH: 10m),               // usuario
            Fila(id: 4, fecha: Dia.AddDays(3))                              // sistema (traslado)
        };

        var resultado = CicloVidaPosturaCalculos.FiltrarRegistrosDeUsuario(filas);

        Assert.Equal(new[] { 3, 1 }, resultado.Select(r => r.Id).ToArray());
    }

    [Fact]
    public void FiltrarRegistrosDeUsuario_tolera_null_y_vacio()
    {
        Assert.Empty(CicloVidaPosturaCalculos.FiltrarRegistrosDeUsuario(null!));
        Assert.Empty(CicloVidaPosturaCalculos.FiltrarRegistrosDeUsuario(Array.Empty<RegistroProduccionResumen>()));
    }

    // ── Estado de cierre ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Cerrado", true)]     // género del levante
    [InlineData("Cerrada", true)]     // género del lote de producción
    [InlineData("cerrado ", true)]
    [InlineData(" CERRADA", true)]
    [InlineData("Abierta", false)]
    [InlineData("Abierto", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void EstaCerrado_cubre_ambos_generos_y_es_case_insensitive(string? estado, bool esperado)
    {
        Assert.Equal(esperado, CicloVidaPosturaCalculos.EstaCerrado(estado));
        Assert.Equal(!esperado, CicloVidaPosturaCalculos.EstaAbierto(estado));
    }

    // ── Mensajes ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Mensaje_de_bloqueo_con_un_solo_registro_usa_singular_y_la_fecha_exacta()
    {
        var mensaje = CicloVidaPosturaCalculos.ConstruirMensajeBloqueoReapertura(
            "P-L001", new[] { Fila(fecha: Dia, tipoAlimento: "Postura") });

        Assert.Contains("«P-L001»", mensaje);
        Assert.Contains("1 registro de seguimiento diario (01/03/2026)", mensaje);
        Assert.Contains("Elimine esos registros desde Seguimiento Diario de Producción", mensaje);
        Assert.Contains("se vuelve a crear actualizado", mensaje);
    }

    [Fact]
    public void Mensaje_de_bloqueo_con_varios_registros_describe_el_rango_de_fechas()
    {
        var registros = new[]
        {
            Fila(id: 1, fecha: Dia.AddDays(11), tipoAlimento: "Postura"),
            Fila(id: 2, fecha: Dia, tipoAlimento: "Postura"),
            Fila(id: 3, fecha: Dia.AddDays(5), tipoAlimento: "Postura")
        };

        var mensaje = CicloVidaPosturaCalculos.ConstruirMensajeBloqueoReapertura("P-L001", registros);

        Assert.Contains("3 registros de seguimiento diario (del 01/03/2026 al 12/03/2026)", mensaje);
    }

    [Fact]
    public void Mensaje_de_bloqueo_con_varios_registros_del_mismo_dia_no_repite_la_fecha()
    {
        var registros = new[]
        {
            Fila(id: 1, fecha: Dia, tipoAlimento: "Postura"),
            Fila(id: 2, fecha: Dia.AddHours(3), tipoAlimento: "Postura")
        };

        var mensaje = CicloVidaPosturaCalculos.ConstruirMensajeBloqueoReapertura("P-L001", registros);

        Assert.Contains("2 registros de seguimiento diario (01/03/2026)", mensaje);
        Assert.DoesNotContain("del 01/03/2026 al", mensaje);
    }

    [Fact]
    public void Mensaje_de_bloqueo_sin_nombre_de_lote_no_deja_comillas_vacias()
    {
        var mensaje = CicloVidaPosturaCalculos.ConstruirMensajeBloqueoReapertura(
            null, new[] { Fila(tipoAlimento: "Postura") });

        Assert.Contains("el lote de producción asociado tiene", mensaje);
        Assert.DoesNotContain("«»", mensaje);
    }

    [Fact]
    public void Mensaje_de_produccion_cerrada_pide_reabrir_produccion_primero()
    {
        var mensaje = CicloVidaPosturaCalculos.ConstruirMensajeBloqueoProduccionCerrada("P-L001");

        Assert.Contains("«P-L001» está cerrado", mensaje);
        Assert.Contains("Reabra primero el lote de producción", mensaje);
    }

    [Fact]
    public void Aviso_de_reapertura_permitida_explica_que_el_lote_de_produccion_se_elimina()
    {
        var aviso = CicloVidaPosturaCalculos.ConstruirAvisoReaperturaPermitida("P-L001");

        Assert.Contains("«P-L001» se eliminará", aviso);
        Assert.Contains("Se volverá a crear, actualizado", aviso);
    }

    [Fact]
    public void Aviso_de_reapertura_sin_lote_de_produccion_no_menciona_borrado()
    {
        var aviso = CicloVidaPosturaCalculos.ConstruirAvisoReaperturaPermitida(null);

        Assert.DoesNotContain("eliminará", aviso);
    }

    /// <summary>
    /// Las fechas viajan al front dentro del mensaje: el formato no puede depender de la cultura del
    /// servidor (un ECS en en-US daría 03/01/2026 y el usuario leería marzo como enero).
    /// </summary>
    [Fact]
    public void Las_fechas_del_mensaje_usan_formato_invariante()
    {
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");

            var mensaje = CicloVidaPosturaCalculos.ConstruirMensajeBloqueoReapertura(
                "P-L001", new[] { Fila(fecha: Dia, tipoAlimento: "Postura") });

            Assert.Contains("01/03/2026", mensaje);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
