// src/ZooSanMarino.Application/Calculos/GuiaGeneticaEscrituraAutorizacionCalculos.cs
// Quién puede ESCRIBIR una guía genética y EN CUÁL de las dos tablas. Sin EF, sin HttpContext.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Regla PURA de escritura de las guías genéticas (plan
/// <c>fase_de_desarrollo/guia_genetica_tres_modulos_plan.md</c> §4, F2.3).
///
/// <para>
/// <b>Lo que había antes: nada.</b> Ninguno de los tres controllers de guía tenía un solo chequeo
/// de permiso. <c>ProduccionAvicolaRawController</c> y <c>ExcelImportController</c> no llevan
/// siquiera un <c>[Authorize]</c> de clase: los salva únicamente la <c>FallbackPolicy</c> global.
/// Cualquier sesión válida podía reescribir —o borrar en duro, porque
/// <c>ProduccionAvicolaRawService.DeleteAsync</c> hace <c>Remove()</c>— la guía genética de su
/// empresa, que es el insumo de todos los indicadores técnicos.
/// </para>
///
/// <para>
/// Son <b>dos gates independientes</b> y hay que pasar los dos para escribir:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>Quién</b> — <see cref="PermisoGestionar"/>. Las LECTURAS quedan abiertas a propósito: sin
///     el permiso se consulta la guía y se exporta, pero no se crea, edita, importa ni da de baja.
///   </description></item>
///   <item><description>
///     <b>Dónde</b> — <see cref="PuedeEscribirEnPerfil"/>. Una empresa escribe en la tabla de SU
///     perfil (<see cref="GuiaGeneticaPerfilCalculos"/>) y en ninguna otra. Fail-closed en los dos
///     sentidos: jamás se cae al otro perfil.
///   </description></item>
/// </list>
/// </summary>
public static class GuiaGeneticaEscrituraAutorizacionCalculos
{
    /// <summary>
    /// Permiso que habilita crear, editar, importar y dar de baja líneas de guía genética.
    /// Convención <c>modulo.accion</c>, la misma de <c>usuarios.gestionar</c> y
    /// <c>lote.corregir_aves</c>.
    ///
    /// <para>
    /// 🔴 <b>Anti-lockout — lo tiene que sembrar la migración de F4.</b> Este permiso <b>invierte el
    /// default</b>: hoy escribe cualquiera, mañana sólo quien lo tenga. Sembrarlo sólo para
    /// <c>role_id = 1</c> dejaría a los administradores de cada país sin poder cargar su guía el día
    /// del deploy. Se hereda de <c>role_menus</c> localizando el menú <b>por <c>route</c></b> (nunca
    /// por id: los ids difieren local ↔ prod), igual que el patrón de
    /// <c>SeedPermisoLoteCorregirAves</c>: gana el permiso todo rol que hoy ya ve alguna pantalla de
    /// guía genética. Nadie gana ni pierde acceso.
    /// </para>
    ///
    /// <para>
    /// ⚠️ Los permisos viajan dentro de la sesión cifrada, no se consultan por acción: después de
    /// sembrarlo hay que <b>cerrar sesión y volver a entrar</b> para verlo.
    /// </para>
    /// </summary>
    public const string PermisoGestionar = "guia_genetica.gestionar";

    /// <summary>
    /// Mensaje del rechazo por permiso. Dice qué SÍ se puede hacer, para que quien lo lea no crea
    /// que perdió el acceso al módulo entero.
    /// </summary>
    public const string MensajeSinPermiso =
        "No tiene permiso para administrar la guía genética. Puede consultarla y exportarla, " +
        "pero no crear, editar, importar ni dar de baja líneas.";

    /// <summary>
    /// ¿Este usuario puede ESCRIBIR guía genética? <b>Fail-closed</b>: lista nula ⇒ no.
    /// Comparación ordinal, igual que el resto de los gates por permiso del repo.
    /// </summary>
    public static bool PuedeGestionar(IEnumerable<string>? permisos) =>
        permisos is not null && permisos.Contains(PermisoGestionar, StringComparer.Ordinal);

    /// <summary>
    /// ¿Es una operación de solo LECTURA? Se expone para que el criterio de qué queda abierto viva
    /// en un solo lugar y no se disperse en cada <c>if</c> del controller.
    /// </summary>
    public static bool EsLectura(string? metodoHttp) =>
        string.Equals((metodoHttp ?? string.Empty).Trim(), "GET", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ¿Una empresa con este perfil puede escribir en la tabla que exige
    /// <paramref name="perfilDeLaTabla"/>?
    ///
    /// <para>
    /// Comparación exacta contra el perfil ya resuelto por
    /// <see cref="GuiaGeneticaPerfilCalculos.Resolver"/> — que <b>lanza</b> ante un valor
    /// desconocido, a propósito: dejar escribir en la tabla equivocada en silencio es peor que fallar.
    /// </para>
    /// </summary>
    public static bool PuedeEscribirEnPerfil(string? perfilDeLaEmpresa, string perfilDeLaTabla) =>
        string.Equals(
            GuiaGeneticaPerfilCalculos.Resolver(perfilDeLaEmpresa),
            GuiaGeneticaPerfilCalculos.Resolver(perfilDeLaTabla),
            StringComparison.Ordinal);

    /// <summary>
    /// Por qué se rechaza la escritura, nombrando el módulo al que el usuario tiene que ir. Un
    /// «403» pelado en esta pantalla es indistinguible de «me falta un permiso», y manda a pedirle
    /// al administrador algo que no le va a servir.
    /// </summary>
    /// <param name="perfilDeLaTabla">Perfil que exige la tabla a la que se intentó escribir.</param>
    public static string MensajePerfilIncorrecto(string perfilDeLaTabla) =>
        GuiaGeneticaPerfilCalculos.Resolver(perfilDeLaTabla) == GuiaGeneticaPerfilCalculos.Reducida
            ? "Esta empresa administra su guía genética en el módulo «Guía Genética Sanmarino». " +
              "Este módulo es para las empresas que usan la guía reducida (producción, retiro " +
              "acumulado y consumo por semana)."
            : "Esta empresa administra su guía genética en el módulo «Guía Genética Santa Reyes». " +
              "La guía compartida no acepta escrituras de esta empresa.";
}
