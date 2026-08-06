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
    /// Peso báscula OBLIGATORIO al registrar ventas (regla de negocio tras el incidente
    /// de una venta guardada con pesos NULL: quedaba en 0 kg y descuadraba la liquidación
    /// y los reportes de indicadores). Aplica cuando el tipo de movimiento es "Venta";
    /// los traslados no pasan por báscula.
    /// </summary>
    /// <param name="pesoDiferidoPermitido">
    /// Empresas con el flag <c>venta_engorde_peso_diferido</c> (Panamá): la báscula llega al día
    /// siguiente, así que la venta puede registrarse SIN peso y queda "Pendiente" hasta que se
    /// carga en la confirmación. Sólo se tolera la ausencia TOTAL de peso: un peso a medias
    /// (sólo bruto o sólo tara) sigue siendo un error de digitación en ambos modos, igual que
    /// los valores fuera de rango. Default <c>false</c> ⇒ comportamiento histórico intacto.
    /// </param>
    public static void ValidarPesoObligatorioEnVenta(
        string? tipoMovimiento, double? pesoBruto, double? pesoTara, bool pesoDiferidoPermitido = false)
    {
        if (tipoMovimiento != "Venta") return;
        if (pesoDiferidoPermitido && !pesoBruto.HasValue && !pesoTara.HasValue) return;
        if (!pesoBruto.HasValue || !pesoTara.HasValue)
            throw new InvalidOperationException(
                "El peso báscula es obligatorio para registrar la venta: indique peso bruto y peso tara.");
        if (pesoBruto.Value <= 0)
            throw new InvalidOperationException("El peso bruto de la venta debe ser mayor a 0 kg.");
        if (pesoTara.Value < 0)
            throw new InvalidOperationException("El peso tara no puede ser negativo.");
        if (pesoBruto.Value < pesoTara.Value)
            throw new InvalidOperationException("El peso bruto no puede ser menor que el peso tara.");
    }

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

    /// <summary>Ubicación (granja / núcleo / galpón) de un lado del movimiento.</summary>
    public readonly record struct UbicacionMovimiento(int? GranjaId, string? NucleoId, string? GalponId);

    /// <summary>
    /// Ubicación DESTINO efectiva de un movimiento: <b>campo por campo</b>, lo que mandó el cliente manda
    /// y lo que falte se completa con la ubicación del lote destino.
    /// <para>
    /// <b>Por qué existe:</b> desde que el traslado de engorde puede apuntar a otra granja/galpón, el front
    /// envía la ubicación destino explícita; pero los flujos históricos (y la carga masiva) mandan solo el
    /// lote, y la cascada del modal permite elegir la granja sin bajar a galpón. Como las aves aterrizan
    /// físicamente en el galpón del lote destino, ese dato se completa igual: dejarlo nulo perdería
    /// información que el movimiento sí tiene. Sin lote destino (venta / retiro / ajuste) no se inventa nada
    /// y la ubicación queda exactamente como llegó — comportamiento previo intacto.
    /// </para>
    /// </summary>
    /// <param name="explicita">Ubicación destino tal como llegó en el DTO.</param>
    /// <param name="delLoteDestino">Ubicación del lote destino; <c>null</c> si el movimiento no tiene destino.</param>
    public static UbicacionMovimiento ResolverUbicacionDestino(
        UbicacionMovimiento explicita, UbicacionMovimiento? delLoteDestino)
    {
        if (delLoteDestino is not { } lote) return explicita;

        return new UbicacionMovimiento(
            explicita.GranjaId ?? lote.GranjaId,
            string.IsNullOrWhiteSpace(explicita.NucleoId) ? lote.NucleoId : explicita.NucleoId,
            string.IsNullOrWhiteSpace(explicita.GalponId) ? lote.GalponId : explicita.GalponId);
    }
}
