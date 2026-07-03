// Cálculo PURO del kardex del inventario Colombia (modelo A). Es el ORÁCULO/contrato del
// saldo que la fn SQL fn_kardex_farm_inventory replica: si alguien cambia el signo por tipo
// de movimiento aquí, el test golden falla y avisa que la fn SQL debe alinearse (y viceversa).
//
// Refleja EXACTO el switch histórico de FarmInventoryReportService.GetKardexAsync (antes en C#):
//   Entry, TransferIn      -> +1
//   Exit,  TransferOut     -> -1
//   Adjust                 -> quantity >= 0 ? +1 : -1
//   (cualquier otro)       -> 0
// El saldo es la suma acumulada de (signo * quantity) en el orden dado (created_at, id).
namespace ZooSanMarino.Application.Calculos;

public static class FarmInventoryKardexCalculos
{
    /// <summary>
    /// Signo del movimiento según su tipo (nombre del enum, tal como se persiste como string).
    /// <paramref name="quantity"/> solo importa para 'Adjust'. Tipos no mapeados → 0.
    /// </summary>
    public static decimal Signo(string movementType, decimal quantity) => movementType switch
    {
        "Entry"       => +1m,
        "TransferIn"  => +1m,
        "Exit"        => -1m,
        "TransferOut" => -1m,
        "Adjust"      => quantity >= 0 ? +1m : -1m,
        _             => 0m
    };

    /// <summary>Delta con signo de un movimiento (== Cantidad emitida en el kardex).</summary>
    public static decimal Delta(string movementType, decimal quantity)
        => Signo(movementType, quantity) * quantity;

    /// <summary>
    /// Saldo acumulado sobre una secuencia YA ORDENADA (created_at, id): suma corriente de los
    /// deltas. Devuelve el saldo tras cada movimiento (mismo orden de entrada).
    /// </summary>
    public static IReadOnlyList<decimal> SaldosAcumulados(IEnumerable<(string MovementType, decimal Quantity)> movimientosOrdenados)
    {
        var saldos = new List<decimal>();
        var saldo = 0m;
        foreach (var m in movimientosOrdenados)
        {
            saldo += Delta(m.MovementType, m.Quantity);
            saldos.Add(saldo);
        }
        return saldos;
    }
}
