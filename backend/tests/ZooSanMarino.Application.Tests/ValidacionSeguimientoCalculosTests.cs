using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la doble validación de los seguimientos diarios.
///
/// <para>
/// El bloque más importante es <c>FlagApagado_*</c>: con la empresa sin el flag, todas las respuestas
/// tienen que reproducir el comportamiento anterior a esta funcionalidad (descuenta al guardar, no
/// separa, no bloquea, nada en rojo). Es el gate que exige CLAUDE.md para cualquier feature por
/// empresa, y acá vale doble porque el descuento diferido toca inventario en producción.
/// </para>
/// </summary>
public class ValidacionSeguimientoCalculosTests
{
    private static readonly DateOnly Lunes = new(2026, 8, 10);

    // ─── Flag apagado: el comportamiento previo, intacto ──────────────────────

    [Fact]
    public void FlagApagado_DescuentaAlGuardarYNoSepara()
    {
        Assert.True(ValidacionSeguimientoCalculos.DescuentaAlGuardar(false));
        Assert.False(ValidacionSeguimientoCalculos.SeparaAlGuardar(false));
    }

    [Fact]
    public void FlagApagado_TodoSigueSiendoEditable()
    {
        // Incluso un registro marcado como validado por el backfill: en esas empresas el concepto
        // no existe y quitarles la edición sería un cambio de comportamiento que nadie pidió.
        Assert.True(ValidacionSeguimientoCalculos.EsEditable(false, validado: true));
        Assert.True(ValidacionSeguimientoCalculos.EsEditable(false, validado: false));
    }

    [Fact]
    public void FlagApagado_NuncaBloqueaAunqueHayaVencidos()
    {
        Assert.False(ValidacionSeguimientoCalculos.BloqueaAltaPorVencidos(false, cantidadVencidos: 7));
    }

    // ─── Flag encendido: separar en vez de descontar ──────────────────────────

    [Fact]
    public void FlagEncendido_SeparaYNoDescuenta()
    {
        Assert.False(ValidacionSeguimientoCalculos.DescuentaAlGuardar(true));
        Assert.True(ValidacionSeguimientoCalculos.SeparaAlGuardar(true));
    }

    [Fact]
    public void DescontarYSepararSonMutuamenteExcluyentes()
    {
        // El invariante que evita contar el consumo dos veces.
        foreach (var flag in new[] { true, false })
            Assert.NotEqual(
                ValidacionSeguimientoCalculos.DescuentaAlGuardar(flag),
                ValidacionSeguimientoCalculos.SeparaAlGuardar(flag));
    }

    [Fact]
    public void FlagEncendido_ValidadoDejaDeSerEditable()
    {
        Assert.True(ValidacionSeguimientoCalculos.EsEditable(true, validado: false));
        Assert.False(ValidacionSeguimientoCalculos.EsEditable(true, validado: true));
    }

    // ─── Estado derivado ──────────────────────────────────────────────────────

    [Fact]
    public void ValidadoEsValidadoAunqueSeaViejo()
    {
        var estado = ValidacionSeguimientoCalculos.Estado(
            validado: true, fechaSeguimiento: Lunes, hoy: Lunes.AddDays(30));

        Assert.Equal(EstadoValidacionSeguimiento.Validado, estado);
    }

    [Theory]
    [InlineData(0)]  // mismo día
    [InlineData(1)]  // el día siguiente: último día del plazo
    public void SinValidarDentroDelPlazo_EstaPendiente(int diasDespues)
    {
        var estado = ValidacionSeguimientoCalculos.Estado(
            validado: false, fechaSeguimiento: Lunes, hoy: Lunes.AddDays(diasDespues));

        Assert.Equal(EstadoValidacionSeguimiento.Pendiente, estado);
    }

    [Fact]
    public void SinValidarPasadoElPlazo_EstaEnRetraso()
    {
        // «Un día de diferencia como máximo»: el registro del lunes vence el martes; el miércoles
        // ya está en retraso.
        var estado = ValidacionSeguimientoCalculos.Estado(
            validado: false, fechaSeguimiento: Lunes, hoy: Lunes.AddDays(2));

        Assert.Equal(EstadoValidacionSeguimiento.EnRetraso, estado);
        Assert.True(ValidacionSeguimientoCalculos.EstaEnRetraso(false, Lunes, Lunes.AddDays(2)));
    }

    [Fact]
    public void FechaLimiteEsElDiaSiguiente()
    {
        Assert.Equal(Lunes.AddDays(1), ValidacionSeguimientoCalculos.FechaLimiteValidacion(Lunes));
    }

    [Fact]
    public void RegistroFuturo_NoEstaEnRetraso()
    {
        // Carga anticipada o desfase de zona horaria: hoy < fecha del registro. No puede estar vencido.
        Assert.Equal(
            EstadoValidacionSeguimiento.Pendiente,
            ValidacionSeguimientoCalculos.Estado(false, Lunes, Lunes.AddDays(-1)));
    }

    // ─── Bloqueo y mensajes ───────────────────────────────────────────────────

    [Fact]
    public void FlagEncendidoConVencidos_Bloquea()
    {
        Assert.True(ValidacionSeguimientoCalculos.BloqueaAltaPorVencidos(true, 1));
        Assert.False(ValidacionSeguimientoCalculos.BloqueaAltaPorVencidos(true, 0));
    }

    [Fact]
    public void MensajeDeBloqueoNombraLasFechas()
    {
        var msg = ValidacionSeguimientoCalculos.MensajeBloqueoPorVencidos(
            new[] { new DateOnly(2026, 8, 11), new DateOnly(2026, 8, 10) });

        // Ordenadas y en formato local: el usuario tiene que poder ir directo a esas filas.
        Assert.Contains("10/08/2026", msg);
        Assert.Contains("11/08/2026", msg);
        Assert.True(msg.IndexOf("10/08/2026", StringComparison.Ordinal) <
                    msg.IndexOf("11/08/2026", StringComparison.Ordinal));
        Assert.Contains("2 registros", msg);
    }

    [Fact]
    public void MensajeDeBloqueoRecortaLaListaLarga()
    {
        var fechas = Enumerable.Range(0, 9).Select(i => Lunes.AddDays(i)).ToArray();

        var msg = ValidacionSeguimientoCalculos.MensajeBloqueoPorVencidos(fechas, maximoFechas: 3);

        Assert.Contains("y 6 más", msg);
        Assert.Contains("9 registros", msg);
    }

    [Fact]
    public void MensajeDeAlerta_DistingueRetrasoDePendiente()
    {
        var soloPendientes = ValidacionSeguimientoCalculos.MensajeAlertaPendientes(0, 3);
        Assert.Contains("3 registros", soloPendientes);
        Assert.DoesNotContain("RETRASO", soloPendientes);

        var conRetraso = ValidacionSeguimientoCalculos.MensajeAlertaPendientes(2, 5);
        Assert.Contains("2 registros EN RETRASO", conRetraso);
        Assert.Contains("3 pendientes dentro del plazo", conRetraso);
        Assert.Contains("no acepta registros de días nuevos", conRetraso);
    }

    [Fact]
    public void MensajeDeAlerta_VacioSinPendientes()
    {
        Assert.Equal(string.Empty, ValidacionSeguimientoCalculos.MensajeAlertaPendientes(0, 0));
    }

    // ─── Catálogo de módulos ──────────────────────────────────────────────────

    [Theory]
    [InlineData(ModuloSeguimiento.Levante, true, false)]
    [InlineData(ModuloSeguimiento.Produccion, true, false)]
    [InlineData(ModuloSeguimiento.Engorde, false, true)]
    [InlineData(ModuloSeguimiento.EngordeEcuador, false, true)]
    [InlineData(ModuloSeguimiento.Reproductora, false, false)]
    public void ClasificacionDeModulos(string modulo, bool esPostura, bool esEngorde)
    {
        Assert.True(ModuloSeguimiento.EsValido(modulo));
        Assert.Equal(esPostura, ModuloSeguimiento.EsPostura(modulo));
        Assert.Equal(esEngorde, ModuloSeguimiento.EsEngorde(modulo));
    }

    [Fact]
    public void ModuloDesconocidoNoEsValido()
    {
        Assert.False(ModuloSeguimiento.EsValido("VACUNACION"));
        Assert.False(ModuloSeguimiento.EsValido(null));
    }

    // ─── Sincronización del cruce: sólo reproductora ──────────────────────────
    // Reproductora es el único módulo cuyas bajas las escribe el TRIGGER del cruce y no un aplicador
    // de C#, así que validar/des-validar tiene que sincronizar el maestro de aves a mano. Para los
    // demás la sincronización es un no-op: si esto devolviera true de más, se correría el aplicador
    // del cruce sobre lotes que no tienen días de cruce.

    [Theory]
    [InlineData(ModuloSeguimiento.Reproductora, true)]
    [InlineData(ModuloSeguimiento.Engorde, false)]
    [InlineData(ModuloSeguimiento.EngordeEcuador, false)]
    [InlineData(ModuloSeguimiento.Levante, false)]
    [InlineData(ModuloSeguimiento.Produccion, false)]
    public void SoloReproductoraRequiereSincronizarElCruce(string modulo, bool esperado)
    {
        Assert.Equal(esperado, ModuloSeguimiento.RequiereSincronizarCruce(modulo));
    }

    [Fact]
    public void RequiereSincronizarCruceEsInsensibleAMayusculasYToleraNulo()
    {
        Assert.True(ModuloSeguimiento.RequiereSincronizarCruce("reproductora"));
        Assert.False(ModuloSeguimiento.RequiereSincronizarCruce(null));
        Assert.False(ModuloSeguimiento.RequiereSincronizarCruce(""));
        Assert.False(ModuloSeguimiento.RequiereSincronizarCruce("VACUNACION"));
    }

    // ─── Literal canónico: los dos engordes son UN solo registro ──────────────
    // Nace del bug de agosto-2026: el formulario de engorde hace su CRUD contra el controller de
    // Ecuador (que separaba como ENGORDE_EC) pero pide pendientes y valida como ENGORDE. Al filtrar
    // las reservas por módulo no encontraba ninguna, así que validar marcaba `validado = true` sin
    // descontar un solo kilo y la reserva quedaba activa para siempre.

    [Fact]
    public void Canonico_EngordeEcuadorColapsaAEngorde()
    {
        Assert.Equal(ModuloSeguimiento.Engorde, ModuloSeguimiento.Canonico(ModuloSeguimiento.EngordeEcuador));
        Assert.Equal(ModuloSeguimiento.Engorde, ModuloSeguimiento.Canonico("engorde_ec"));
    }

    [Theory]
    [InlineData(ModuloSeguimiento.Levante)]
    [InlineData(ModuloSeguimiento.Produccion)]
    [InlineData(ModuloSeguimiento.Engorde)]
    [InlineData(ModuloSeguimiento.Reproductora)]
    public void Canonico_NoTocaAlResto(string modulo)
    {
        Assert.Equal(modulo, ModuloSeguimiento.Canonico(modulo));
    }

    /// <summary>
    /// La clave canónica es lo que hace que separar por una vía y validar por la otra se encuentren:
    /// separar como <c>ENGORDE_EC</c> y validar como <c>ENGORDE</c> tienen que dar la MISMA clave.
    /// </summary>
    [Fact]
    public void Canonico_SepararPorEcuadorYValidarPorEngordeDanLaMismaClave()
    {
        var claveAlSeparar = ModuloSeguimiento.Canonico(ModuloSeguimiento.EngordeEcuador);
        var claveAlValidar = ModuloSeguimiento.Canonico(ModuloSeguimiento.Engorde);

        Assert.Equal(claveAlSeparar, claveAlValidar);
    }

    /// <summary>
    /// Colapsar la clave no puede colapsar el vocabulario: los dos literales siguen siendo válidos en
    /// la API y siguen clasificando como engorde.
    /// </summary>
    [Fact]
    public void Canonico_NoInvalidaElLiteralDeEcuador()
    {
        Assert.True(ModuloSeguimiento.EsValido(ModuloSeguimiento.EngordeEcuador));
        Assert.True(ModuloSeguimiento.EsEngorde(ModuloSeguimiento.EngordeEcuador));
        Assert.False(ModuloSeguimiento.EsPostura(ModuloSeguimiento.EngordeEcuador));
    }
    // ─── Concordancia del mensaje de bloqueo ────────────────────────────────
    // Nace de un smoke real: con UNA fecha vencida el texto decía «un registro ... que superaron el
    // plazo». Un mensaje mal concordado se lee como un error del sistema y le resta credibilidad al
    // aviso justo cuando se le está pidiendo al usuario que actúe.

    [Fact]
    public void MensajeBloqueo_ConUnaSolaFecha_ConcuerdaEnSingular()
    {
        var msg = ValidacionSeguimientoCalculos.MensajeBloqueoPorVencidos(
            new[] { new DateOnly(2026, 8, 11) });

        Assert.Contains("un registro", msg);
        Assert.Contains("que superó el plazo", msg);
        Assert.Contains("Validá ese registro", msg);
        Assert.DoesNotContain("superaron", msg);
    }

    [Fact]
    public void MensajeBloqueo_ConVariasFechas_ConcuerdaEnPlural()
    {
        var msg = ValidacionSeguimientoCalculos.MensajeBloqueoPorVencidos(
            new[] { new DateOnly(2026, 8, 11), new DateOnly(2026, 8, 12) });

        Assert.Contains("2 registros", msg);
        Assert.Contains("que superaron el plazo", msg);
        Assert.Contains("Validá esos registros", msg);
    }

    // ─── El mecanismo que dejó 4 lotes de Panamá trabados (25-ago-2026) ────────
    //
    // Estos tres tests no cubren código nuevo: fijan la interacción que hizo posible el defecto de
    // `fn_cruce_reproductora_a_engorde`, para que se lea en el banco de pruebas y no solo en un plan.
    // Ver fase_de_desarrollo/cruce_reproductora_nace_sin_validar_plan.md

    /// <summary>
    /// 🔴 El estado depende de la FECHA DEL SEGUIMIENTO, no de cuándo se creó la fila. Un registro
    /// insertado hoy con fecha de hace días <b>nace EN_RETRASO</b>: nunca tuvo ventana para validarse.
    ///
    /// <para>
    /// Es exactamente lo que le pasaba a los días 1-7 que genera el cruce cuando la reproductora se
    /// confirma tarde. Medido en producción: la reproductora del lote 215 confirmó con 5 a 10 días de
    /// atraso, y los 7 registros de pollo engorde nacieron entre 6 y 12 días vencidos.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(12)]
    public void Un_registro_creado_hoy_con_fecha_vieja_nace_EN_RETRASO(int diasDeAtraso)
    {
        var hoy = new DateOnly(2026, 8, 25);
        var fechaSeguimiento = hoy.AddDays(-diasDeAtraso);

        Assert.Equal(
            EstadoValidacionSeguimiento.EnRetraso,
            ValidacionSeguimientoCalculos.Estado(validado: false, fechaSeguimiento, hoy));
    }

    /// <summary>
    /// Y por eso el arreglo funciona: <b>validado gana sobre la fecha</b>. Un registro validado nunca
    /// está en retraso, por vieja que sea su fecha — que es lo que permite que los días del cruce,
    /// naciendo ya validados, no traben el lote.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(12)]
    public void Un_registro_validado_nunca_esta_en_retraso_por_vieja_que_sea_su_fecha(int diasDeAtraso)
    {
        var hoy = new DateOnly(2026, 8, 25);
        var fechaSeguimiento = hoy.AddDays(-diasDeAtraso);

        Assert.Equal(
            EstadoValidacionSeguimiento.Validado,
            ValidacionSeguimientoCalculos.Estado(validado: true, fechaSeguimiento, hoy));
        Assert.False(ValidacionSeguimientoCalculos.EstaEnRetraso(true, fechaSeguimiento, hoy));
    }

    /// <summary>
    /// El plazo es de UN día, y esa estrechez es la que convierte cualquier retraso del cruce en un
    /// bloqueo inmediato. Se fija en un test para que cambiarlo sea una decisión consciente: el
    /// número lo puso el usuario («los registros tienen que ser validados con un día de diferencia
    /// como máximo»).
    /// </summary>
    [Fact]
    public void El_plazo_es_de_un_dia_y_vence_al_dia_siguiente()
    {
        Assert.Equal(1, ValidacionSeguimientoCalculos.DiasPlazoValidacion);

        var fecha = new DateOnly(2026, 8, 20);
        Assert.Equal(new DateOnly(2026, 8, 21), ValidacionSeguimientoCalculos.FechaLimiteValidacion(fecha));

        // El día límite todavía se puede validar; el siguiente ya no.
        Assert.Equal(EstadoValidacionSeguimiento.Pendiente,
            ValidacionSeguimientoCalculos.Estado(false, fecha, new DateOnly(2026, 8, 21)));
        Assert.Equal(EstadoValidacionSeguimiento.EnRetraso,
            ValidacionSeguimientoCalculos.Estado(false, fecha, new DateOnly(2026, 8, 22)));
    }

    // ─── El plazo se cuenta desde la CREACIÓN ─────────────────────────────────
    //
    // Con el plazo contado desde `fecha`, un día viejo cargado hoy nacía EN_RETRASO en el mismo
    // instante en que se guardaba — y un vencido sin validar bloquea el alta de días nuevos, así que
    // el operario quedaba trabado por el registro que acababa de hacer. Medido el 25-ago-2026 sobre
    // la copia de producción: en 30 días, el 89,5 % de la captura de ItalcolPanama (1.191 de 1.331) y
    // el 14,1 % de la de ItalcolEcuador nacían ya vencidas.
    //
    // La regla la dijo el usuario: «debo tenerlas máximo para confirmar mañana, porque hoy las hice
    // la creación, no de acuerdo a cuándo es».

    /// <summary>
    /// Equivalencia: para la captura del mismo día —el caso normal— las dos reglas dan idéntico.
    /// Es el test que protege al 100 % del flujo diario de cambiar de comportamiento.
    /// </summary>
    [Theory]
    [InlineData(0, EstadoValidacionSeguimiento.Pendiente)]   // hoy es el día del registro
    [InlineData(1, EstadoValidacionSeguimiento.Pendiente)]   // día límite: todavía se valida
    [InlineData(2, EstadoValidacionSeguimiento.EnRetraso)]   // ya venció
    public void CapturaDelMismoDia_LasDosReglasDanIgual(int diasDespues, EstadoValidacionSeguimiento esperado)
    {
        var fecha = new DateOnly(2026, 8, 20);
        var hoy = fecha.AddDays(diasDespues);

        var conCreacion = ValidacionSeguimientoCalculos.Estado(false, fecha, fecha, hoy);
        var sinCreacion = ValidacionSeguimientoCalculos.Estado(false, fecha, hoy);

        Assert.Equal(esperado, conCreacion);
        Assert.Equal(sinCreacion, conCreacion);
    }

    /// <summary>
    /// El caso que motiva el cambio: un día de hace un mes, cargado HOY, no nace vencido — tiene su
    /// día de plazo como cualquier otro. Con la regla vieja nacía EN_RETRASO y trababa el lote.
    /// </summary>
    [Fact]
    public void DiaViejoCargadoHoy_NoNaceVencido()
    {
        var fecha = new DateOnly(2026, 7, 20);
        var creacion = new DateOnly(2026, 8, 20);

        Assert.Equal(new DateOnly(2026, 8, 21),
            ValidacionSeguimientoCalculos.FechaLimiteValidacion(fecha, creacion));

        // Hoy, recién creado: pendiente, no bloquea.
        Assert.Equal(EstadoValidacionSeguimiento.Pendiente,
            ValidacionSeguimientoCalculos.Estado(false, fecha, creacion, creacion));
        Assert.False(ValidacionSeguimientoCalculos.EstaEnRetraso(false, fecha, creacion, creacion));

        // La regla vieja lo daba por vencido en el mismo instante.
        Assert.True(ValidacionSeguimientoCalculos.EstaEnRetraso(false, fecha, creacion));
    }

    /// <summary>El plazo no es indefinido: al día siguiente de crearlo, vence igual.</summary>
    [Fact]
    public void DiaViejoCargadoHoy_VenceAlDiaSiguienteDeCrearlo()
    {
        var fecha = new DateOnly(2026, 7, 20);
        var creacion = new DateOnly(2026, 8, 20);

        Assert.Equal(EstadoValidacionSeguimiento.Pendiente,
            ValidacionSeguimientoCalculos.Estado(false, fecha, creacion, creacion.AddDays(1)));
        Assert.Equal(EstadoValidacionSeguimiento.EnRetraso,
            ValidacionSeguimientoCalculos.Estado(false, fecha, creacion, creacion.AddDays(2)));
    }

    /// <summary>
    /// Sin fecha de creación (filas viejas, sin auditoría) cae en el comportamiento previo, byte a
    /// byte. Es lo que evita que el cambio reescriba el estado del histórico.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(30)]
    public void SinFechaDeCreacion_ComportamientoPrevioIntacto(int diasDespues)
    {
        var fecha = new DateOnly(2026, 8, 20);
        var hoy = fecha.AddDays(diasDespues);

        Assert.Equal(
            ValidacionSeguimientoCalculos.Estado(false, fecha, hoy),
            ValidacionSeguimientoCalculos.Estado(false, fecha, null, hoy));
        Assert.Equal(
            ValidacionSeguimientoCalculos.FechaLimiteValidacion(fecha),
            ValidacionSeguimientoCalculos.FechaLimiteValidacion(fecha, null));
    }

    /// <summary>
    /// Un registro cargado por ANTICIPADO no arranca con menos plazo: el límite se cuenta desde la
    /// fecha del seguimiento, no desde una creación anterior. Por eso la fórmula es
    /// <c>max(fecha, creación)</c> y no <c>creación</c> a secas.
    /// </summary>
    [Fact]
    public void RegistroCargadoPorAnticipado_ElPlazoNoSeAcorta()
    {
        var fecha = new DateOnly(2026, 8, 20);
        var creacion = new DateOnly(2026, 8, 18);

        Assert.Equal(new DateOnly(2026, 8, 21),
            ValidacionSeguimientoCalculos.FechaLimiteValidacion(fecha, creacion));
        Assert.Equal(EstadoValidacionSeguimiento.Pendiente,
            ValidacionSeguimientoCalculos.Estado(false, fecha, creacion, fecha));
    }

    /// <summary>
    /// Invariante de dirección: el límite nuevo nunca cae ANTES que el viejo. O sea, el cambio sólo
    /// puede aflojar — no puede bloquear a nadie que hoy no esté bloqueado. Es lo que lo vuelve
    /// seguro de desplegar sobre una empresa que ya tiene la regla encendida.
    /// </summary>
    [Theory]
    [InlineData(-10)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(45)]
    public void ElLimiteNuevoNuncaEsMasEstrictoQueElViejo(int desfaseCreacion)
    {
        var fecha = new DateOnly(2026, 8, 20);
        var creacion = fecha.AddDays(desfaseCreacion);

        Assert.True(
            ValidacionSeguimientoCalculos.FechaLimiteValidacion(fecha, creacion)
                >= ValidacionSeguimientoCalculos.FechaLimiteValidacion(fecha),
            $"con creación {creacion} el límite nuevo quedó antes que el viejo");
    }

    /// <summary>Un registro validado nunca está en retraso, venga de donde venga el plazo.</summary>
    [Fact]
    public void ValidadoNuncaEstaEnRetraso_TampocoConLaFechaDeCreacion()
    {
        var fecha = new DateOnly(2026, 7, 20);
        var creacion = new DateOnly(2026, 8, 20);
        var hoy = new DateOnly(2026, 9, 30);

        Assert.Equal(EstadoValidacionSeguimiento.Validado,
            ValidacionSeguimientoCalculos.Estado(true, fecha, creacion, hoy));
        Assert.False(ValidacionSeguimientoCalculos.EstaEnRetraso(true, fecha, creacion, hoy));
        Assert.Equal("VALIDADO",
            ValidacionSeguimientoCalculos.EtiquetaEstado(true, fecha, creacion, hoy));
    }
}
