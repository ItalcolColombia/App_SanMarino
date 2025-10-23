using System.ComponentModel.DataAnnotations;

namespace ZooSanMarino.Application.DTOs
{
    /// <summary>
    /// DTO para estadísticas generales del dashboard
    /// </summary>
    public class DashboardEstadisticasGeneralesDto
    {
        public int TotalUsuarios { get; set; }
        public int UsuariosActivos { get; set; }
        public int TotalGranjas { get; set; }
        public int TotalLotes { get; set; }
        public int TotalLotesReproductora { get; set; }
        public int TotalLotesProduccion { get; set; }
        public int TotalLotesLevante { get; set; }
        public int TotalMovimientosPendientes { get; set; }
        public int TotalMovimientosCompletados { get; set; }
        public int TotalInventarioAves { get; set; }
        public DateTime UltimaActualizacion { get; set; }
    }

    /// <summary>
    /// DTO para producción por granja
    /// </summary>
    public class ProduccionGranjaDto
    {
        public int GranjaId { get; set; }
        public string GranjaNombre { get; set; } = string.Empty;
        public int TotalLotes { get; set; }
        public int TotalHuevos { get; set; }
        public int TotalAves { get; set; }
        public double Eficiencia { get; set; }
    }

    /// <summary>
    /// DTO para registros diarios
    /// </summary>
    public class RegistroDiarioDto
    {
        public DateTime Fecha { get; set; }
        public int RegistrosSeguimiento { get; set; }
        public int MovimientosAves { get; set; }
        public int CambiosInventario { get; set; }
        public int TotalRegistros { get; set; }
    }

    /// <summary>
    /// DTO para actividades recientes
    /// </summary>
    public class ActividadRecienteDto
    {
        public string Id { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para estadísticas de mortalidad
    /// </summary>
    public class MortalidadDto
    {
        public DateTime Fecha { get; set; }
        public int CantidadMuertas { get; set; }
        public string LoteId { get; set; } = string.Empty;
        public string GranjaNombre { get; set; } = string.Empty;
        public string CausaMuerte { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para distribución de lotes
    /// </summary>
    public class DistribucionLotesDto
    {
        public int GranjaId { get; set; }
        public string GranjaNombre { get; set; } = string.Empty;
        public int LotesReproductora { get; set; }
        public int LotesProduccion { get; set; }
        public int LotesLevante { get; set; }
        public int TotalLotes { get; set; }
    }

    /// <summary>
    /// DTO para estadísticas de inventario
    /// </summary>
    public class InventarioEstadisticasDto
    {
        public int TotalInventarios { get; set; }
        public int TotalAvesHembras { get; set; }
        public int TotalAvesMachos { get; set; }
        public int TotalAvesMixtas { get; set; }
        public int InventariosBajoStock { get; set; }
        public DateTime UltimaActualizacion { get; set; }
    }

    /// <summary>
    /// DTO para métricas de rendimiento
    /// </summary>
    public class MetricasRendimientoDto
    {
        public double PromedioProduccionDiaria { get; set; }
        public double EficienciaPromedio { get; set; }
        public double TasaMortalidadPromedio { get; set; }
        public double MovimientosPorDia { get; set; }
        public double RegistrosPorDia { get; set; }
        public DateTime UltimaActualizacion { get; set; }
    }

    /// <summary>
    /// DTO para gráfico de tendencias
    /// </summary>
    public class TendenciaDto
    {
        public DateTime Fecha { get; set; }
        public double Valor { get; set; }
        public string Categoria { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para alertas del dashboard
    /// </summary>
    public class AlertaDashboardDto
    {
        public string Id { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty; // "warning", "error", "info", "success"
        public string Titulo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
        public DateTime FechaCreacion { get; set; }
        public bool EsLeida { get; set; }
        public string Icono { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para resumen de KPIs
    /// </summary>
    public class KpiResumenDto
    {
        public string Nombre { get; set; } = string.Empty;
        public double Valor { get; set; }
        public double ValorAnterior { get; set; }
        public double PorcentajeCambio { get; set; }
        public string Unidad { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
    }
}
