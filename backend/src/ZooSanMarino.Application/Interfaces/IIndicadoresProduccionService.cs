using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para calcular indicadores semanales de producción diaria
/// </summary>
public interface IIndicadoresProduccionService
{
    /// <summary>
    /// Obtiene indicadores semanales de producción agrupados por semana
    /// Compara con guía genética cuando está disponible
    /// </summary>
    Task<IndicadoresProduccionResponse> ObtenerIndicadoresSemanalesAsync(IndicadoresProduccionRequest request);
    
    /// <summary>
    /// Obtiene indicadores para una semana específica
    /// </summary>
    Task<IndicadorProduccionSemanalDto?> ObtenerIndicadorSemanaAsync(int loteId, int semana);

    /// <summary>
    /// Desglose de la clasificación de huevos POR ÍTEM del catálogo (Primera/Pnc) por semana,
    /// para las empresas con <c>companies.clasificacion_huevo_por_items = true</c> (el desglose vive
    /// en <c>seguimiento_diario_produccion.metadata → huevoItems</c> y las 11 columnas fijas van en 0).
    /// <para>Usa el MISMO request y la MISMA resolución de lote que
    /// <see cref="ObtenerIndicadoresSemanalesAsync"/>, con la misma fórmula de semana → una fila por
    /// semana × ítem que casa 1:1 con la grilla de indicadores. Lista vacía si el lote no tiene
    /// desglose (nunca error).</para>
    /// </summary>
    Task<List<ClasificacionHuevoItemSemanaDto>> ObtenerClasificacionHuevoItemsAsync(IndicadoresProduccionRequest request);
}





