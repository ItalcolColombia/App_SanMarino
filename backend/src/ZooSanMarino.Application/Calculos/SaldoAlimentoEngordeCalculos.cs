// Cálculo puro compartido del saldo de alimento de engorde (multi-país).
// NOTA: YmdHistoricoEfectivo NO se comparte todavía: Colombia extrae la fecha
// efectiva desde la referencia del evento ("Seguimiento aves engorde #N yyyy-MM-dd",
// o cualquier fecha para INV_CONSUMO) y Ecuador usa FechaOperacion a secas.
// Esa divergencia afecta el orden del recálculo y está pendiente de decisión
// (ver tracker_estado.md — posible causa del descuadre del liquidador Ecuador).
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

public static class SaldoAlimentoEngordeCalculos
{
    /// <summary>
    /// Delta de kg y orden intra-día de un evento del histórico unificado para el
    /// recálculo de saldo de alimento. Solo INV_INGRESO (+, ord 0),
    /// INV_TRASLADO_ENTRADA (+, ord 1) e INV_TRASLADO_SALIDA (−, ord 2) participan.
    /// </summary>
    public static bool TryGetHistDeltaAndOrd(LoteRegistroHistoricoUnificado h, out decimal delta, out int ord)
    {
        delta = 0;
        ord = 0;
        if (h.Anulado) return false;
        var kg = h.CantidadKg ?? 0;
        switch (h.TipoEvento)
        {
            case "INV_INGRESO":
                if (kg == 0) return false;
                delta = kg; ord = 0; return true;
            case "INV_TRASLADO_ENTRADA":
                if (kg == 0) return false;
                delta = kg; ord = 1; return true;
            case "INV_TRASLADO_SALIDA":
                if (kg == 0) return false;
                delta = -Math.Abs(kg); ord = 2; return true;
            default:
                return false;
        }
    }
}
