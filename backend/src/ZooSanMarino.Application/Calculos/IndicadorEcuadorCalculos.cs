namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculos puros (sin dependencias de infraestructura) de la liquidación técnica Ecuador:
/// mermas, ajuste de aves, kilos a cliente, días de engorde y excedente de sobrante.
/// Extraídos del servicio para ser deterministas y testeables (Parte B / R1-R2).
/// </summary>
public static class IndicadorEcuadorCalculos
{
    /// <summary>
    /// Conversión ajustada = conversión + (pesoAjuste − pesoPromedio) / divisorAjuste.
    /// Devuelve 0 cuando la conversión es ≤ 0 (sin kg de carne producidos).
    /// </summary>
    public static decimal ConversionAjustada(decimal conversion, decimal pesoPromedio, decimal pesoAjuste, decimal divisorAjuste)
        => conversion <= 0 ? 0m : conversion + ((pesoAjuste - pesoPromedio) / divisorAjuste);

    /// <summary>Merma % = merma_unidades / aves_vendidas × 100.</summary>
    public static decimal MermaPorcentaje(int mermaUnidades, int avesVendidas)
        => avesVendidas > 0 ? (decimal)mermaUnidades / avesVendidas * 100 : 0m;

    /// <summary>Ajuste de aves = encasetadas − vendidas − (mortalidad + selección). Negativo ⇒ sobrante.</summary>
    public static int AjusteAves(int avesEncasetadas, int avesVendidas, int mortalidad)
        => avesEncasetadas - avesVendidas - mortalidad;

    /// <summary>% de ajuste = ajuste / encasetadas × 100.</summary>
    public static decimal PorcentajeAjuste(int ajusteAves, int avesEncasetadas)
        => avesEncasetadas > 0 ? (decimal)ajusteAves / avesEncasetadas * 100 : 0m;

    /// <summary>Total kilos despachados a cliente = producción kilo en pie − merma kilos.</summary>
    public static decimal TotalKilosDespachadosCliente(decimal produccionKiloEnPie, decimal mermaKilos)
        => produccionKiloEnPie - mermaKilos;

    /// <summary>Días de engorde = días entre encasetamiento y fecha de cierre/último despacho (≥ 0).</summary>
    public static int DiasEngorde(DateTime? fechaEncaset, DateTime? fechaCierre)
        => (fechaEncaset.HasValue && fechaCierre.HasValue)
            ? Math.Max(0, (int)(fechaCierre.Value.Date - fechaEncaset.Value.Date).TotalDays)
            : 0;

    /// <summary>Excedente de sobrante por sexo: suma de (solicitado − disponible) acotado a ≥ 0 (R2).</summary>
    public static int ExcedenteSobrante(
        int solicitadoH, int dispH,
        int solicitadoM, int dispM,
        int solicitadoX, int dispX)
        => Math.Max(0, solicitadoH - dispH)
         + Math.Max(0, solicitadoM - dispM)
         + Math.Max(0, solicitadoX - dispX);
}
