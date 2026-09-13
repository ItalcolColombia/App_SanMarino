namespace ZooSanMarino.Application.Exceptions;

/// <summary>
/// Se intentó prender en una empresa un permiso que pertenece a módulos que esa empresa no tiene
/// (ajuste fino fuera de módulo, regla R-M4 de <c>PermisoModuloCalculos</c>).
///
/// <para>
/// Mismo diseño que <see cref="PermisoNoHabilitadoException"/>: hereda de
/// <see cref="InvalidOperationException"/> para no cambiar ningún <c>catch</c> existente, y el manejador
/// global la traduce a <b>400</b> con el mensaje tal cual.
/// </para>
/// </summary>
public class PermisoFueraDeModuloException : InvalidOperationException
{
    public PermisoFueraDeModuloException(string message) : base(message) { }
}
