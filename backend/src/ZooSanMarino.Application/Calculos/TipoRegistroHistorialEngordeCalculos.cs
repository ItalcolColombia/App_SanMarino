// src/ZooSanMarino.Application/Calculos/TipoRegistroHistorialEngordeCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Catálogo canónico de <c>historial_lote_pollo_engorde.tipo_registro</c> y los predicados de negocio
/// que dependen de él. Lógica PURA: sin EF, sin estado.
///
/// <para><b>Por qué existe (incidente 2026-08-28).</b> La corrección del encasetamiento de un lote de
/// engorde (commit <c>a9fd721</c>, 21-ago-2026) audita cada ajuste con
/// <c>tipo_registro = 'AjusteEncaset'</c>, pero la tabla en producción tenía
/// <c>ck_hlpe_tipo_registro CHECK (tipo_registro IN ('Inicio','Ajuste','AjusteResync'))</c>: la
/// funcionalidad se mergeó con el C# que escribe el valor y <b>sin la migración que lo permite</b>.
/// Resultado: TODA edición de aves de un lote de engorde moría con SQLSTATE 23514 y el usuario veía el
/// toast genérico «Alguno de los valores no cumple una regla de validación de la base de datos».
/// No se detectó en local porque la copia local <b>no tiene ni una sola constraint CHECK</b>.</para>
///
/// <para><b>Para qué sirve tenerlo acá.</b> El catálogo es la contraparte ejecutable del CHECK que
/// vive en la BD: los tests lo congelan contra la lista que dejó la migración
/// <c>20260828190000_AmpliaCheckHistorialEngordeAjusteEncaset</c>. Si alguien inventa un quinto
/// <c>tipo_registro</c> en C# sin escribir su migración, falla el gate de CI — no producción.
/// Es el mismo criterio de «una sola fórmula por número»: el CHECK es el dueño, esto es el test.</para>
/// </summary>
public static class TipoRegistroHistorialEngordeCalculos
{
    /// <summary>Aves con que ARRANCÓ el lote. Es la base del encasetamiento; la escribe el alta.</summary>
    public const string Inicio = "Inicio";

    /// <summary>
    /// Descuento por aves fantasma (nunca descargadas). <b>SÍ participa en la conservación</b>
    /// (esperado = iniciales − ventas − ajustes fantasma).
    /// </summary>
    public const string Ajuste = "Ajuste";

    /// <summary>
    /// Re-sync por ventas Completadas que no descontaron el maestro. <b>SUSTITUYE</b> el descuento de
    /// esas ventas, por lo que no vuelve a restarse en la conservación.
    /// </summary>
    public const string AjusteResync = "AjusteResync";

    /// <summary>
    /// Corrección del encasetamiento: guarda el <b>DELTA con signo</b> aplicado al lote. No participa
    /// en la conservación — el ajuste ya quedó dentro del registro <see cref="Inicio"/> corregido.
    /// </summary>
    public const string AjusteEncaset = "AjusteEncaset";

    /// <summary>
    /// Los cuatro valores admitidos, en el mismo orden en que los lista
    /// <c>ck_hlpe_tipo_registro</c>. Ver <see cref="TipoRegistroHistorialEngordeCalculos"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> Catalogo = new[]
    {
        Inicio, Ajuste, AjusteResync, AjusteEncaset
    };

    /// <summary>
    /// ¿El valor cabe en el CHECK de la tabla? Comparación <b>case-sensitive</b> y fail-closed: el
    /// CHECK de Postgres compara literales exactos, así que <c>"inicio"</c> es tan inválido como
    /// <c>"Cualquiera"</c>, y null/vacío también.
    /// </summary>
    public static bool EsValido(string? tipoRegistro) =>
        tipoRegistro is not null && Catalogo.Contains(tipoRegistro, StringComparer.Ordinal);

    /// <summary>
    /// ¿La fila se resta en la conservación de aves del lote (<c>esperado = Inicio − ventas − ajustes</c>)?
    /// Sólo <see cref="Ajuste"/>: <see cref="AjusteResync"/> sustituye un descuento que faltó y
    /// <see cref="AjusteEncaset"/> ya está dentro del <see cref="Inicio"/> corregido — restarlos los
    /// contaría dos veces.
    /// </summary>
    public static bool ParticipaEnConservacion(string? tipoRegistro) =>
        string.Equals(tipoRegistro, Ajuste, StringComparison.Ordinal);

    /// <summary>
    /// ¿La fila puede llevar aves negativas? Sólo <see cref="AjusteEncaset"/>, que guarda un delta con
    /// signo (bajar el encasetamiento de 10.500 a 10.000 audita −500). Los demás son CANTIDADES y su
    /// no-negatividad la sigue vigilando <c>ck_hlpe_aves_nonneg</c> en la BD; este predicado es su
    /// espejo en C#.
    /// </summary>
    public static bool AdmiteDeltaNegativo(string? tipoRegistro) =>
        string.Equals(tipoRegistro, AjusteEncaset, StringComparison.Ordinal);
}
