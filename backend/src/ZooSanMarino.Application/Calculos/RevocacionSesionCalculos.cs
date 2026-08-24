// src/ZooSanMarino.Application/Calculos/RevocacionSesionCalculos.cs
// Regla PURA de si una sesión sigue viva. Sin EF, sin estado, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>Estado de una sesión frente a la tabla <c>sesiones_activas</c>.</summary>
public enum EstadoSesion
{
    /// <summary>
    /// Token sin claim <c>jti</c>: emitido ANTES de B1. <b>Ya NO pasa</b> — la ventana de gracia se
    /// cerró el 21-ago-2026 (V39.13). Ver la nota de <see cref="Evaluar"/>.
    /// </summary>
    Legado = 0,

    /// <summary>Hay <c>jti</c> y hay fila viva: la sesión existe y nadie la apagó.</summary>
    Valida = 1,

    /// <summary>Hay <c>jti</c> pero NO hay fila. <b>Fail-closed</b>: sin fila no hay sesión.</summary>
    NoRegistrada = 2,

    /// <summary>La fila tiene <c>revoked_at</c>: alguien la apagó a propósito.</summary>
    Revocada = 3,

    /// <summary>La fila venció (<c>expires_at &lt;= ahora</c>).</summary>
    Vencida = 4,

    /// <summary>
    /// <b>No se pudo consultar la base.</b> Se acepta el token a propósito (<i>fail-open</i>): si RDS
    /// se cae, rechazar todo convertiría una caída de base en el deslogueo simultáneo de todas las
    /// tablets en campo. Es el único estado «indeterminado» y <b>nunca</b> se cachea.
    ///
    /// <para>
    /// Existe porque hasta V39.13 este caso compartía valor con <see cref="Legado"/>, y cerrar la
    /// ventana de gracia sin separarlos habría convertido un blip de RDS en un logout masivo.
    /// </para>
    /// </summary>
    NoVerificable = 5,
}

/// <summary>
/// Decide si un JWT de usuario sigue habilitado, más allá de que su firma y su <c>exp</c> sean
/// válidos. Es el corazón de <b>B1 — revocación de sesión</b>.
///
/// <para>
/// <b>Por qué existe.</b> Hasta ago-2026 un JWT emitido era irrevocable: una tablet perdida seguía
/// entrando hasta que el token venciera por tiempo, y cambiar la contraseña —o desactivar al
/// usuario— <b>no invalidaba nada</b>. El servidor deja de confiar en el token y pasa a confiar en
/// una fila de BD (<c>sesiones_activas</c>, lista BLANCA por <c>jti</c>): sin fila, no hay sesión.
/// </para>
///
/// <para>
/// <b>La vigencia del token deja de ser el mecanismo de revocación.</b> Por eso —y sólo porque esta
/// verificación entra en el mismo despliegue— <c>Jwt:DurationInMinutes</c> puede alinearse con la
/// jornada offline de 16 h (<c>politica-sesion.funcion.ts</c>) en vez de expulsar al operario al
/// minuto 61 sin señal. Subir la vigencia SIN esta verificación sería emitir tokens de 16 horas que
/// nadie puede apagar: entra completo o no entra.
/// </para>
///
/// <para>
/// ⚠️ <b>Lo que esto NO compra.</b> Un dispositivo que nunca vuelve a ver la red no se puede revocar
/// —ni con esto ni con ningún diseño de servidor—. Lo que sí garantiza es que ese aparato no vuelve
/// a entrar (ni a leer, ni a escribir, ni a sincronizar) apenas toque una red.
/// </para>
/// </summary>
public static class RevocacionSesionCalculos
{
    /// <summary><c>errorCode</c> que el cliente distingue para cerrar la sesión (no es un fallo de plataforma).</summary>
    public const string MotivoRevocada = "sesion-revocada";

    /// <summary><c>errorCode</c> de token vencido: el cliente ya lo trata como fin de sesión normal.</summary>
    public const string MotivoExpirado = "token-expirado";

    /// <summary>Permiso que habilita revocar sesiones de OTROS usuarios (además del super admin).</summary>
    public const string PermisoRevocarSesion = "usuarios.revocar_sesion";

    /// <summary>
    /// Ventana anti-escritura de <c>last_seen_at</c>: el heartbeat llega cada 90 s y un <c>UPDATE</c>
    /// por request sería peor que el <c>SELECT</c> que la caché evita.
    /// </summary>
    public static readonly TimeSpan UmbralUltimaVistaPorDefecto = TimeSpan.FromMinutes(5);

    /// <summary>TTL de caché de una sesión VÁLIDA: cota superior de cuánto tarda en surtir efecto una revocación.</summary>
    public static readonly TimeSpan TtlSesionValida = TimeSpan.FromSeconds(60);

    /// <summary>
    /// ¿Sigue viva la sesión de este token?
    ///
    /// <para>
    /// <b>Ventana de gracia — CERRADA el 21-ago-2026 (V39.13).</b> <paramref name="jti"/> nulo o vacío
    /// ⇒ <see cref="EstadoSesion.Legado"/>, que ahora <b>se RECHAZA</b>.
    /// </para>
    ///
    /// <para>
    /// Al desplegar B1 (20-ago-2026, TaskDef <c>sanmarino-back-task:161</c>) todos los tokens vivos
    /// eran de antes y no tenían <c>jti</c>: rechazarlos ese día habría deslogueado a todo el mundo de
    /// golpe, tablets en campo con capturas sin subir incluidas. Por eso se aceptaron. La ventana se
    /// apaga <b>sola</b> porque el token viejo dura lo que duraba —<c>JwtSettings__DurationInMinutes
    /// = 60</c> en la TaskDef—, así que un día después ya no quedaba ni uno vivo, y hoy la única
    /// fábrica de tokens de usuario (<c>AuthService</c>, un solo <c>new JwtSecurityToken</c> en todo
    /// el backend) siempre emite <c>jti</c> y anota la fila. Un token sin <c>jti</c> a partir de acá
    /// no es un rezagado: es un token que este backend no emitió.
    /// </para>
    ///
    /// <para>
    /// ⚠️ <b>Lo que NO se cerró.</b> El <i>fail-open</i> ante una caída de base sigue vivo y es otra
    /// cosa: vive en <c>SesionActivaService</c> y devuelve <see cref="EstadoSesion.NoVerificable"/>.
    /// Los dos casos compartían el valor <see cref="EstadoSesion.Legado"/> hasta V39.13; borrar la
    /// rama sin separarlos convertía un blip de RDS en un logout masivo.
    /// </para>
    /// </summary>
    /// <param name="jti">Claim <c>jti</c> del token, o <c>null</c> si el token es anterior a B1.</param>
    /// <param name="hayFila">¿Existe fila en <c>sesiones_activas</c> para ese <c>jti</c>?</param>
    /// <param name="revokedAt">Momento de revocación de la fila, o <c>null</c> si sigue activa.</param>
    /// <param name="expiresAt">Vencimiento de la fila (mismo instante que el <c>exp</c> del token).</param>
    /// <param name="ahoraUtc">Reloj, inyectado para poder testear los bordes.</param>
    public static EstadoSesion Evaluar(
        string? jti,
        bool hayFila,
        DateTime? revokedAt,
        DateTime? expiresAt,
        DateTime ahoraUtc)
    {
        if (string.IsNullOrWhiteSpace(jti))
            return EstadoSesion.Legado;

        if (!hayFila)
            return EstadoSesion.NoRegistrada;

        // Precedencia estable: revocada gana sobre vencida. Una sesión apagada a mano y además
        // vencida se explica al usuario como revocada, que es la información útil.
        if (revokedAt.HasValue)
            return EstadoSesion.Revocada;

        // `<=` coherente con ClockSkew = Zero en TokenValidationParameters.
        if (expiresAt.HasValue && expiresAt.Value <= ahoraUtc)
            return EstadoSesion.Vencida;

        return EstadoSesion.Valida;
    }

    /// <summary>
    /// ¿El estado deja pasar el request? Sólo <see cref="EstadoSesion.Valida"/> (la sesión existe y
    /// nadie la apagó) y <see cref="EstadoSesion.NoVerificable"/> (no se pudo preguntar; fail-open
    /// deliberado). <see cref="EstadoSesion.Legado"/> dejó de pasar el 21-ago-2026 — V39.13.
    /// </summary>
    public static bool EsSesionValida(EstadoSesion estado) =>
        estado is EstadoSesion.Valida or EstadoSesion.NoVerificable;

    /// <summary>
    /// <c>errorCode</c> que viaja al cliente en el cuerpo del 401. Nunca <c>null</c> para un estado
    /// inválido: el front decide si cierra la sesión leyendo exactamente este valor.
    /// </summary>
    public static string? MotivoParaCliente(EstadoSesion estado) => estado switch
    {
        EstadoSesion.Revocada => MotivoRevocada,
        EstadoSesion.NoRegistrada => MotivoRevocada,
        // V39.13: un token sin `jti` ya no pasa. Se le dice al cliente lo mismo que a una sesión
        // apagada —«iniciá sesión de nuevo»—, que es exactamente lo que tiene que hacer: el login
        // emite un token nuevo, con `jti` y con su fila.
        EstadoSesion.Legado => MotivoRevocada,
        EstadoSesion.Vencida => MotivoExpirado,
        _ => null,
    };

    /// <summary>
    /// ¿Toca escribir <c>last_seen_at</c>? Sin marca previa siempre sí; con marca, sólo pasado el
    /// umbral. Mantiene el camino caliente libre de escrituras.
    /// </summary>
    public static bool DebeActualizarUltimaVista(
        DateTime? ultimaVista,
        DateTime ahoraUtc,
        TimeSpan? umbral = null)
    {
        if (!ultimaVista.HasValue) return true;
        var ventana = umbral ?? UmbralUltimaVistaPorDefecto;
        return ahoraUtc - ultimaVista.Value >= ventana;
    }

    /// <summary>
    /// ¿Puede revocar la sesión de OTRO? Super admin (dato <c>users.is_super_admin</c>) o el permiso
    /// <see cref="PermisoRevocarSesion"/>. <b>Fail-closed</b>: sin permisos ⇒ <c>false</c>.
    /// No mira nombres de empresa: la revocación es infraestructura de autenticación, no una feature
    /// por empresa.
    /// </summary>
    public static bool PuedeRevocarSesionDeOtro(bool esSuperAdmin, IEnumerable<string>? permisos)
    {
        if (esSuperAdmin) return true;
        if (permisos is null) return false;

        return permisos.Any(p =>
            string.Equals(p?.Trim(), PermisoRevocarSesion, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ¿Es suya la sesión? Se compara contra el Guid del token ya validado, <b>nunca</b> contra un id
    /// del body. Sin Guid ⇒ <c>false</c>.
    /// </summary>
    public static bool PuedeRevocarSesionPropia(Guid? userIdActual, Guid userIdDeLaSesion) =>
        userIdActual.HasValue &&
        userIdActual.Value != Guid.Empty &&
        userIdActual.Value == userIdDeLaSesion;

    /// <summary>
    /// Cuánto cachear el veredicto.
    ///
    /// <para>
    /// Una sesión <b>válida</b> se cachea poco (60 s): esa es la cota de cuánto tarda una revocación
    /// en surtir efecto en cada tarea ECS, y es lo que hay que decirle al usuario en la UI —«en menos
    /// de un minuto desde que el dispositivo toque la red»—, no «inmediato».
    /// Un estado <b>muerto</b> se cachea hasta el <c>exp</c> del token: una sesión revocada no
    /// resucita (para volver hay que hacer login, que emite otro <c>jti</c>), así que preguntar de
    /// nuevo es gasto puro.
    /// </para>
    /// </summary>
    public static TimeSpan TtlCache(EstadoSesion estado, DateTime expiracionToken, DateTime ahoraUtc)
    {
        if (estado == EstadoSesion.Valida)
            return TtlSesionValida;

        // Un veredicto que no se pudo verificar no se cachea NUNCA: cachearlo hasta el `exp` sería
        // convertir un blip de RDS en una hora de barra libre para ese token. `SesionActivaService`
        // ni siquiera llega a esta línea en ese caso (retorna antes de tocar la caché); el cero está
        // acá para que la regla viva en la parte pura y testeable.
        if (estado == EstadoSesion.NoVerificable)
            return TimeSpan.Zero;

        var restante = expiracionToken - ahoraUtc;
        return restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
    }
}
