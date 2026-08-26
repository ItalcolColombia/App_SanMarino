// src/ZooSanMarino.Application/Calculos/GuiaGeneticaPerfilCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Qué MODELO de guía genética usa una empresa — la señal es <c>companies.guia_genetica_perfil</c>,
/// una columna tipada nombrada por COMPORTAMIENTO (CLAUDE.md §🏢 prohíbe
/// <c>if (empresa == 'Santa Reyes')</c> / <c>if (pais == X)</c>). La empresa #4 que mañana quiera el
/// modelo plano se da de alta cambiando <b>un dato</b>, no desplegando código.
///
/// <para>
/// Los dos perfiles existen porque son dos modelos de datos genuinamente distintos, y se dejan
/// separados a propósito (decisión del usuario, 26-ago-2026 — ver
/// <c>fase_de_desarrollo/guia_genetica_tres_modulos_plan.md</c>):
/// </para>
/// <list type="bullet">
///   <item><description>
///     <see cref="Sanmarino"/> — tabla ANCHA compartida <c>guia_genetica_sanmarino_colombia</c>
///     (~50 columnas, reproductora + postura). Es el default neutro: toda empresa que no declare
///     otra cosa sigue exactamente donde estaba.
///   </description></item>
///   <item><description>
///     <see cref="Reducida"/> — tabla PLANA <c>guia_genetica_santa_reyes</c> de 3 métricas
///     (<c>prod_porcentaje</c>, <c>retiro_ac_h</c>, <c>gr_ave_dia_h</c>) por raza/año/edad.
///   </description></item>
/// </list>
///
/// <para>
/// El perfil gobierna: (a) el guard fail-closed del controller de la tabla reducida, (b) el guard
/// fail-closed del controller de la tabla compartida, (c) qué ítem de menú se habilita y (d) qué
/// pantalla ofrece el front.
/// </para>
/// </summary>
public static class GuiaGeneticaPerfilCalculos
{
    /// <summary>
    /// Tabla ANCHA compartida (<c>guia_genetica_sanmarino_colombia</c> / <c>ProduccionAvicolaRaw</c>).
    /// <b>Default neutro</b>: es lo que hacen hoy Sanmarino, Demo, Ecuador y Panamá.
    /// </summary>
    public const string Sanmarino = "sanmarino";

    /// <summary>
    /// Tabla PLANA de 3 métricas (<c>guia_genetica_santa_reyes</c> / <c>GuiaGeneticaSantaReyes</c>).
    /// </summary>
    public const string Reducida = "reducida";

    /// <summary>
    /// Valor asumido cuando la empresa no declara perfil (columna <c>NULL</c>/vacía, empresa vieja,
    /// DTO sin el campo). Coincide con el <c>DEFAULT</c> de la columna en base.
    /// </summary>
    public const string Default = Sanmarino;

    /// <summary>Los únicos valores que la columna acepta, en orden de declaración.</summary>
    public static IReadOnlyList<string> Validos { get; } = new[] { Sanmarino, Reducida };

    /// <summary>
    /// ¿Es un perfil conocido? Versión que NO lanza, para validar un valor que viene de afuera
    /// (payload, query string) antes de rechazarlo con un 400 legible en vez de un 500.
    /// Ausente/vacío cuenta como conocido: resuelve al <see cref="Default"/>.
    /// </summary>
    public static bool EsPerfilConocido(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return true;
        var normalizado = Normalizar(valor);
        return normalizado == Sanmarino || normalizado == Reducida;
    }

    /// <summary>
    /// Resuelve el perfil de una empresa a partir del valor crudo de la columna.
    ///
    /// <para>
    /// <c>null</c> / vacío ⇒ <see cref="Default"/> (<c>"sanmarino"</c>), el comportamiento de
    /// siempre. Un valor conocido ⇒ ese mismo perfil, normalizado (se tolera espaciado y
    /// mayúsculas: la columna es un <c>varchar</c> libre y el dato puede llegar de un backfill).
    /// </para>
    ///
    /// <para>
    /// 🔴 Un valor DESCONOCIDO <b>lanza</b>, a propósito. Caer al default en silencio le mostraría
    /// al usuario la tabla equivocada —y, peor, lo dejaría escribir en ella— sin un solo síntoma
    /// visible. Hacer INALCANZABLE el estado malo es mejor que manejarlo (CLAUDE.md §🛡️).
    /// </para>
    /// </summary>
    /// <param name="valor">Valor crudo de <c>companies.guia_genetica_perfil</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException">El valor no es ninguno de <see cref="Validos"/>.</exception>
    public static string Resolver(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return Default;

        var normalizado = Normalizar(valor);
        if (normalizado == Sanmarino) return Sanmarino;
        if (normalizado == Reducida) return Reducida;

        throw new ArgumentOutOfRangeException(
            nameof(valor),
            valor,
            $"Perfil de guía genética desconocido: «{valor}». Válidos: '{Sanmarino}' (tabla ancha " +
            $"compartida) | '{Reducida}' (tabla plana de 3 métricas). No se cae al default a " +
            "propósito: mostrar —o dejar escribir— la tabla equivocada en silencio es peor que fallar.");
    }

    /// <summary>
    /// ¿La empresa usa la tabla PLANA de 3 métricas (<c>guia_genetica_santa_reyes</c>)?
    /// Lanza ante un perfil desconocido, igual que <see cref="Resolver"/>.
    /// </summary>
    public static bool UsaGuiaReducida(string? valor) => Resolver(valor) == Reducida;

    /// <summary>
    /// ¿La empresa usa la tabla ANCHA compartida (<c>guia_genetica_sanmarino_colombia</c>)?
    /// Es el caso por defecto. Lanza ante un perfil desconocido, igual que <see cref="Resolver"/>.
    /// </summary>
    public static bool UsaGuiaCompartida(string? valor) => Resolver(valor) == Sanmarino;

    private static string Normalizar(string valor) => valor.Trim().ToLowerInvariant();
}
