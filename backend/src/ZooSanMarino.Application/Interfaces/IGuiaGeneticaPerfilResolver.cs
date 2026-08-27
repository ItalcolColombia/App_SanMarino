// src/ZooSanMarino.Application/Interfaces/IGuiaGeneticaPerfilResolver.cs
namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Resuelve qué MODELO de guía genética usa la empresa activa
/// (<c>companies.guia_genetica_perfil</c>: <c>'sanmarino'</c> | <c>'reducida'</c>).
///
/// <para>
/// Existe como servicio propio —y no como un método de cada service— porque el guard fail-closed lo
/// necesitan <b>tres controllers distintos</b> que no comparten dependencias:
/// <c>GuiaGeneticaSantaReyesController</c>, <c>ProduccionAvicolaRawController</c> y
/// <c>ExcelImportController</c>.
/// </para>
///
/// <para>
/// La empresa se resuelve por DATOS —empresa activa validada por <c>ActiveCompanyMiddleware</c>, con
/// el <c>CompanyId</c> del token como respaldo—, <b>nunca</b> leyendo un header crudo.
/// </para>
/// </summary>
public interface IGuiaGeneticaPerfilResolver
{
    /// <summary>
    /// Perfil de la empresa activa, ya normalizado.
    /// </summary>
    /// <remarks>
    /// Empresa inexistente o columna vacía ⇒ <c>'sanmarino'</c>, el default neutro: es el
    /// comportamiento de siempre y mantiene el delta cero de la tabla compartida. Un valor
    /// desconocido en la columna <b>lanza</b> (<see cref="ArgumentOutOfRangeException"/>), por
    /// decisión explícita de <c>GuiaGeneticaPerfilCalculos</c>: caer al default en silencio dejaría
    /// escribir en la tabla equivocada sin un solo síntoma.
    /// </remarks>
    Task<string> PerfilEmpresaActivaAsync(CancellationToken ct = default);
}
