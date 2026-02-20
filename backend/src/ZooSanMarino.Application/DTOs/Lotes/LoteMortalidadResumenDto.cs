namespace ZooSanMarino.Application.DTOs.Lotes;

public sealed class LoteMortalidadResumenDto
{
    public string LoteId { get; init; } = default!;

    // Bases del lote (desde historial lote_etapa_levante o lote)
    public int HembrasIniciales { get; init; }
    public int MachosIniciales  { get; init; }

    // Mortandad en caja
    public int MortCajaHembras  { get; init; }
    public int MortCajaMachos   { get; init; }

    // Descuentos acumulados (desde seguimiento_diario tipo levante)
    public int MortalidadAcumHembras { get; init; }
    public int MortalidadAcumMachos  { get; init; }
    public int SelAcumHembras        { get; init; }
    public int SelAcumMachos         { get; init; }
    public int ErrorSexajeAcumHembras { get; init; }
    public int ErrorSexajeAcumMachos  { get; init; }

    // Saldos resultantes (inicio - mort caja - mortalidad - sel - error sexaje)
    public int SaldoHembras     { get; init; }
    public int SaldoMachos      { get; init; }
}
