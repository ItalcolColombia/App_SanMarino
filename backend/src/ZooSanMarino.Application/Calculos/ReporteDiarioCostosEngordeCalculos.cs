// src/ZooSanMarino.Application/Calculos/ReporteDiarioCostosEngordeCalculos.cs
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosEngorde;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO del Reporte Diario Costos de engorde (sin EF ni estado):
/// totales del footer (global, por alimento, por galpón) y aves vivas actuales.
/// La agregación por fecha viene de fn_reporte_diario_costos_engorde; aquí solo
/// se consolidan las filas ya calculadas.
/// </summary>
public static class ReporteDiarioCostosEngordeCalculos
{
    /// <summary>Redondeo estándar del reporte para kg (3 decimales, half away from zero como el resto del módulo).</summary>
    public static double RedondearKg(double valor) => Math.Round(valor, 3, MidpointRounding.AwayFromZero);

    /// <summary>SUMA TOTAL del footer: consumo global, mort+sel global, por alimento y por galpón.</summary>
    public static ReporteDiarioCostosTotalesDto ConstruirTotales(IReadOnlyList<ReporteDiarioCostosFilaDto> filas)
    {
        if (filas.Count == 0)
            return new ReporteDiarioCostosTotalesDto(
                0, 0,
                Array.Empty<ReporteDiarioCostosAlimentoTotalDto>(),
                Array.Empty<ReporteDiarioCostosGalponTotalDto>());

        var consumoTotal = RedondearKg(filas.Sum(f => f.ConsumoTotalKg));
        var mortSelTotal = filas.Sum(f => f.MortSelTotal);

        var alimentos = filas
            .SelectMany(f => f.Alimentos)
            .GroupBy(a => a.NombreAlimento, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReporteDiarioCostosAlimentoTotalDto(g.First().NombreAlimento, RedondearKg(g.Sum(a => a.ConsumoKg))))
            .OrderBy(a => a.NombreAlimento, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var porGalpon = filas
            .SelectMany(f => f.Galpones)
            .GroupBy(g => g.GalponId)
            .Select(g => new ReporteDiarioCostosGalponTotalDto(
                g.Key,
                g.First().GalponNombre,
                g.Sum(x => x.Mortalidad),
                g.Sum(x => x.Seleccion),
                g.Sum(x => x.ErrSexaje),
                g.Sum(x => x.MortSel)))
            .OrderBy(g => g.GalponNombre, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.GalponId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ReporteDiarioCostosTotalesDto(consumoTotal, mortSelTotal, alimentos, porGalpon);
    }

    /// <summary>
    /// Alcance granular de ubicación (user_farms.restrict_locations): deja en cada fila SOLO los
    /// galpones visibles del usuario y recalcula los totales del día con la MISMA aritmética de la
    /// fn (suma por galpón). El desglose de alimento se descarta: la fn lo agrega a nivel GRANJA
    /// COMPLETA y no es atribuible por galpón (fail-closed). Las filas que quedan sin galpón visible
    /// se descartan. Solo se usa cuando la granja está restringida; en el caso normal ni se llama.
    /// </summary>
    public static IReadOnlyList<ReporteDiarioCostosFilaDto> FiltrarPorGalponesVisibles(
        IReadOnlyList<ReporteDiarioCostosFilaDto> filas, IReadOnlySet<string> galponesVisibles)
    {
        var result = new List<ReporteDiarioCostosFilaDto>();
        foreach (var f in filas)
        {
            var visibles = f.Galpones.Where(g => galponesVisibles.Contains(g.GalponId)).ToList();
            if (visibles.Count == 0) continue;
            result.Add(f with
            {
                ConsumoTotalKg = RedondearKg(visibles.Sum(g => g.ConsumoKg)),
                MortSelTotal = visibles.Sum(g => g.MortSel),
                AvesVivasTotal = visibles.Sum(g => g.AvesVivas),
                Alimentos = Array.Empty<ReporteDiarioCostosAlimentoDto>(),
                Galpones = visibles
            });
        }
        return result;
    }

    /// <summary>
    /// Aves vivas "actuales" del reporte = las de la ÚLTIMA fecha (por galpón + total).
    /// Sin filas → lista vacía y total 0.
    /// </summary>
    public static (IReadOnlyList<ReporteDiarioCostosAvesActualesDto> PorGalpon, int Total) AvesVivasActuales(
        IReadOnlyList<ReporteDiarioCostosFilaDto> filas)
    {
        if (filas.Count == 0)
            return (Array.Empty<ReporteDiarioCostosAvesActualesDto>(), 0);

        var ultima = filas.MaxBy(f => f.Fecha)!;
        var porGalpon = ultima.Galpones
            .Select(g => new ReporteDiarioCostosAvesActualesDto(g.GalponId, g.GalponNombre, g.AvesVivas))
            .ToList();
        return (porGalpon, ultima.AvesVivasTotal);
    }
}
