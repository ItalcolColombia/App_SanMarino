// src/ZooSanMarino.Application/Calculos/MigracionAutorizacionCalculos.cs
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Quién puede usar el módulo de Migraciones Masivas, y con qué tipo. Cálculo PURO (sin EF ni
/// HttpContext) para poder fijarlo con tests: el filtro de la capa API resuelve el tipo de la
/// request y delega.
///
/// <para>
/// <b>Por qué existe.</b> El módulo estaba gateado únicamente en la UI: <c>MigracionController</c> no
/// tenía un solo <c>[Authorize]</c> por permiso (solo la <c>FallbackPolicy</c> = token válido) y la
/// ruta del front no llevaba <c>permissionGuard</c>. Cualquier sesión autenticada que escribiera la
/// URL a mano podía importar. No era una fuga entre empresas —la empresa efectiva la valida
/// <c>ActiveCompanyMiddleware</c>— pero sí una escritura masiva sin autorización.
/// </para>
///
/// <para>
/// <b>El permiso depende del TIPO, no del método HTTP</b> (a diferencia de
/// <c>GestionUsuariosAutorizacionCalculos</c>, donde alcanzaba con separar lecturas de escrituras):
/// las dos líneas del módulo tienen permisos distintos y el tipo viaja como PARÁMETRO de la request,
/// así que un <c>[Authorize(Policy = …)]</c> plano no alcanza.
/// </para>
/// </summary>
public static class MigracionAutorizacionCalculos
{
    /// <summary>Permiso de la línea POSTURA (Seguimiento Levante y Producción).</summary>
    public const string PermisoPostura = "carga_masiva_postura";

    /// <summary>Permiso de la línea POLLO ENGORDE.</summary>
    public const string PermisoPolloEngorde = "carga_masiva_pollo_engorde";

    /// <summary>
    /// Tipos de la línea Engorde. Espejo EXACTO de <c>TIPOS_POLLO_ENGORDE</c> del front
    /// (<c>agrupar-tipo-migracion.funcion.ts</c>), que es el que decide qué tiles se ofrecen: si las
    /// dos listas se desincronizaran, el front ofrecería un tile que el backend rechaza con 403.
    /// Todo lo que no está acá es Postura.
    /// </summary>
    private static readonly HashSet<TipoMigracion> LineaEngorde = new()
    {
        TipoMigracion.LotesPolloEngorde,
        TipoMigracion.SeguimientoPolloEngorde,
        TipoMigracion.SeguimientoReproductoraEngorde,
        TipoMigracion.VentaPolloEngorde,
    };

    /// <summary>
    /// Tipos de ESTRUCTURA (Granjas/Núcleos/Galpones). Están retirados de la pantalla pero siguen
    /// vivos en el backend, así que hoy los puede disparar cualquier sesión con token. No pertenecen
    /// a ninguna de las dos líneas: se exige <b>cualquiera</b> de los dos permisos, que es lo más
    /// estricto que se puede pedir sin inventar una key que nadie tiene (y que dejaría el camino
    /// muerto para todos).
    /// </summary>
    private static readonly HashSet<TipoMigracion> Estructura = new()
    {
        TipoMigracion.Granjas,
        TipoMigracion.Nucleos,
        TipoMigracion.Galpones,
    };

    /// <summary>¿El tipo pertenece a la línea Pollo Engorde?</summary>
    public static bool EsLineaEngorde(TipoMigracion tipo) => LineaEngorde.Contains(tipo);

    /// <summary>¿El tipo es de estructura (Granjas/Núcleos/Galpones), retirado de la UI?</summary>
    public static bool EsEstructura(TipoMigracion tipo) => Estructura.Contains(tipo);

    /// <summary>
    /// Permiso que exige ese tipo, o <c>null</c> cuando alcanza con cualquiera de los dos
    /// (tipos de estructura, y las operaciones que no traen tipo, como el historial).
    /// </summary>
    public static string? PermisoRequerido(TipoMigracion tipo)
    {
        if (EsEstructura(tipo)) return null;
        return EsLineaEngorde(tipo) ? PermisoPolloEngorde : PermisoPostura;
    }

    /// <summary>
    /// ¿Esta sesión puede operar el módulo con ese tipo?
    ///
    /// <para>
    /// Sin tipo (<c>null</c>) se pide <b>cualquiera</b> de los dos permisos: es el caso del historial
    /// y de las consultas auxiliares, donde no hay línea que distinguir pero tampoco corresponde
    /// abrirlo a cualquier sesión.
    /// </para>
    ///
    /// <para>
    /// Fail-closed: sin permisos (lista vacía, sesión sin cargar) no pasa nadie.
    /// </para>
    /// </summary>
    public static bool PuedeUsar(IEnumerable<string>? permisos, TipoMigracion? tipo)
    {
        if (permisos is null) return false;
        var claves = permisos as IReadOnlyCollection<string> ?? permisos.ToArray();
        if (claves.Count == 0) return false;

        // Comparación ORDINAL explícita, igual que GestionUsuariosAutorizacionCalculos: una key de
        // permiso es un identificador, no texto de usuario. Que "CARGA_MASIVA_POSTURA" no abra la
        // puerta es parte del contrato, y su test lo fija.
        bool Tiene(string key) => claves.Contains(key, StringComparer.Ordinal);

        var requerido = tipo is TipoMigracion t ? PermisoRequerido(t) : null;
        return requerido is null
            ? Tiene(PermisoPostura) || Tiene(PermisoPolloEngorde)
            : Tiene(requerido);
    }

    /// <summary>Mensaje del 403, nombrando el permiso que falta para que el admin sepa qué asignar.</summary>
    public static string MensajeSinPermiso(TipoMigracion? tipo)
    {
        var requerido = tipo is TipoMigracion t ? PermisoRequerido(t) : null;
        return requerido is null
            ? $"No tenés permisos de carga masiva. Pedí a un administrador que te asigne '{PermisoPostura}' o '{PermisoPolloEngorde}'."
            : $"No tenés permiso para esta carga masiva. Pedí a un administrador que te asigne '{requerido}'.";
    }
}
