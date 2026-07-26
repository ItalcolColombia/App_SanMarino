// src/ZooSanMarino.Application/DTOs/Produccion/ClasificacionHuevoItemSemanaDto.cs
namespace ZooSanMarino.Application.DTOs.Produccion;

/// <summary>
/// Una fila del desglose de clasificación de huevos POR ÍTEM del catálogo y SEMANA de vida
/// (empresas con <c>companies.clasificacion_huevo_por_items = true</c>, ej. Santa Reyes: las 11
/// columnas fijas quedan en 0 y el desglose real vive en
/// <c>seguimiento_diario_produccion.metadata → huevoItems</c>).
/// <para>
/// Proyección 1:1 de <c>fn_clasificacion_huevo_items_produccion(...)</c>: los nombres de las
/// propiedades corresponden a las columnas snake_case de la función y los mapea EF por
/// <c>SqlQueryRaw</c> (mismo patrón que <see cref="IndicadorProduccionSemanalBdRow"/>). Se expone
/// tal cual en la respuesta de <c>POST /api/Produccion/clasificacion-huevo-items</c>
/// (JSON camelCase: <c>{ semana, tipoHuevo, codigo, nombre, cantidad }</c>).
/// </para>
/// La <see cref="Semana"/> se calcula con la MISMA fórmula que los indicadores semanales
/// (semana de vida desde el encaset, calendario America/Bogota) → casa 1:1 con esa grilla.
/// Hay una fila por semana × ítem; el front agrupa por tipo (Primera/Pnc).
/// </summary>
public sealed class ClasificacionHuevoItemSemanaDto
{
    /// <summary>Semana de vida del lote (misma base que los indicadores semanales).</summary>
    public int Semana { get; set; }

    /// <summary>Categoría comercial del ítem (<c>Primera</c> | <c>Pnc</c>); cadena vacía si el desglose no la trae.</summary>
    public string TipoHuevo { get; set; } = string.Empty;

    /// <summary>Código del ítem de huevo en el catálogo; cadena vacía si el desglose no lo trae.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Nombre del ítem de huevo; cadena vacía si el desglose no lo trae.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Cantidad total de huevos de ese ítem en la semana (suma de los días; siempre &gt; 0).</summary>
    public long Cantidad { get; set; }
}
