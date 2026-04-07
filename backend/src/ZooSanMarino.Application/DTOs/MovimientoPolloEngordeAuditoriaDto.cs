namespace ZooSanMarino.Application.DTOs;

public sealed class AuditoriaVentasEngordeRequest
{
    public int? GranjaId { get; set; }
    public List<int>? LoteAveEngordeIds { get; set; }
    /// <summary>Si true, aplica correcciones automáticas (solo sobre movimientos Pendiente).</summary>
    public bool AplicarCorreccion { get; set; } = false;
    /// <summary>Si true, simula y no persiste cambios (tiene prioridad sobre AplicarCorreccion).</summary>
    public bool DryRun { get; set; } = true;
}

public sealed record AuditoriaVentasLoteDetalle(
    int LoteAveEngordeId,
    string? LoteNombre,
    int EncasetadasH,
    int EncasetadasM,
    int EncasetadasX,
    int MortCajaH,
    int MortCajaM,
    int MortSegH,
    int MortSegM,
    int SelH,
    int SelM,
    int ErrSexH,
    int ErrSexM,
    int AsignadasH,
    int AsignadasM,
    int MaxVendibleH,
    int MaxVendibleM,
    int MaxVendibleX,
    int VendidasCompletadoH,
    int VendidasCompletadoM,
    int VendidasCompletadoX,
    int VendidasPendienteH,
    int VendidasPendienteM,
    int VendidasPendienteX,
    int ExcesoH,
    int ExcesoM,
    int ExcesoX,
    bool AutoCorregible,
    string Estado
);

public sealed record AuditoriaCorreccionAccion(
    int MovimientoId,
    string NumeroMovimiento,
    int LoteAveEngordeOrigenId,
    int AntesH,
    int AntesM,
    int AntesX,
    int DespuesH,
    int DespuesM,
    int DespuesX,
    string Nota
);

public sealed class AuditoriaVentasEngordeResponse
{
    public bool Ok { get; set; }
    public bool DryRun { get; set; }
    public bool AplicarCorreccion { get; set; }
    public string? Mensaje { get; set; }
    public List<AuditoriaVentasLoteDetalle> Lotes { get; set; } = new();
    public List<AuditoriaCorreccionAccion> Acciones { get; set; } = new();
}

