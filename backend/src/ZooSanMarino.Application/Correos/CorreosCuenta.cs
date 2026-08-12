namespace ZooSanMarino.Application.Correos;

/// <summary>
/// Cuerpos de los correos de cuenta: restablecimiento de contraseña, contraseña asignada por un
/// administrador y bienvenida.
///
/// Están acá — y no dentro de <c>EmailService</c> — porque son funciones puras y porque lo que
/// tienen que decir es delicado: hasta el 12-ago-2026 el correo de recuperación imprimía el
/// <b>token de un solo uso</b> bajo el rótulo «Tu nueva contraseña es», de modo que quien lo recibía
/// intentaba entrar con 64 caracteres que nunca fueron una contraseña. Vivir en Application los deja
/// cubiertos por los tests que corren en el gate de CI.
/// </summary>
public static class CorreosCuenta
{
    /// <summary>Ruta del frontend que canjea el token por una contraseña nueva.</summary>
    public const string RutaRestablecer = "/reset-password";

    /// <summary>Minutos que vive el token (los fija <c>AuthService</c> al emitirlo).</summary>
    public const int MinutosVigencia = 15;

    /// <summary>
    /// Arma el enlace de restablecimiento. El token se codifica para URL: es CSPRNG en base64 y
    /// puede traer <c>+</c> o <c>/</c>, que sin codificar llegan mutilados al frontend.
    /// </summary>
    public static string ConstruirEnlaceRestablecer(string applicationUrl, string token) =>
        $"{(applicationUrl ?? string.Empty).TrimEnd('/')}{RutaRestablecer}?token={Uri.EscapeDataString(token ?? string.Empty)}";

    // ─────────────────────────── Restablecer (autoservicio) ───────────────────────────

    /// <summary>
    /// Correo de «olvidé mi contraseña»: lleva un ENLACE, nunca el secreto en el cuerpo. Si el correo
    /// se reenvía o queda en un buzón compartido, el enlace ya venció o ya se usó.
    /// </summary>
    public static string RestablecerContrasena(
        string marca, string lema, string logoUrl, string applicationUrl, string token, string? nombre)
    {
        var enlace = ConstruirEnlaceRestablecer(applicationUrl, token);

        var contenido = string.Concat(
            EmailComponentes.Titulo("Restablecé tu contraseña"),
            EmailComponentes.Saludo(nombre),
            EmailComponentes.Parrafo(
                $"Recibimos una solicitud para restablecer la contraseña de tu cuenta en {marca}. " +
                "Elegí una contraseña nueva desde el siguiente botón."),
            EmailComponentes.Boton(enlace, "Crear contraseña nueva"),
            EmailComponentes.EnlaceRespaldo(enlace),
            EmailComponentes.CalloutAviso(
                $"El enlace vence en {MinutosVigencia} minutos",
                "Sirve una sola vez. Si se te vence, volvé a pedir el restablecimiento desde la pantalla de ingreso."),
            EmailComponentes.Separador(),
            EmailComponentes.Nota(
                "Si no pediste este cambio, podés ignorar este mensaje: tu contraseña actual sigue " +
                "funcionando y nadie puede usar el enlace sin acceso a este correo."));

        return EmailLayout.Documento(
            titulo: $"Restablecé tu contraseña · {marca}",
            preheader: $"Creá una contraseña nueva. El enlace vence en {MinutosVigencia} minutos.",
            logoUrl: logoUrl,
            marca: marca,
            lema: lema,
            contenidoHtml: contenido,
            motivoEnvio: "Recibís este correo porque se solicitó restablecer la contraseña de esta cuenta.");
    }

    // ─────────────────────────── Restablecida por un administrador ───────────────────────────

    /// <summary>
    /// Correo del reset hecho DESDE la administración de usuarios: acá sí viaja una contraseña real,
    /// porque el administrador ya la fijó en la cuenta y el usuario no tiene otra forma de conocerla.
    /// </summary>
    public static string ContrasenaRestablecidaPorAdmin(
        string marca, string lema, string logoUrl, string applicationUrl, string contrasena, string? nombre)
    {
        var contenido = string.Concat(
            EmailComponentes.Titulo("Tu contraseña fue restablecida"),
            EmailComponentes.Saludo(nombre),
            EmailComponentes.Parrafo(
                $"Un administrador de {marca} restableció la contraseña de tu cuenta. " +
                "Usá esta contraseña temporal para ingresar:"),
            EmailComponentes.BloqueCodigo("Contraseña temporal", contrasena),
            EmailComponentes.CalloutAviso(
                "Cambiala en cuanto ingreses",
                "Esta clave la conoce quien la generó. Cambiala desde tu perfil apenas entres a la plataforma."),
            EmailComponentes.Boton($"{(applicationUrl ?? string.Empty).TrimEnd('/')}/login", "Iniciar sesión"),
            EmailComponentes.Separador(),
            EmailComponentes.Nota(
                "Si no esperabas este cambio, avisá al administrador del sistema antes de ingresar."));

        return EmailLayout.Documento(
            titulo: $"Tu contraseña fue restablecida · {marca}",
            preheader: "Un administrador restableció tu contraseña. Ingresá y cambiala.",
            logoUrl: logoUrl,
            marca: marca,
            lema: lema,
            contenidoHtml: contenido,
            motivoEnvio: "Recibís este correo porque un administrador restableció la contraseña de tu cuenta.");
    }

    // ─────────────────────────── Bienvenida ───────────────────────────

    /// <summary>Alta de usuario: credenciales de acceso y los primeros pasos.</summary>
    public static string Bienvenida(
        string marca, string lema, string logoUrl, string applicationUrl,
        string correo, string contrasena, string? nombre)
    {
        var url = (applicationUrl ?? string.Empty).TrimEnd('/');

        var contenido = string.Concat(
            EmailComponentes.Titulo("Tu cuenta ya está lista"),
            EmailComponentes.Saludo(nombre),
            EmailComponentes.Parrafo(
                $"Te damos la bienvenida a {marca}. Creamos tu cuenta con estas credenciales de acceso:"),
            EmailComponentes.Ficha(("Usuario", correo)),
            EmailComponentes.BloqueCodigo("Contraseña temporal", contrasena),
            EmailComponentes.ParrafoHtml("<strong>Cómo empezar</strong>"),
            EmailComponentes.Pasos(
                "Ingresá con el usuario y la contraseña temporal de arriba.",
                "Cambiá la contraseña desde tu perfil: la temporal la conoce quien creó la cuenta.",
                "Si trabajás en campo, instalá la aplicación en el dispositivo y entrá una vez con red."),
            EmailComponentes.Boton($"{url}/login", "Entrar a la plataforma"),
            EmailComponentes.Separador(),
            EmailComponentes.Nota(
                "¿Dudas o no reconocés esta cuenta? Escribile al administrador del sistema."));

        return EmailLayout.Documento(
            titulo: $"Bienvenido a {marca}",
            preheader: "Tu cuenta está lista. Estas son tus credenciales de acceso.",
            logoUrl: logoUrl,
            marca: marca,
            lema: lema,
            contenidoHtml: contenido,
            motivoEnvio: "Recibís este correo porque se creó una cuenta a tu nombre en la plataforma.");
    }
}
