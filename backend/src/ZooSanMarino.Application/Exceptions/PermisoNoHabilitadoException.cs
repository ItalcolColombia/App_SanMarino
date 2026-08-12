namespace ZooSanMarino.Application.Exceptions;

/// <summary>
/// Se intentó asignar a un rol un permiso que su empresa no habilita (<c>company_permissions</c>).
///
/// <para>
/// Hereda de <see cref="InvalidOperationException"/> a propósito: cualquier <c>catch</c> existente de
/// ese tipo la sigue atrapando, así que introducirla no cambia el comportamiento de nada. Lo único que
/// agrega es que el manejador global de excepciones la traduce a <b>400</b> en vez de 500 — es un
/// error del request, no del servidor, y el cliente puede distinguirlo.
/// </para>
/// </summary>
public class PermisoNoHabilitadoException : InvalidOperationException
{
    public PermisoNoHabilitadoException(string message) : base(message) { }
}
