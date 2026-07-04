// Cálculo puro compartido del seguimiento diario de engorde.
// Usado por SeguimientoAvesEngordeService (Colombia) y
// SeguimientoAvesEngordeEcuadorService (Ecuador): estas funciones estaban
// duplicadas byte a byte en ambos servicios.
namespace ZooSanMarino.Application.Calculos;

public static class SeguimientoEngordeCalculos
{
    /// <summary>Kcal/ave y proteína/ave del día: consumo (kg) × valor del alimento, redondeado a 3 decimales.</summary>
    public static (double? KcalAveH, double? ProtAveH) CalcularDerivados(double consumoKgHembras, double? kcalAlH, double? protAlH)
    {
        double? kcal = kcalAlH is null ? null : Math.Round(consumoKgHembras * kcalAlH.Value, 3);
        double? prot = protAlH is null ? null : Math.Round(consumoKgHembras * protAlH.Value, 3);
        return (kcal, prot);
    }

    /// <summary>Semana de vida 1-based desde el encasetamiento (día 0-6 = semana 1).</summary>
    public static int CalcularSemana(DateTime fechaEncaset, DateTime fechaRegistro)
    {
        var dias = (fechaRegistro.Date - fechaEncaset.Date).TotalDays;
        return Math.Max(1, (int)Math.Floor(dias / 7.0) + 1);
    }
}
