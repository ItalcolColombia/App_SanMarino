using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Módulos de seguimiento diario que participan de la doble validación. Se persiste el string en
/// <c>origen_modulo</c> de las tablas de separación, así que los valores son parte del contrato de
/// datos: no se renombran.
/// </summary>
public static class ModuloSeguimiento
{
    public const string Levante        = "LEVANTE";
    public const string Produccion     = "PRODUCCION";
    public const string Engorde        = "ENGORDE";
    public const string EngordeEcuador = "ENGORDE_EC";
    public const string Reproductora   = "REPRODUCTORA";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Levante, Produccion, Engorde, EngordeEcuador, Reproductora };

    public static bool EsValido(string? modulo) =>
        !string.IsNullOrWhiteSpace(modulo) && Todos.Contains(modulo);

    /// <summary>True para los módulos de postura, donde el alimento se captura por sexo.</summary>
    public static bool EsPostura(string modulo) =>
        Levante.Equals(modulo, StringComparison.OrdinalIgnoreCase) ||
        Produccion.Equals(modulo, StringComparison.OrdinalIgnoreCase);

    /// <summary>True para los dos servicios de pollo engorde (Colombia/Panamá y Ecuador).</summary>
    public static bool EsEngorde(string modulo) =>
        Engorde.Equals(modulo, StringComparison.OrdinalIgnoreCase) ||
        EngordeEcuador.Equals(modulo, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Literal con el que se GUARDAN y se BUSCAN las reservas y el estado de un registro.
    ///
    /// <para>
    /// <see cref="EngordeEcuador"/> colapsa a <see cref="Engorde"/> porque los dos services de pollo
    /// engorde escriben en la MISMA tabla (<c>seguimiento_diario_aves_engorde</c>): son dos services
    /// sobre un solo esquema, no dos esquemas. Sin colapsar, un registro creado por el service de
    /// Ecuador reservaba como <c>ENGORDE_EC</c> y el front lo validaba como <c>ENGORDE</c> — la
    /// consulta de reservas no encontraba nada y el registro quedaba <c>validado = true</c> sin haber
    /// descontado ni un kilo, con la reserva activa para siempre.
    /// </para>
    ///
    /// <para>
    /// Los dos literales siguen siendo válidos en la API: lo que se unifica es la CLAVE, no el
    /// vocabulario que aceptan los endpoints ni el service que llama.
    /// </para>
    /// </summary>
    public static string Canonico(string modulo) =>
        EngordeEcuador.Equals(modulo, StringComparison.OrdinalIgnoreCase) ? Engorde : modulo;
}

/// <summary>Estado de un registro de seguimiento frente a la doble validación.</summary>
public enum EstadoValidacionSeguimiento
{
    /// <summary>Ya validado: el consumo se aplicó al inventario y las aves se descontaron.</summary>
    Validado,
    /// <summary>Sin validar, dentro del plazo. Se puede editar y eliminar.</summary>
    Pendiente,
    /// <summary>Sin validar y vencido: fila roja, alarma y bloqueo de días nuevos.</summary>
    EnRetraso
}

/// <summary>
/// Reglas PURAS de la doble validación de los seguimientos diarios (levante, producción, pollo
/// engorde y reproductora).
///
/// <para>
/// El punto de toda la clase es que la decisión «¿esto descuenta ahora o se separa hasta validar?»
/// viva en UN solo lugar y esté cubierta por tests, porque el descuento está escrito cinco veces (los
/// dos servicios de engorde, levante, producción y reproductora) y cada copia tiene su propio camino
/// de inventario por país. Los services solo resuelven el flag de la empresa y delegan acá.
/// </para>
///
/// <para>
/// <b>Invariante que sostienen los tests:</b> con <c>empresaRequiereValidacion = false</c> todas las
/// respuestas reproducen el comportamiento previo — descuenta al guardar, no separa nada, nada
/// bloquea y ningún registro se pinta en rojo—. El flag apagado tiene que ser indistinguible del
/// código que había antes de esta funcionalidad.
/// </para>
/// </summary>
public static class ValidacionSeguimientoCalculos
{
    /// <summary>
    /// Plazo para validar, en días, contado desde la fecha del seguimiento. Lo fijó el usuario:
    /// «los registros tienen que ser validados con un día de diferencia como máximo».
    /// </summary>
    public const int DiasPlazoValidacion = 1;

    // ─── Gate del flag ────────────────────────────────────────────────────────

    /// <summary>
    /// True si el registro debe descontar alimento y aves <b>en el momento de guardar</b>, que es lo
    /// que hacían los cinco services antes de esta funcionalidad. Con la doble validación encendida
    /// el descuento se posterga hasta validar.
    /// </summary>
    public static bool DescuentaAlGuardar(bool empresaRequiereValidacion) => !empresaRequiereValidacion;

    /// <summary>
    /// True si al guardar hay que <b>separar</b> (reservar) el alimento y las aves en vez de
    /// descontarlos. Es el complemento exacto de <see cref="DescuentaAlGuardar"/>: nunca las dos
    /// cosas a la vez, que sería contar el consumo dos veces.
    /// </summary>
    public static bool SeparaAlGuardar(bool empresaRequiereValidacion) => empresaRequiereValidacion;

    // ─── Estado del registro ──────────────────────────────────────────────────

    /// <summary>Último día en que el registro puede validarse sin quedar en retraso.</summary>
    public static DateOnly FechaLimiteValidacion(DateOnly fechaSeguimiento) =>
        fechaSeguimiento.AddDays(DiasPlazoValidacion);

    /// <summary>
    /// Estado derivado del registro. No se persiste: se calcula con la fecha del seguimiento y el día
    /// de hoy, así un registro «pendiente» pasa solo a «en retraso» al cambiar el día, sin que nadie
    /// tenga que correr un proceso.
    /// </summary>
    public static EstadoValidacionSeguimiento Estado(bool validado, DateOnly fechaSeguimiento, DateOnly hoy)
    {
        if (validado) return EstadoValidacionSeguimiento.Validado;
        return hoy > FechaLimiteValidacion(fechaSeguimiento)
            ? EstadoValidacionSeguimiento.EnRetraso
            : EstadoValidacionSeguimiento.Pendiente;
    }

    /// <summary>True si el registro ya venció su plazo y sigue sin validar.</summary>
    public static bool EstaEnRetraso(bool validado, DateOnly fechaSeguimiento, DateOnly hoy) =>
        Estado(validado, fechaSeguimiento, hoy) == EstadoValidacionSeguimiento.EnRetraso;

    /// <summary>
    /// Literal del estado tal como viaja al front (<c>VALIDADO</c> | <c>PENDIENTE</c> |
    /// <c>EN_RETRASO</c>). Existe para que el guion bajo no dependa de cómo se llame el miembro del
    /// enum: <c>EnRetraso.ToString()</c> da <c>EnRetraso</c> y el front compara contra
    /// <c>EN_RETRASO</c>.
    /// </summary>
    public static string Etiqueta(EstadoValidacionSeguimiento estado) => estado switch
    {
        EstadoValidacionSeguimiento.Validado => "VALIDADO",
        EstadoValidacionSeguimiento.EnRetraso => "EN_RETRASO",
        _ => "PENDIENTE"
    };

    /// <summary>Atajo: estado de un registro ya como literal para el DTO.</summary>
    public static string EtiquetaEstado(bool validado, DateOnly fechaSeguimiento, DateOnly hoy) =>
        Etiqueta(Estado(validado, fechaSeguimiento, hoy));

    // ─── Qué se puede hacer con el registro ───────────────────────────────────

    /// <summary>
    /// True si el registro admite edición o borrado.
    ///
    /// <para>
    /// Con el flag apagado devuelve siempre <c>true</c>: hoy cualquier registro se edita y se borra, y
    /// esta funcionalidad no puede quitarle esa libertad a las empresas que no la pidieron. Con el
    /// flag encendido, validar es el punto de no retorno — es justamente lo que le da sentido a la
    /// separación: mientras no esté validado no se descontó nada, así que editar no obliga a ningún
    /// cálculo de retorno.
    /// </para>
    /// </summary>
    public static bool EsEditable(bool empresaRequiereValidacion, bool validado) =>
        !empresaRequiereValidacion || !validado;

    /// <summary>
    /// Mensaje del rechazo al intentar editar o eliminar un registro ya validado. Nombra la acción
    /// para que el usuario sepa cuál es la salida (des-validar) y no crea que el registro se trabó.
    /// </summary>
    public static string MensajeRegistroValidado(string accion) =>
        $"El registro está validado y no se puede {accion}. " +
        "Para corregirlo hay que quitarle la validación primero (requiere permiso).";

    // ─── Bloqueo por registros vencidos ───────────────────────────────────────

    /// <summary>
    /// True si el lote no debe aceptar un seguimiento nuevo por tener registros vencidos sin validar.
    /// Decisión del usuario (14ago26): el retraso además de avisar, <b>bloquea</b>.
    ///
    /// <para>
    /// Con el flag apagado nunca bloquea, aunque haya registros viejos sin validar: en esas empresas
    /// la columna <c>validado</c> quedó en <c>true</c> por el backfill y el concepto no existe.
    /// </para>
    /// </summary>
    public static bool BloqueaAltaPorVencidos(bool empresaRequiereValidacion, int cantidadVencidos) =>
        empresaRequiereValidacion && cantidadVencidos > 0;

    /// <summary>
    /// Texto del bloqueo. Lista las fechas concretas —hasta <paramref name="maximoFechas"/>— porque un
    /// «tiene registros pendientes» sin decir cuáles obliga al usuario a buscarlos a mano en la tabla.
    /// </summary>
    public static string MensajeBloqueoPorVencidos(IReadOnlyCollection<DateOnly> fechasVencidas, int maximoFechas = 5)
    {
        if (fechasVencidas is null || fechasVencidas.Count == 0)
            return "Hay registros de seguimiento sin validar que superaron el plazo.";

        var ordenadas = fechasVencidas.OrderBy(f => f).ToList();
        var visibles = ordenadas.Take(maximoFechas)
            .Select(f => f.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        var lista = string.Join(", ", visibles);
        var resto = ordenadas.Count - Math.Min(maximoFechas, ordenadas.Count);
        if (resto > 0) lista += $" y {resto} más";

        // La concordancia se arma entera, no solo el sustantivo: con "un registro ... que superaron"
        // el mensaje se lee como un error del sistema y le resta credibilidad al resto del aviso.
        var (sujeto, verbo, cierre) = ordenadas.Count == 1
            ? ("un registro", "superó", "Validá ese registro y volvé a intentar.")
            : ($"{ordenadas.Count} registros", "superaron", "Validá esos registros y volvé a intentar.");

        return $"No se puede registrar un día nuevo: el lote tiene {sujeto} sin validar que {verbo} el plazo " +
               $"({lista}). {cierre}";
    }

    /// <summary>
    /// Texto del modal de alerta que aparece al entrar al lote. Es un aviso, no un rechazo, así que
    /// se separa del mensaje de bloqueo: dicen cosas distintas y se muestran en momentos distintos.
    /// </summary>
    public static string MensajeAlertaPendientes(int cantidadVencidos, int cantidadPendientes)
    {
        if (cantidadVencidos <= 0 && cantidadPendientes <= 0) return string.Empty;

        if (cantidadVencidos <= 0)
            return cantidadPendientes == 1
                ? "Hay 1 registro de seguimiento pendiente de validar."
                : $"Hay {cantidadPendientes} registros de seguimiento pendientes de validar.";

        var enRetraso = cantidadVencidos == 1
            ? "1 registro EN RETRASO (superó el plazo de validación)"
            : $"{cantidadVencidos} registros EN RETRASO (superaron el plazo de validación)";

        var otros = cantidadPendientes > cantidadVencidos
            ? $" y {cantidadPendientes - cantidadVencidos} pendientes dentro del plazo"
            : string.Empty;

        return $"Este lote tiene {enRetraso}{otros}. " +
               "Mientras no se validen, el lote no acepta registros de días nuevos.";
    }
}
