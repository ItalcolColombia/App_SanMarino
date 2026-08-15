// src/ZooSanMarino.Application/Calculos/PesoLevanteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Peso corporal del reporte técnico de levante: conversión de unidad y diferencia contra la guía.
///
/// <para>
/// <b>Las dos fuentes hablan en GRAMOS.</b> <c>seguimiento_diario_levante.peso_prom_hembras</c>
/// guarda gramos (151 en la semana 1, 3.029 en la 24) y <c>produccion_avicola_raw.peso_h</c>
/// también (145, 2.915). La pantalla y el Excel, en cambio, muestran <b>kilos</b>: las cabeceras
/// dicen «kg Real» / «Guía kg» con tres decimales y el Excel apila Real sobre Guía en la misma
/// columna, así que las dos filas están obligadas a compartir unidad.
/// </para>
///
/// <para>
/// El reporte convertía a kilos SÓLO la guía y comparaba gramos contra kilos, así que el «%Dif
/// Peso» salía multiplicado por mil (S369A semana 1 ⇒ 104.037,93 %). La liquidación de cierre
/// nunca tuvo el problema porque compara gramos contra gramos.
/// </para>
///
/// <para>
/// Acá el porcentaje se calcula <b>en gramos contra gramos</b>: es invariante a la unidad (dividir
/// numerador y denominador por mil no lo cambia) y evita arrastrar el redondeo de la división.
/// </para>
/// </summary>
public static class PesoLevanteCalculos
{
    public const double GramosPorKilo = 1000d;

    /// <summary>
    /// Gramos → kilos, para la fila «Real» que se muestra junto a la guía.
    /// Sin peso registrado (0 o negativo) devuelve <c>null</c>, que es como el reporte marca
    /// «no hubo pesaje» — mismo guard <c>peso &gt; 0</c> que ya tenía el service.
    /// </summary>
    public static double? AKilos(double pesoGramos) =>
        pesoGramos > 0 ? pesoGramos / GramosPorKilo : null;

    /// <summary>
    /// Diferencia porcentual del peso real contra el de la guía. <b>Ambos en la misma unidad</b>
    /// (se usan los gramos crudos de una y otra fuente). Positivo = el lote pesa MÁS que la guía.
    /// Devuelve <c>null</c> si falta cualquiera de los dos, igual que antes.
    /// </summary>
    public static double? PorcDiferencia(double pesoRealGramos, double pesoGuiaGramos) =>
        pesoRealGramos > 0 && pesoGuiaGramos > 0
            ? (pesoRealGramos - pesoGuiaGramos) / pesoGuiaGramos * 100
            : null;

    /// <summary>
    /// <see cref="PorcDiferencia(double, double)"/> redondeada, para el armado que ya venía
    /// redondeando cada campo a 2 decimales.
    /// </summary>
    public static double? PorcDiferencia(double pesoRealGramos, double pesoGuiaGramos, int decimales) =>
        PorcDiferencia(pesoRealGramos, pesoGuiaGramos) is double d ? Math.Round(d, decimales) : null;
}
