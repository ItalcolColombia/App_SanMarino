using System;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Reflejo en EF de la tabla Postgres generada por el script:
/// public.lote_registro_historico_unificado
///
/// Se usa para calcular en UI/servicio totales diarios (ingresos/traslados/ventas)
/// sin tener que consultar múltiples orígenes.
/// </summary>
public class LoteRegistroHistoricoUnificado
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    public int? LoteAveEngordeId { get; set; }

    public int FarmId { get; set; }

    public string? NucleoId { get; set; }

    public string? GalponId { get; set; }

    public DateTime FechaOperacion { get; set; }

    public string TipoEvento { get; set; } = null!;

    public string OrigenTabla { get; set; } = null!;

    public int OrigenId { get; set; }

    public string? MovementTypeOriginal { get; set; }

    public int? ItemInventarioEcuadorId { get; set; }

    public string? ItemResumen { get; set; }

    public decimal? CantidadKg { get; set; }

    public string? Unidad { get; set; }

    public int? CantidadHembras { get; set; }

    public int? CantidadMachos { get; set; }

    public int? CantidadMixtas { get; set; }

    public string? Referencia { get; set; }

    public string? NumeroDocumento { get; set; }

    public decimal? AcumuladoEntradasAlimentoKg { get; set; }

    public bool Anulado { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

