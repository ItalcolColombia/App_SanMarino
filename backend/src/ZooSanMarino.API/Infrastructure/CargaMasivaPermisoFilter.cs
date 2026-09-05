// src/ZooSanMarino.API/Infrastructure/CargaMasivaPermisoFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Marca un endpoint del módulo de Migraciones Masivas como abierto a cualquier sesión autenticada,
/// exceptuándolo de <see cref="CargaMasivaPermisoFilterAttribute"/>.
///
/// <para>
/// Su único uso legítimo hoy es <c>GET /api/Migracion/tipos</c>: el catálogo estático de tipos. La
/// pantalla lo pide ANTES de saber qué puede hacer el usuario y recién después filtra los tiles con
/// los permisos de la sesión (<c>filtrar-tipos-visibles.funcion.ts</c>, fail-closed). Si se cerrara,
/// quien no tiene permisos vería un error de red en vez del mensaje que le explica que le falta el
/// permiso — el mismo error que ya se cometió cerrando <c>GET /api/Company/global</c>.
/// </para>
///
/// <para>
/// No expone dato alguno de ninguna empresa: son constantes de código
/// (<c>TipoMigracionCatalogo.Todos</c>).
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class CargaMasivaPermisoNoRequeridoAttribute : Attribute;

/// <summary>
/// Exige el permiso de carga masiva que corresponde al TIPO de migración de la request
/// (<c>carga_masiva_postura</c> / <c>carga_masiva_pollo_engorde</c>).
///
/// <para>
/// <b>Por qué un filtro de clase y no un <c>if</c> por acción</b>: mismo criterio que
/// <see cref="GestionUsuariosEscrituraFilterAttribute"/> — con el filtro en la clase, un endpoint
/// nuevo nace cubierto y hay que sacarlo EXPLÍCITAMENTE (con
/// <see cref="CargaMasivaPermisoNoRequeridoAttribute"/>) para abrirlo. Repetir la guarda por acción
/// convierte «se olvidaron de una» en cuestión de tiempo, y el endpoint olvidado no falla:
/// <b>deja pasar</b>.
/// </para>
///
/// <para>
/// <b>Por qué no alcanza un <c>[Authorize(Policy = …)]</c></b>: el tipo no viaja en la ruta sino como
/// parámetro —query string en las lecturas, campo del formulario multipart en <c>/validar</c> e
/// <c>/importar</c>—, así que el permiso exigido se conoce recién con la request armada.
/// </para>
///
/// <para>
/// Un tipo ilegible (ausente o inválido) NO abre la puerta: se trata como «sin tipo» y se exige
/// cualquiera de los dos permisos. El tipo inválido lo rechaza después el propio controller con un
/// 400, que es el mensaje correcto para ese caso.
/// </para>
///
/// <para>Responde <b>403</b> (no 401): la sesión es válida, lo que falta es la autorización.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class CargaMasivaPermisoFilterAttribute : ActionFilterAttribute
{
    /// <summary>Nombres de parámetro por los que puede llegar el tipo de migración.</summary>
    private static readonly string[] ClavesDeTipo = { "tipo", "Tipo" };

    /// <inheritdoc />
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<CargaMasivaPermisoNoRequeridoAttribute>().Any())
        {
            base.OnActionExecuting(context);
            return;
        }

        var permisos = context.HttpContext.User.FindAll("permission").Select(c => c.Value).ToArray();
        var tipo = ResolverTipo(context);

        if (!MigracionAutorizacionCalculos.PuedeUsar(permisos, tipo))
        {
            var mensaje = MigracionAutorizacionCalculos.MensajeSinPermiso(tipo);
            context.Result = new ObjectResult(new { message = mensaje, error = mensaje })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        base.OnActionExecuting(context);
    }

    /// <summary>
    /// El tipo de la request, mirando en este orden: los argumentos ya bindeados de la acción (cubre
    /// el <c>[FromQuery] string tipo</c> y el <c>[FromForm]</c> del formulario), y si no, la query
    /// string cruda. Devuelve <c>null</c> cuando no hay tipo o no se puede interpretar.
    /// </summary>
    private static TipoMigracion? ResolverTipo(ActionExecutingContext context)
    {
        foreach (var clave in ClavesDeTipo)
            if (context.ActionArguments.TryGetValue(clave, out var valor) && Interpretar(TextoDe(valor)) is TipoMigracion t)
                return t;

        // El formulario de /validar e /importar llega como un objeto (MigracionUploadForm): su
        // propiedad Tipo es la que manda.
        foreach (var valor in context.ActionArguments.Values)
        {
            if (valor is null || valor is string) continue;
            var prop = valor.GetType().GetProperty("Tipo");
            if (prop?.GetValue(valor) is string texto && Interpretar(texto) is TipoMigracion t) return t;
        }

        foreach (var clave in ClavesDeTipo)
            if (context.HttpContext.Request.Query.TryGetValue(clave, out var q) && Interpretar(q.ToString()) is TipoMigracion t)
                return t;

        return null;
    }

    private static string? TextoDe(object? valor) => valor as string;

    private static TipoMigracion? Interpretar(string? texto)
        => !string.IsNullOrWhiteSpace(texto)
           && Enum.TryParse<TipoMigracion>(texto, ignoreCase: true, out var parsed)
           && Enum.IsDefined(typeof(TipoMigracion), parsed)
            ? parsed
            : null;
}
