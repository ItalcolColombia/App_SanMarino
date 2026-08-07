// src/ZooSanMarino.Application/Calculos/ReporteContableResumenCalculos.cs
// Cálculo PURO del acumulado de la hoja RESUMEN del Reporte Contable.
// Sin EF, sin EPPlus, sin estado: recibe las semanas del reporte y devuelve la fila de totales.
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Consolidación de la hoja RESUMEN del informe contable. El Excel solo pinta lo que esta clase
/// calcula, de modo que la fila «TOTAL GENERAL» tenga una sola fórmula y sea testeable sin abrir
/// un libro.
/// </summary>
public static class ReporteContableResumenCalculos
{
    /// <summary>
    /// Fila del resumen semanal: los conteos de aves y los consumos de una semana contable, ya
    /// sumados hembras + machos. Es la unidad que pinta el Excel y también la que se acumula.
    /// </summary>
    public readonly record struct FilaResumen(
        int Semana,
        DateTime FechaInicio,
        DateTime FechaFin,
        int Mortalidad,
        int Seleccion,
        int Traslados,
        int Ventas,
        decimal Alimento,
        decimal Agua,
        decimal Medicamento,
        decimal Vacuna,
        decimal Otros,
        decimal TotalGeneral);

    /// <summary>
    /// Proyecta una semana del reporte a su fila de resumen, sumando hembras + machos en los
    /// conteos de aves. <c>Selección</c> se toma de <c>SeleccionHembrasSemanal +
    /// SeleccionMachosSemanal</c>, el mismo par que ya escribe la sección AVES de la hoja semanal.
    /// </summary>
    public static FilaResumen AFila(ReporteContableSemanalDto s) => new(
        Semana: s.SemanaContable,
        FechaInicio: s.FechaInicio,
        FechaFin: s.FechaFin,
        Mortalidad: s.MortalidadHembrasSemanal + s.MortalidadMachosSemanal,
        Seleccion: s.SeleccionHembrasSemanal + s.SeleccionMachosSemanal,
        Traslados: s.TrasladosHembrasSemanal + s.TrasladosMachosSemanal,
        Ventas: s.VentasHembrasSemanal + s.VentasMachosSemanal,
        Alimento: s.ConsumoTotalAlimento,
        Agua: s.ConsumoTotalAgua,
        Medicamento: s.ConsumoTotalMedicamento,
        Vacuna: s.ConsumoTotalVacuna,
        Otros: s.OtrosConsumos,
        TotalGeneral: s.TotalGeneral);

    /// <summary>
    /// Las semanas del reporte ordenadas por semana contable y proyectadas a filas de resumen.
    /// </summary>
    public static List<FilaResumen> Filas(IEnumerable<ReporteContableSemanalDto> semanas) =>
        semanas.OrderBy(s => s.SemanaContable).Select(AFila).ToList();

    /// <summary>
    /// Fila «TOTAL GENERAL»: suma columna a columna. Sobre una lista vacía devuelve todo en cero
    /// (semana 0 y fechas <c>default</c>), que es lo que el Excel debe pintar cuando el lote no
    /// tiene semanas en el período.
    /// </summary>
    public static FilaResumen Total(IEnumerable<FilaResumen> filas)
    {
        var total = new FilaResumen();
        foreach (var f in filas)
        {
            total = total with
            {
                Mortalidad = total.Mortalidad + f.Mortalidad,
                Seleccion = total.Seleccion + f.Seleccion,
                Traslados = total.Traslados + f.Traslados,
                Ventas = total.Ventas + f.Ventas,
                Alimento = total.Alimento + f.Alimento,
                Agua = total.Agua + f.Agua,
                Medicamento = total.Medicamento + f.Medicamento,
                Vacuna = total.Vacuna + f.Vacuna,
                Otros = total.Otros + f.Otros,
                TotalGeneral = total.TotalGeneral + f.TotalGeneral
            };
        }
        return total;
    }
}
