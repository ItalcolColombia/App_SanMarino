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

}
