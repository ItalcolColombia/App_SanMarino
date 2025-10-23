using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para obtener datos de guías genéticas
/// </summary>
public interface IGuiaGeneticaService
{
    /// <summary>
    /// Obtiene los datos de guía genética para una raza, año y edad específicos
    /// </summary>
    Task<GuiaGeneticaResponse> ObtenerGuiaGeneticaAsync(GuiaGeneticaRequest request);

    /// <summary>
    /// Obtiene múltiples edades de una guía genética
    /// </summary>
    Task<IEnumerable<GuiaGeneticaDto>> ObtenerGuiaGeneticaRangoAsync(string raza, int anoTabla, int edadDesde, int edadHasta);

    /// <summary>
    /// Verifica si existe una guía genética para los parámetros dados
    /// </summary>
    Task<bool> ExisteGuiaGeneticaAsync(string raza, int anoTabla);

    /// <summary>
    /// Obtiene las razas disponibles en las guías genéticas
    /// </summary>
    Task<IEnumerable<string>> ObtenerRazasDisponiblesAsync();

    /// <summary>
    /// Obtiene los años disponibles para una raza específica
    /// </summary>
    Task<IEnumerable<int>> ObtenerAnosDisponiblesAsync(string raza);
}
