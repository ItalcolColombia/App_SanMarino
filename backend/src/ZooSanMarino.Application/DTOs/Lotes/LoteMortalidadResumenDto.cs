namespace ZooSanMarino.Application.DTOs.Lotes;

public sealed class LoteMortalidadResumenDto
{
    public string LoteId { get; init; } = default!;

    // Sumas acumuladas (de SeguimientoLoteLevante)
    public int MortalidadAcumHembras { get; init; }
    public int MortalidadAcumMachos  { get; init; }

    // Bases del lote (valores de carga inicial)
    public int HembrasIniciales { get; init; }
    public int MachosIniciales  { get; init; }

    // Mortandad en caja (si aplica en tu modelo)
    public int MortCajaHembras  { get; init; }
    public int MortCajaMachos   { get; init; }

    // Saldos resultantes solicitados (solo restando mortalidad)
    public int SaldoHembras     { get; init; }
    public int SaldoMachos      { get; init; }
}
