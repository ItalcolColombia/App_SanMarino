// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionMaterializadorDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Qué pasaría —o qué pasó— con el cronograma de un lote al aplicarle el plan de su empresa.
///
/// <para>
/// El mismo record sirve de <b>vista previa</b> y de <b>informe</b>: los dos salen del mismo cálculo
/// puro, así que lo que el usuario ve antes de confirmar es literalmente lo que se va a escribir.
/// </para>
/// </summary>
/// <param name="Motivo">Por qué le toca esa plantilla, o por qué ninguna. En castellano.</param>
/// <param name="Aplicado"><c>false</c> en la vista previa; <c>true</c> cuando ya se escribió.</param>
/// <param name="Error">
/// Sólo en el masivo: por qué este lote quedó afuera. Los demás se aplican igual — un lote que falla
/// no puede dejar a los otros a medio materializar.
/// </param>
public record VacunacionMaterializacionLoteDto(
    string LineaProductiva,
    int LoteId,
    string? LoteNombre,
    int GranjaId,
    string? GalponId,
    int? PlantillaId,
    string? PlantillaNombre,
    string Motivo,
    VacunacionMaterializacionConteosDto Conteos,
    List<VacunacionMaterializacionDetalleDto> Detalle,
    bool Aplicado = false,
    string? Error = null
);

/// <summary>
/// El impacto en números. <c>YaAplicados</c>, <c>Manuales</c> y <c>SinCambios</c> van separados a
/// propósito: «12 preservados» no le dice nada a quien tiene que decidir, «10 ya estaban bien y 2 ya
/// se aplicaron» sí.
/// </summary>
public record VacunacionMaterializacionConteosDto(
    int Faltantes,
    int Actualizables,
    int YaAplicados,
    int Manuales,
    int SinCambios,
    int Sobrantes
)
{
    /// <summary>Aplicarlo escribiría algo. Si es <c>false</c>, el botón no tiene nada que hacer.</summary>
    public bool EscribeAlgo => Faltantes > 0 || Actualizables > 0;
}

/// <summary>Una línea del detalle: qué vacuna, cuándo, y qué se va a hacer con ella.</summary>
/// <param name="Accion">
/// <c>Crear</c> | <c>Actualizar</c> | <c>YaAplicado</c> | <c>Manual</c> | <c>SinCambios</c> | <c>Sobrante</c>.
/// </param>
/// <param name="Detalle">Qué cambia, o por qué no se toca. Vacío cuando no hay nada que explicar.</param>
public record VacunacionMaterializacionDetalleDto(
    string Accion,
    int? CronogramaItemId,
    int? PlantillaItemId,
    int ItemInventarioId,
    string VacunaNombre,
    string UnidadObjetivo,
    int? ValorObjetivo,
    string? Detalle
);

/// <summary>
/// Impacto de una plantilla sobre todos los lotes vivos que hoy resuelven a ella.
/// </summary>
/// <param name="LotesEvaluados">Lotes vivos de la línea de la plantilla que se miraron.</param>
/// <param name="LotesAlcanzados">De ésos, a cuántos les toca <b>esta</b> plantilla.</param>
/// <param name="LotesQueEscriben">De ésos, en cuántos hay algo para escribir.</param>
public record VacunacionMaterializacionMasivaDto(
    int PlantillaId,
    string PlantillaNombre,
    string LineaProductiva,
    int LotesEvaluados,
    int LotesAlcanzados,
    int LotesQueEscriben,
    VacunacionMaterializacionConteosDto Conteos,
    List<VacunacionMaterializacionLoteDto> Lotes,
    int LotesConError = 0
);
