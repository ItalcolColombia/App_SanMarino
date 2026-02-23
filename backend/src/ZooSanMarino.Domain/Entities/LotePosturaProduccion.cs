// src/ZooSanMarino.Domain/Entities/LotePosturaProduccion.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Lote de postura en etapa Producción. Tabla independiente con todos los campos de Lote,
/// clasificación de huevos y campos específicos para lotes postura producción.
/// </summary>
public class LotePosturaProduccion : AuditableEntity
{
    public int? LotePosturaProduccionId { get; set; }

    // Campos base de Lote
    public string LoteNombre { get; set; } = null!;
    public int GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public string? Regional { get; set; }
    public DateTime? FechaEncaset { get; set; }
    public int? HembrasL { get; set; }
    public int? MachosL { get; set; }
    public double? PesoInicialH { get; set; }
    public double? PesoInicialM { get; set; }
    public double? UnifH { get; set; }
    public double? UnifM { get; set; }
    public int? MortCajaH { get; set; }
    public int? MortCajaM { get; set; }
    public string? Raza { get; set; }
    public int? AnoTablaGenetica { get; set; }
    public string? Linea { get; set; }
    public string? TipoLinea { get; set; }
    public string? CodigoGuiaGenetica { get; set; }
    public int? LineaGeneticaId { get; set; }
    public string? Tecnico { get; set; }
    public int? Mixtas { get; set; }
    public double? PesoMixto { get; set; }
    public int? AvesEncasetadas { get; set; }
    public int? EdadInicial { get; set; }
    public string? LoteErp { get; set; }
    public string? EstadoTraslado { get; set; }
    public int? PaisId { get; set; }
    public string? PaisNombre { get; set; }
    public string? EmpresaNombre { get; set; }

    // Campos Producción (Lote)
    public DateTime? FechaInicioProduccion { get; set; }
    public int? HembrasInicialesProd { get; set; }
    public int? MachosInicialesProd { get; set; }
    public int? HuevosIniciales { get; set; }
    public string? TipoNido { get; set; }
    public string? NucleoP { get; set; }
    public string? CicloProduccion { get; set; }
    public DateTime? FechaFinProduccion { get; set; }
    public int? AvesFinHembrasProd { get; set; }
    public int? AvesFinMachosProd { get; set; }

    // Clasificación de huevos
    public int? HuevoTot { get; set; }
    public int? HuevoInc { get; set; }
    public int? HuevoLimpio { get; set; }
    public int? HuevoTratado { get; set; }
    public int? HuevoSucio { get; set; }
    public int? HuevoDeforme { get; set; }
    public int? HuevoBlanco { get; set; }
    public int? HuevoDobleYema { get; set; }
    public int? HuevoPiso { get; set; }
    public int? HuevoPequeno { get; set; }
    public int? HuevoRoto { get; set; }
    public int? HuevoDesecho { get; set; }
    public int? HuevoOtro { get; set; }
    public decimal? PesoHuevo { get; set; }

    // Campos específicos postura producción
    public int? LoteId { get; set; }
    public int? LotePadreId { get; set; }
    public int? LotePosturaLevanteId { get; set; }
    public int? AvesHInicial { get; set; }
    public int? AvesMInicial { get; set; }
    public int? AvesHActual { get; set; }
    public int? AvesMActual { get; set; }
    public int? EmpresaId { get; set; }
    public int? UsuarioId { get; set; }
    public string? Estado { get; set; }
    public string? Etapa { get; set; }
    public int? Edad { get; set; }
    /// <summary>Abierta = lote en producción activa. Cerrada = producción finalizada.</summary>
    public string? EstadoCierre { get; set; }

    public Farm Farm { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }
    public Lote? Lote { get; set; }
    public LotePosturaLevante? LotePosturaLevante { get; set; }
}
