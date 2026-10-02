// src/ZooSanMarino.Application/Calculos/LevanteSemanaLimiteCalculos.cs
// Límite de semanas del seguimiento diario de LEVANTE, configurable por empresa
// (companies.levante_hasta_semana; plan fase_de_desarrollo/levante_hasta_semana_por_empresa_plan.md).
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Hasta qué semana de vida del lote se admiten registros de seguimiento diario de levante.
/// <para>
/// Con el límite N, el último día permitido es el último día de la semana N (<c>encaset + N·7 − 1</c>,
/// con la semana canónica <see cref="HuevosLevanteCalculos.SemanaVida"/>: el día del encaset es la
/// semana 1). Desde la semana N+1 el registro se rechaza: el lote tiene que cerrarse y seguir en
/// producción.
/// </para>
/// <para>
/// <c>null</c> = la empresa no configura límite ⇒ todo pasa, idéntico a antes. Sin fecha de encaset no
/// hay semana evaluable y también pasa (mismo criterio que el gate de huevos de levante: bloquear ahí
/// dejaría un 400 sin remedio).
/// </para>
/// </summary>
public static class LevanteSemanaLimiteCalculos
{
    /// <summary>¿Se admite un registro de levante con esta fecha?</summary>
    public static bool PermiteRegistro(DateTime? fechaEncaset, DateTime fechaRegistro, int? hastaSemana)
    {
        if (hastaSemana is null || !fechaEncaset.HasValue) return true;
        return HuevosLevanteCalculos.SemanaVida(fechaRegistro, fechaEncaset.Value) <= hastaSemana.Value;
    }

    /// <summary>Último día permitido: el último día de la semana <paramref name="hastaSemana"/>.</summary>
    public static DateTime FechaLimite(DateTime fechaEncaset, int hastaSemana) =>
        fechaEncaset.Date.AddDays(hastaSemana * 7 - 1);

    /// <summary>Mensaje del rechazo (cita límite, semana del registro y último día).</summary>
    public static string Mensaje(int hastaSemana, DateTime fechaEncaset, DateTime fechaRegistro) =>
        $"El levante de esta empresa se registra hasta la semana {hastaSemana} de vida del lote " +
        $"(último día: {FechaLimite(fechaEncaset, hastaSemana):dd/MM/yyyy}); este registro es de la semana " +
        $"{HuevosLevanteCalculos.SemanaVida(fechaRegistro, fechaEncaset)}. Cierre el levante del lote para continuar en producción.";
}
