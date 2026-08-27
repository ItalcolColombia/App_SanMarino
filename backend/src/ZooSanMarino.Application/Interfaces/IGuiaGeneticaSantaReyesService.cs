// src/ZooSanMarino.Application/Interfaces/IGuiaGeneticaSantaReyesService.cs
using ZooSanMarino.Application.DTOs;
using PagedResultCommon = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.GuiaGeneticaSantaReyesDto>;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Puerta de ESCRITURA de la guía genética reducida (<c>guia_genetica_santa_reyes</c>).
///
/// <para>
/// <b>Por qué existe.</b> La tabla nació <i>seed-only</i>: 615 filas sembradas por la migración
/// <c>20260820093323</c> y <b>cero endpoints de escritura</b> — <c>GuiaGeneticaController</c> es
/// 100 % <c>[HttpGet]</c> y las 20 referencias C# a la entidad, fuera de migraciones, son todas
/// <c>.AsNoTracking()</c>. El cliente entró a producción y no encontró dónde cargar su línea genética.
/// </para>
///
/// <para>
/// Todo está scopeado por la <b>empresa efectiva</b> (empresa activa validada por
/// <c>ActiveCompanyMiddleware</c>, con el <c>CompanyId</c> del token como respaldo), nunca por un
/// header crudo.
/// </para>
/// </summary>
public interface IGuiaGeneticaSantaReyesService
{
    /// <summary>Listado paginado y filtrado de la guía de la empresa activa.</summary>
    Task<PagedResultCommon> SearchAsync(
        GuiaGeneticaSantaReyesSearchRequest request, CancellationToken ct = default);

    /// <summary>Una línea por id, o <c>null</c> si no existe o es de otra empresa.</summary>
    Task<GuiaGeneticaSantaReyesDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Alta de una línea. El código se calcula solo; si ya existe uno igual (vivo) para la empresa,
    /// lanza <see cref="InvalidOperationException"/> en vez de chocar contra el UNIQUE.
    /// </summary>
    Task<GuiaGeneticaSantaReyesDto> CreateAsync(
        CreateGuiaGeneticaSantaReyesDto dto, CancellationToken ct = default);

    /// <summary>
    /// Edición. Recalcula el código si cambia raza/año/edad.
    /// </summary>
    /// <exception cref="KeyNotFoundException">La línea no existe para la empresa activa.</exception>
    Task<GuiaGeneticaSantaReyesDto> UpdateAsync(
        UpdateGuiaGeneticaSantaReyesDto dto, CancellationToken ct = default);

    /// <summary>
    /// 🔴 <b>Baja SUAVE</b> (<c>DeletedAt</c>), nunca <c>Remove()</c>. El UNIQUE está filtrado por
    /// <c>deleted_at IS NULL</c> justamente para que dar de baja una línea no impida recrear el
    /// mismo código. (El módulo compartido sí borra en duro —
    /// <c>ProduccionAvicolaRawService.cs:195</c>— y eso no se replica acá.)
    /// </summary>
    /// <returns><c>false</c> si la línea no existe o ya estaba dada de baja.</returns>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Import Excel <b>idempotente</b> por <c>codigo = Raza+AnioGuia+Edad</c>: reimportar el mismo
    /// archivo actualiza, no duplica.
    /// </summary>
    /// <param name="contenido">Contenido del .xlsx.</param>
    /// <param name="nombreArchivo">Nombre original, sólo para validar la extensión.</param>
    /// <param name="tamanoBytes">Tamaño, para el tope de 10 MB.</param>
    Task<GuiaGeneticaSantaReyesImportResultDto> ImportarExcelAsync(
        Stream contenido, string nombreArchivo, long tamanoBytes, CancellationToken ct = default);

    /// <summary>
    /// Plantilla .xlsx con los encabezados que el import espera y una fila de ejemplo.
    /// </summary>
    byte[] GenerarPlantillaExcel();
}
