// src/ZooSanMarino.Domain/Entities/LotePosturaLevante.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Lote de postura en etapa Levante. Tabla independiente con todos los campos de Lote
/// más campos específicos para distribución de lotes postura.
/// </summary>
public class LotePosturaLevante : AuditableEntity
{
    public int? LotePosturaLevanteId { get; set; }

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

    // Campos específicos postura levante
    public int? LoteId { get; set; }
    public int? LotePadreId { get; set; }
    public int? LotePosturaLevantePadreId { get; set; } // lote_postura_levante_id: referencia al lote levante padre
    public int? AvesHInicial { get; set; }
    public int? AvesMInicial { get; set; }
    public int? AvesHActual { get; set; }
    public int? AvesMActual { get; set; }
    public int? EmpresaId { get; set; }
    public int? UsuarioId { get; set; }
    public string? Estado { get; set; }
    public string? Etapa { get; set; }
    public int? Edad { get; set; }
    /// <summary>Abierto: antes semana 26. Cerrado: al llegar semana 26, se cierra y se crean lotes producción (H/M).</summary>
    public string? EstadoCierre { get; set; }

    public Farm Farm { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }
    public Lote? Lote { get; set; }
    public LotePosturaLevante? LotePosturaLevantePadre { get; set; }
}
