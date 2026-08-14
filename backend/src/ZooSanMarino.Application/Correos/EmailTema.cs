namespace ZooSanMarino.Application.Correos;

/// <summary>
/// Tokens visuales de los correos salientes. Existe para que los siete cuerpos que manda la
/// aplicación compartan una sola paleta: antes cada plantilla traía su propio bloque de estilos y
/// el dorado <c>#f4b428</c> convivía con el verde de tickets sin ninguna regla.
///
/// Se alinea con la paleta de marca del repositorio (<c>CLAUDE.md</c>): naranja para acciones,
/// verde solo para éxito y rojo solo para peligro.
/// </summary>
public static class EmailTema
{
    // ── Marca y acciones ──────────────────────────────────────────────────────
    /// <summary>Naranja Italfoods. Es el color de las ACCIONES (botones, acentos).</summary>
    public const string Accion = "#e85c25";
    /// <summary>Naranja oscuro para bordes del botón (Outlook no dibuja sombras).</summary>
    public const string AccionOscuro = "#c44a19";

    // ── Semánticos ────────────────────────────────────────────────────────────
    /// <summary>Verde: exclusivo de estados de éxito (ticket solucionado / cerrado bien).</summary>
    public const string Exito = "#2d7a3e";
    public const string ExitoFondo = "#f0fdf4";
    /// <summary>Ámbar: advertencias (vigencia de un enlace, "cambiá la contraseña").</summary>
    public const string Aviso = "#b45309";
    public const string AvisoFondo = "#fffbeb";
    /// <summary>Rojo: solo peligro (acción irreversible, "no fuiste vos").</summary>
    public const string Peligro = "#b91c1c";
    public const string PeligroFondo = "#fef2f2";
    /// <summary>Azul informativo, sin carga semántica de estado.</summary>
    public const string Info = "#1d4ed8";
    public const string InfoFondo = "#eff6ff";

    // ── Neutros ───────────────────────────────────────────────────────────────
    public const string Texto = "#1f2937";
    public const string TextoSuave = "#4b5563";
    public const string TextoTenue = "#6b7280";
    public const string Borde = "#e5e7eb";
    public const string BordeSuave = "#f1f5f9";
    public const string Superficie = "#ffffff";
    public const string SuperficieSuave = "#f9fafb";
    /// <summary>Fondo de la ventana del cliente de correo (ital-cream apagado).</summary>
    public const string Lienzo = "#f4f2ef";

    // ── Tipografía y medidas ──────────────────────────────────────────────────
    /// <summary>Pila web-safe: los clientes de correo no cargan fuentes externas de forma fiable.</summary>
    public const string Fuente = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";
    public const string FuenteMono = "'SFMono-Regular',Consolas,'Liberation Mono',Menlo,monospace";
    /// <summary>600 px es el ancho seguro histórico: entra en el panel de lectura de Outlook.</summary>
    public const int AnchoMaximo = 600;
}
