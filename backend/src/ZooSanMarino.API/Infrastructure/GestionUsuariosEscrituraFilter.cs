// src/ZooSanMarino.API/Infrastructure/GestionUsuariosEscrituraFilter.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Exige el permiso <c>usuarios.gestionar</c> para toda operación de ESCRITURA de los controllers
/// de Gestión de Usuarios. Las lecturas (<c>GET</c>) pasan sin permiso, que es lo pedido: el listado
/// y el detalle quedan abiertos a cualquier sesión.
///
/// <para>
/// <b>Por qué un filtro de clase y no un <c>if</c> por acción</b> (que es el patrón de los otros 11
/// controllers del repo): acá son <b>32 endpoints</b> entre <c>UsersController</c> (15) y
/// <c>UserFarmController</c> (17), y de ellos ~20 escriben. Repetir la guarda veinte veces convierte
/// «se olvidaron de una» en cuestión de tiempo — y el endpoint olvidado no falla: <b>deja pasar</b>.
/// Con el filtro en la clase, un endpoint nuevo nace cubierto y hay que sacarlo explícitamente para
/// abrirlo. Es el mismo criterio por el que <c>VentanaFechaRegistroGuard</c> vive en esta carpeta:
/// una regla transversal de la capa API, no de un service.
/// </para>
///
/// <para>
/// ⛔ <b>Por qué no se endurece la policy <c>CanManageUsers</c>.</b> Esa policy la usan
/// <c>RoleController.GetMenusForUser</c> y <c>MenuController.GetForUser</c>, ajenos a este módulo:
/// exigirle esta key rompería la pantalla de Roles.
/// </para>
///
/// <para>
/// El permiso sale del claim <c>permission</c> del JWT — la misma fuente que usa
/// <c>HttpCurrentUser</c>—, así que ningún controller tiene que cambiar su constructor ni su DI para
/// adoptarlo.
/// </para>
///
/// <para>
/// Responde <b>403</b> (no 401): la sesión es válida, lo que falta es la autorización.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class GestionUsuariosEscrituraFilterAttribute : ActionFilterAttribute
{
    /// <inheritdoc />
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var metodo = context.HttpContext.Request.Method;

        // Las LECTURAS quedan abiertas a propósito: ver el listado y el detalle es justamente lo que
        // conserva quien no tiene el permiso.
        if (GestionUsuariosAutorizacionCalculos.EsLectura(metodo))
        {
            base.OnActionExecuting(context);
            return;
        }

        // Un endpoint marcado [AllowAnonymous] no pasa por acá con una identidad util; si alguna vez
        // se agrega uno, este filtro seguiria rechazandolo, que es el lado seguro del error.
        var permisos = context.HttpContext.User
            .FindAll("permission")
            .Select(c => c.Value);

        if (!GestionUsuariosAutorizacionCalculos.PuedeGestionar(permisos))
        {
            var mensaje = GestionUsuariosAutorizacionCalculos.MensajeSinPermiso;
            context.Result = new ObjectResult(new { message = mensaje, error = mensaje })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        base.OnActionExecuting(context);
    }
}
