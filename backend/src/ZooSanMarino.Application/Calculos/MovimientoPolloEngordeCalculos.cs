namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculos puros (sin dependencias de infraestructura) del módulo Movimientos de Pollo Engorde:
/// prorrateo de peso por lote dentro de un despacho y límites/excesos de la auditoría de ventas.
/// Extraídos del servicio para ser deterministas y testeables; la aritmética es idéntica a la que
/// vivía inline en el servicio (mismos factores, redondeo a 3 decimales y ajuste de residuo).
/// </summary>
public static class MovimientoPolloEngordeCalculos
{
    /// <summary>Peso prorrateado de una línea del despacho (null cuando el despacho no tiene aves/peso).</summary>
    public readonly record struct PesoLineaProrrateado(double? Bruto, double? Tara, double? Neto, double? Promedio);

    /// <summary>
    /// Distribuye el peso bruto/tara/neto global proporcionalmente a las aves de cada línea, con
    /// ajuste del residuo de redondeo (3 decimales) a la línea con más aves — espejo del frontend
    /// (prorateo-peso). El neto global se deriva como bruto − tara. Si no hay aves en el despacho
    /// (suma = 0) devuelve una entrada por línea con todos los valores en null.
    /// </summary>
    public static PesoLineaProrrateado[] ProrratearPesoPorLinea(
        double pesoBrutoGlobal, double pesoTaraGlobal, IReadOnlyList<int> avesPorLinea)
    {
        var n = avesPorLinea.Count;
        var bruto = new double?[n];
        var tara  = new double?[n];
        var neto  = new double?[n];
        var prom  = new double?[n];

        var totalAves = 0;
        for (int i = 0; i < n; i++) totalAves += avesPorLinea[i];

        if (totalAves > 0)
        {
            var pesoNetoGlobal = pesoBrutoGlobal - pesoTaraGlobal;

            for (int i = 0; i < n; i++)
            {
                var aves   = avesPorLinea[i];
                var factor = (double)aves / totalAves;
                bruto[i] = Math.Round(pesoBrutoGlobal * factor, 3);
                tara[i]  = Math.Round(pesoTaraGlobal  * factor, 3);
                neto[i]  = Math.Round(pesoNetoGlobal  * factor, 3);
                prom[i]  = aves > 0 ? neto[i]!.Value / aves : 0d;
            }

            // Ajuste de residuo de redondeo a la línea con mayor cantidad de aves.
            int maxIdx = 0, maxAves = 0;
            for (int i = 0; i < n; i++)
            {
                if (avesPorLinea[i] > maxAves) { maxAves = avesPorLinea[i]; maxIdx = i; }
            }
            var residuoBruto = pesoBrutoGlobal - bruto.Sum(x => x ?? 0d);
            var residuoTara  = pesoTaraGlobal  - tara.Sum(x => x ?? 0d);
            var residuoNeto  = pesoNetoGlobal  - neto.Sum(x => x ?? 0d);
            bruto[maxIdx] = Math.Round(bruto[maxIdx]!.Value + residuoBruto, 3);
            tara[maxIdx]  = Math.Round(tara[maxIdx]!.Value  + residuoTara,  3);
            neto[maxIdx]  = Math.Round(neto[maxIdx]!.Value  + residuoNeto,  3);
            prom[maxIdx]  = maxAves > 0 ? neto[maxIdx]!.Value / maxAves : 0d;
        }

        var result = new PesoLineaProrrateado[n];
        for (int i = 0; i < n; i++)
            result[i] = new PesoLineaProrrateado(bruto[i], tara[i], neto[i], prom[i]);
        return result;
    }

    /// <summary>
    /// Aves máximas vendibles por sexo = max(0, encasetadas − mortalidad de caja −
    /// mortalidad de seguimiento − selección − error de sexaje − asignadas a otros lotes).
    /// </summary>
    public static int MaxVendiblePorSexo(
        int encasetadas, int mortalidadCaja, int mortalidadSeguimiento,
        int seleccion, int errorSexaje, int asignadas)
        => Math.Max(0, encasetadas - mortalidadCaja - mortalidadSeguimiento - seleccion - errorSexaje - asignadas);

    /// <summary>Exceso de ventas por sexo = max(0, vendidas − máximo vendible).</summary>
    public static int Exceso(int totalVendidas, int maxVendible)
        => Math.Max(0, totalVendidas - maxVendible);
}
