// Cálculo puro compartido de la liquidación de lotes de engorde.
// Usado por SeguimientoAvesEngordeService (Colombia) y
// SeguimientoAvesEngordeEcuadorService (Ecuador): misma aritmética en ambos países.
namespace ZooSanMarino.Application.Calculos;

public static class LiquidacionEngordeCalculos
{
    /// <summary>
    /// Aves de inicio del lote (H, M, X). Prioridad: registro "Inicio" del historial;
    /// si no existe, los saldos del lote; si todos son 0 y hay aves encasetadas, van como mixtas.
    /// </summary>
    public static (int Hembras, int Machos, int Mixtas) CalcularAvesInicio(
        bool tieneRegistroInicio,
        int iniHembras,
        int iniMachos,
        int iniMixtas,
        int? loteHembras,
        int? loteMachos,
        int? loteMixtas,
        int? avesEncasetadas)
    {
        if (tieneRegistroInicio)
            return (iniHembras, iniMachos, iniMixtas);

        var h = loteHembras ?? 0;
        var m = loteMachos ?? 0;
        var x = loteMixtas ?? 0;
        if (h + m + x == 0 && avesEncasetadas.HasValue && avesEncasetadas.Value > 0)
            x = avesEncasetadas.Value;
        return (h, m, x);
    }

    /// <summary>Aves vivas actuales: inicio − bajas acumuladas − ventas acumuladas, nunca negativo.</summary>
    public static int CalcularAvesVivas(int totalInicio, int bajasAcumuladas, int ventasAcumuladas) =>
        Math.Max(0, totalInicio - bajasAcumuladas - ventasAcumuladas);
}
