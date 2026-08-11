// src/ZooSanMarino.Application/Calculos/GastoLoteProgramadoCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Reglas PURAS del gasto de inventario cargado contra un lote PROGRAMADO (lote base) que todavía no
/// existe como <c>lote_ave_engorde</c> — el caso de la desinsectación previa al encasetamiento.
/// <para>
/// Sin EF ni estado: el service resuelve empresa/permisos y aplica; acá vive el «cuándo».
/// </para>
/// </summary>
public static class GastoLoteProgramadoCalculos
{
    /// <summary>Estado de un gasto vivo (los anulados devolvieron su stock y no se re-atribuyen).</summary>
    public const string EstadoActivo = "Activo";

    /// <summary>
    /// Un gasto apunta a un lote REAL o a uno PROGRAMADO, nunca a los dos. Devuelve el mensaje de
    /// error (400) o <c>null</c> si el destino es válido. Sin lote es válido: es un gasto de granja.
    /// </summary>
    public static string? ValidarDestino(int? loteAveEngordeId, int? loteBaseEngordeId)
        => loteAveEngordeId.HasValue && loteBaseEngordeId.HasValue
            ? "El gasto se carga contra un lote real o contra un lote programado, no contra ambos."
            : null;

    /// <summary>Gasto candidato a re-atribución (los datos que decide la regla, nada más).</summary>
    public readonly record struct GastoPendiente(
        string Estado,
        int FarmId,
        string? GalponId,
        int? LoteAveEngordeId,
        int? LoteBaseEngordeId,
        DateTime Fecha
    );

    /// <summary>Lote real recién creado desde una programación.</summary>
    public readonly record struct LoteCreado(
        int FarmId,
        string? GalponId,
        int? LoteBaseEngordeId,
        DateTime FechaEncaset
    );

    /// <summary>
    /// ¿El gasto pendiente pasa a colgar del lote recién creado?
    /// <para>
    /// Condiciones (todas): el gasto está vivo, no fue atribuido todavía, apunta al MISMO lote base,
    /// es de la MISMA granja, su galpón coincide con el del lote (o el gasto es de granja, sin galpón)
    /// y su fecha no es posterior al encasetamiento.
    /// </para>
    /// <para>
    /// El corte por fecha es lo que hace la regla determinista con lotes base REUTILIZABLES: el
    /// segundo lote del mismo base+galpón solo puede tomar lo que el primero no reclamó, porque lo
    /// anterior a SU encaset ya quedó atribuido. Sin ese corte, un lote nuevo se llevaría los insumos
    /// que la granja ya alistó para el ciclo siguiente.
    /// </para>
    /// </summary>
    public static bool DebeReatribuir(GastoPendiente gasto, LoteCreado lote)
    {
        if (!lote.LoteBaseEngordeId.HasValue) return false;
        if (!string.Equals(gasto.Estado, EstadoActivo, StringComparison.OrdinalIgnoreCase)) return false;
        if (gasto.LoteAveEngordeId.HasValue) return false;
        if (gasto.LoteBaseEngordeId != lote.LoteBaseEngordeId) return false;
        if (gasto.FarmId != lote.FarmId) return false;

        // Gasto sin galpón = gasto de granja para ese lote programado ⇒ entra igual.
        if (!string.IsNullOrWhiteSpace(gasto.GalponId) &&
            !string.Equals(gasto.GalponId.Trim(), (lote.GalponId ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return gasto.Fecha.Date <= lote.FechaEncaset.Date;
    }
}
