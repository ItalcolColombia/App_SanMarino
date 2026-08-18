// src/ZooSanMarino.Application/Calculos/EmpresaActivaCalculos.cs
// Reglas PURAS de la empresa activa: quién puede usarla y qué nombre es confiable.
// Sin EF, sin HttpContext, sin estado.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// La <b>empresa activa</b> es la que el cliente pide por cabecera (<c>X-Active-Company</c> /
/// <c>X-Active-Company-Id</c>) y determina el alcance de casi todas las consultas del backend.
///
/// <para>
/// <b>Por qué existe esta clase (18-ago-2026).</b> El middleware validaba la pertenencia y publicaba
/// el resultado, pero <c>ICurrentUser.ActiveCompanyName</c> devolvía <b>el header crudo</b>. Y 44
/// servicios resuelven su empresa así:
/// <code>
/// if (!string.IsNullOrWhiteSpace(_current.ActiveCompanyName)) {
///     var byName = await _companyResolver.GetCompanyIdByNameAsync(_current.ActiveCompanyName);
///     if (byName.HasValue) return byName.Value;   // ← la que eligió el cliente
/// }
/// return _current.CompanyId;                       // ← la validada, que casi nunca se alcanzaba
/// </code>
/// Resultado medido: un usuario que pertenece <b>sólo</b> a Sanmarino (61 ítems de inventario),
/// mandando <c>X-Active-Company: ItalcolEcuador</c>, recibía los <b>152</b> de Ecuador.
/// </para>
///
/// <para>
/// La regla, ahora en un solo lugar: <b>el nombre de empresa en el que se confía es el que validó el
/// middleware, nunca el que llegó por la cabecera.</b> Si no hubo validación, no hay nombre, y el
/// llamador cae a la empresa del token.
/// </para>
/// </summary>
public static class EmpresaActivaCalculos
{
    /// <summary>
    /// ¿Puede este usuario operar sobre la empresa que pidió? Sí cuando es <b>super admin</b> —que
    /// atraviesa el aislamiento multiempresa a propósito— o cuando <b>pertenece</b> a ella.
    /// <b>Fail-closed</b>: cualquier otra combinación, no.
    /// </summary>
    public static bool PuedeUsarEmpresa(bool esSuperAdmin, bool perteneceALaEmpresa) =>
        esSuperAdmin || perteneceALaEmpresa;

    /// <summary>
    /// Nombre de empresa activa en el que se puede confiar para resolver alcance.
    ///
    /// <para>
    /// Recibe <b>únicamente</b> lo que el middleware dejó tras validar. Devuelve <c>null</c> si no hay
    /// nada o si viene en blanco — nunca cae al header. Que la firma no admita el header es
    /// deliberado: hace imposible volver a confundir «lo que pidió el cliente» con «lo que se aprobó».
    /// </para>
    /// </summary>
    public static string? NombreConfiable(string? nombreValidadoPorElMiddleware)
    {
        var nombre = nombreValidadoPorElMiddleware?.Trim();
        return string.IsNullOrEmpty(nombre) ? null : nombre;
    }

    /// <summary>
    /// Id de empresa a usar cuando un servicio resuelve por nombre.
    ///
    /// <para>
    /// Si el nombre pedido es exactamente el de la empresa activa ya validada, se responde con
    /// <b>su</b> id, sin volver a buscarlo: el middleware ya resolvió ese nombre contra la base y su
    /// id es el que se autorizó. Evita depender de una segunda búsqueda por nombre que no es
    /// determinista —<c>companies.name</c> no tiene índice único y el resolver hace
    /// <c>FirstOrDefault</c> sin orden—, y ahorra una consulta por llamada.
    /// </para>
    /// <para>Para cualquier otro nombre devuelve <c>null</c>: que lo resuelva quien preguntó.</para>
    /// </summary>
    public static int? IdDeLaEmpresaActivaSiCoincide(
        string? nombrePedido,
        string? nombreActivoValidado,
        int idActivoValidado)
    {
        if (idActivoValidado <= 0) return null;

        var pedido = nombrePedido?.Trim();
        var activo = nombreActivoValidado?.Trim();
        if (string.IsNullOrEmpty(pedido) || string.IsNullOrEmpty(activo)) return null;

        return string.Equals(pedido, activo, StringComparison.OrdinalIgnoreCase)
            ? idActivoValidado
            : null;
    }
}
