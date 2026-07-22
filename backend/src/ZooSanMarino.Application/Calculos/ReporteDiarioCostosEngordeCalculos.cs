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
