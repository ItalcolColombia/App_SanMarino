namespace ZooSanMarino.Application.DTOs.Dashboard;

/// <summary>
/// Resumen del dashboard: los conteos que se pueden afirmar sin ambigüedad, recortados por empresa
/// activa y por el alcance de ubicación del usuario.
///
/// <para><b>Qué NO trae y por qué.</b> No trae «aves vivas» ni «eficiencia». En este sistema el saldo
/// de aves tiene dueño —las <c>fn_*</c> de cada línea productiva— y CLAUDE.md lo dice explícito:
/// una sola fórmula por número. Sumar acá <c>hembras_l + machos_l</c> sería una tercera
/// implementación, y además equivocada: esa misma columna es el <b>saldo vivo</b> en
/// <c>lote_ave_engorde</c> y la <b>base de encasetamiento</b> en <c>lotes</c>. Los indicadores de
/// aves llegan con el panel de su línea, calculados por la función que ya es su dueña.</para>
/// </summary>
/// <param name="Granjas">
/// Granjas de la empresa activa asignadas al usuario (<c>user_farms</c>), sin borrar.
/// </param>
/// <param name="LotesPosturaActivos">Lotes de postura cuyo levante NO está cerrado.</param>
/// <param name="LotesPosturaTotal">Todos los lotes de postura del alcance (activos y cerrados).</param>
/// <param name="LotesEngordeActivos">
/// Lotes de pollo engorde con <c>estado_operativo_lote</c> distinto de «Cerrado».
/// </param>
/// <param name="LotesEngordeTotal">Todos los lotes de engorde del alcance.</param>
/// <param name="AlcanceRestringido">
/// <c>true</c> si al usuario le aplica al menos una granja con alcance granular. Se expone para que
/// la pantalla pueda decir «estás viendo una parte» en vez de dejar creer que ese es el total de la
/// empresa — un conteo recortado sin avisar se lee como un dato faltante.
/// </param>
/// <param name="GeneradoAt">Momento en que se calculó (UTC).</param>
public sealed record DashboardResumenDto(
    int Granjas,
    int LotesPosturaActivos,
    int LotesPosturaTotal,
    int LotesEngordeActivos,
    int LotesEngordeTotal,
    bool AlcanceRestringido,
    DateTime GeneradoAt
)
{
    /// <summary>Resumen de un usuario sin granjas visibles. Fail-closed: ceros, no «toda la empresa».</summary>
    public static DashboardResumenDto Vacio(bool alcanceRestringido) =>
        new(0, 0, 0, 0, 0, alcanceRestringido, DateTime.UtcNow);
}
