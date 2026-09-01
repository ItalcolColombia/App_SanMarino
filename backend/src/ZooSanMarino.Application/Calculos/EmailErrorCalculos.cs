namespace ZooSanMarino.Application.Calculos;

/// <summary>Qué le pasó al correo, en términos de qué hacer al respecto.</summary>
public enum ClaseErrorCorreo
{
    /// <summary>El buzón emisor está bloqueado en el proveedor. Reintentar lo mantiene bloqueado.</summary>
    CuentaBloqueada,

    /// <summary>El proveedor rechaza la autenticación (credenciales, política, origen).</summary>
    AutenticacionRechazada,

    /// <summary>El proveedor retiró la autenticación básica para SMTP: no hay contraseña que sirva.</summary>
    AuthBasicaRetirada,

    /// <summary>El destinatario no existe o el buzón no acepta correo.</summary>
    BuzonInvalido,

    /// <summary>Falta STARTTLS de verdad (y no como espejismo de un AUTH fallido).</summary>
    RequiereStartTls,

    /// <summary>Se cayó la red, venció el tiempo, el servidor pidió reintentar más tarde.</summary>
    Transitorio,

    /// <summary>No se pudo clasificar. Se reintenta, porque asumir que es permanente perdería correos.</summary>
    Desconocido
}

/// <summary>
/// Clasifica el fallo de un envío de correo y decide si tiene sentido reintentarlo.
///
/// <para>
/// 🔴 <b>Clasifica por el MENSAJE del servidor, nunca por el <c>StatusCode</c> de
/// <c>SmtpException</c>.</b> .NET mapea a <c>MustIssueStartTlsFirst</c> el <c>530</c> que Office 365
/// devuelve en el <c>MAIL FROM</c> <i>posterior</i> a un AUTH fallido. El emisor evaluaba esa
/// condición primero, así que <b>todos</b> los fallos de autenticación caían ahí y la rama correcta
/// —la del <c>535</c>, con qué pedirle al administrador— era inalcanzable. El texto que quedaba
/// guardado decía «verificá que EnableSsl sea true» y en la línea siguiente informaba que ya era
/// true: mandaba a arreglar lo que estaba bien y escondía que la cuenta estaba bloqueada.
/// </para>
///
/// <para>
/// <b>El orden de evaluación es parte del contrato</b> —lo más específico primero— y está fijado por
/// tests con los mensajes textuales medidos en producción. Mismo cuidado que pide
/// <c>SemanasCicloPosturaCalculos</c> desde que <c>LOHMANN BROWN</c> cayó en el token <c>LOHMANN</c>.
/// </para>
/// </summary>
public static class EmailErrorCalculos
{
    /// <summary>
    /// Clase del error a partir del mensaje del servidor y, como último recurso, del nombre del
    /// <c>StatusCode</c>. Un mensaje vacío es <see cref="ClaseErrorCorreo.Desconocido"/>: se
    /// reintenta, porque descartar un correo por no saber qué pasó es peor que intentarlo de nuevo.
    /// </summary>
    public static ClaseErrorCorreo Clasificar(string? mensaje, string? statusCode = null)
    {
        var m = mensaje ?? "";
        var sc = statusCode ?? "";

        // 1) Cuenta bloqueada: es un 535 pero con una salida distinta (desbloquear el buzón), así que
        //    va ANTES del rechazo genérico de autenticación.
        if (Contiene(m, "account locked") || Contiene(m, "cuenta bloqueada"))
            return ClaseErrorCorreo.CuentaBloqueada;

        // 2) Auth básica retirada: también es un rechazo, pero no se arregla con credenciales ni
        //    con políticas — hay que cambiar de protocolo. Va antes del genérico.
        if (Contiene(m, "5.7.30") || Contiene(m, "basic authentication is not supported"))
            return ClaseErrorCorreo.AuthBasicaRetirada;

        // 3) Rechazo de autenticación. ANTES de MustIssueStartTlsFirst a propósito: cuando el AUTH
        //    falla, el 530 posterior hace que .NET reporte ese StatusCode, y confundirlos fue
        //    exactamente el defecto que este cálculo viene a cerrar.
        if (Contiene(m, "535") || Contiene(m, "5.7.139") || Contiene(m, "5.7.57") ||
            Contiene(m, "authentication unsuccessful") || Contiene(m, "client not authenticated") ||
            Contiene(m, "did not meet the criteria"))
            return ClaseErrorCorreo.AutenticacionRechazada;

        // 4) Destinatario inexistente: reintentar no lo va a crear.
        if (Contiene(m, "5.1.1") || Contiene(m, "recipientnotfound") ||
            Contiene(m, "user unknown") || Contiene(m, "mailbox unavailable") ||
            Contiene(m, "does not exist"))
            return ClaseErrorCorreo.BuzonInvalido;

        // 5) STARTTLS de verdad: solo si NINGUNA de las condiciones de arriba aplicó. Si el mensaje
        //    trae un código de autenticación, el StatusCode es un espejismo y ya se resolvió antes.
        if (Contiene(m, "mustissuestarttlsfirst") || Contiene(sc, "mustissuestarttlsfirst") ||
            Contiene(m, "must issue a starttls command"))
            return ClaseErrorCorreo.RequiereStartTls;

        // 6) Transitorios conocidos.
        if (Contiene(m, "timeout") || Contiene(m, "timed out") ||
            Contiene(m, "4.7.0") || Contiene(m, "4.3.2") || Contiene(m, "try again later") ||
            Contiene(m, "server is busy") || Contiene(m, "connection") && Contiene(m, "closed") ||
            Contiene(sc, "serviceunavailable") || Contiene(sc, "transactionfailed") ||
            Contiene(sc, "generalfailure") && Contiene(m, "socket"))
            return ClaseErrorCorreo.Transitorio;

        return ClaseErrorCorreo.Desconocido;
    }

    /// <summary>
    /// ¿Tiene sentido volver a intentarlo? En los permanentes, <b>no</b>: además de no poder
    /// funcionar, cada reintento es otra autenticación fallida contra el proveedor, que es lo que
    /// dispara y sostiene el bloqueo de la cuenta.
    /// </summary>
    public static bool ValeLaPenaReintentar(ClaseErrorCorreo clase) => clase switch
    {
        ClaseErrorCorreo.CuentaBloqueada        => false,
        ClaseErrorCorreo.AutenticacionRechazada => false,
        ClaseErrorCorreo.AuthBasicaRetirada     => false,
        ClaseErrorCorreo.BuzonInvalido          => false,
        _                                       => true
    };

    /// <summary>Valor para <c>email_queue.error_type</c>: la CAUSA, no «se acabaron los intentos».</summary>
    public static string TipoParaLaCola(ClaseErrorCorreo clase) => clase switch
    {
        ClaseErrorCorreo.CuentaBloqueada        => "cuenta_bloqueada",
        ClaseErrorCorreo.AutenticacionRechazada => "autenticacion_rechazada",
        ClaseErrorCorreo.AuthBasicaRetirada     => "auth_basica_retirada",
        ClaseErrorCorreo.BuzonInvalido          => "buzon_invalido",
        ClaseErrorCorreo.RequiereStartTls       => "requiere_starttls",
        ClaseErrorCorreo.Transitorio            => "transitorio",
        _                                       => "desconocido"
    };

    /// <summary>
    /// Qué pasó y qué hacer, en el idioma de quien va a leer la cola. Sin recetas que contradigan la
    /// configuración vigente: si el problema no está en la app, lo dice.
    /// </summary>
    public static string Diagnostico(ClaseErrorCorreo clase, string? buzonEmisor = null)
    {
        var buzon = string.IsNullOrWhiteSpace(buzonEmisor) ? "el buzón emisor" : buzonEmisor.Trim();

        return clase switch
        {
            ClaseErrorCorreo.CuentaBloqueada =>
                $"La cuenta {buzon} está BLOQUEADA en el proveedor de correo. No es la configuración " +
                "de la aplicación ni la contraseña, y no se arregla reintentando: cada intento es " +
                "otra autenticación fallida y sostiene el bloqueo. Hay que desbloquear el buzón " +
                "desde el administrador de Microsoft 365.",

            ClaseErrorCorreo.AutenticacionRechazada =>
                $"El proveedor rechazó la autenticación de {buzon}. Verificado contra este mismo " +
                "tenant: las credenciales autentican y este mismo código envía desde otra red, así " +
                "que el rechazo depende del ORIGEN de la conexión. Qué pedirle al administrador de " +
                "Microsoft 365: (1) si una política de Conditional Access o Security Defaults bloquea " +
                "la autenticación heredada por ubicación o IP, excluir el origen del servidor; " +
                "(2) SmtpClientAuthenticationDisabled en False, por buzón y por organización. " +
                "Ojo: la IP de salida del servidor es efímera y cambia en cada despliegue, así que " +
                "una excepción por IP no sirve como solución permanente.",

            ClaseErrorCorreo.AuthBasicaRetirada =>
                "El proveedor retiró la autenticación básica para SMTP. No hay contraseña ni política " +
                "que lo resuelva: el camino es OAuth 2.0 (Microsoft Graph) o un relay de correo.",

            ClaseErrorCorreo.BuzonInvalido =>
                "El destinatario no existe o su buzón no acepta correo. Corregir la dirección; " +
                "reintentar no la va a crear.",

            ClaseErrorCorreo.RequiereStartTls =>
                "El servidor pidió STARTTLS antes de continuar. Revisar que el puerto y EnableSsl " +
                "coincidan con lo que espera el proveedor (587 con STARTTLS, o 465 con TLS implícito).",

            ClaseErrorCorreo.Transitorio =>
                "Falla temporal (red, tiempo de espera o servidor ocupado). Se reintenta solo, con " +
                "espera creciente entre intentos.",

            _ =>
                "No se pudo clasificar el error; el detalle SMTP completo está más abajo. Se reintenta " +
                "por las dudas: descartar un correo por no saber qué pasó es peor que volver a intentarlo."
        };
    }

    /// <summary>
    /// Cuánto esperar antes del próximo intento: <b>1, 5 y 15 minutos</b>. Creciente para no golpear
    /// al proveedor en ráfaga —que es lo que convierte una falla pasajera en un bloqueo— y acotado,
    /// para que un correo no quede dando vueltas un día entero.
    /// </summary>
    public static TimeSpan EsperaAntesDelProximoIntento(int intentosYaHechos)
    {
        var minutos = intentosYaHechos switch
        {
            <= 0 => 1,
            1    => 1,
            2    => 5,
            _    => 15
        };
        return TimeSpan.FromMinutes(minutos);
    }

    private static bool Contiene(string texto, string aguja) =>
        texto.Contains(aguja, StringComparison.OrdinalIgnoreCase);
}
