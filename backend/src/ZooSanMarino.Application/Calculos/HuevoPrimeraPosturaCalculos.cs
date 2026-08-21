// src/ZooSanMarino.Application/Calculos/HuevoPrimeraPosturaCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Vigencia del ítem «Huevo de primera postura» (Santa Reyes): se ofrece en la clasificación por
/// ítems solo hasta el último día de <c>Company.HuevoPrimeraPosturaHastaSemana</c> semanas de edad
/// del lote (edad global desde encasetamiento, misma convención que <see cref="SemanasCicloPosturaCalculos"/>
/// y la guía genética por <c>Edad</c>); desde la semana siguiente el ítem deja de estar disponible.
///
/// <para>
/// Fuente: <c>Requerimientos de Italapp.docx</c> (sección «Producción de huevos», 18-ago-2026) —
/// "Se necesita que cuando se cree un lote poder especificar los huevos que va a producir en la
/// etapa de producción, mostrar primera postura hasta el último día de la semana 22, desde el
/// primer día de la semana 23 no usa más el ítem de primera postura."
/// </para>
///
/// <para>
/// Espejo de frontend: <c>items-huevo-catalogo.funcion.ts</c> (<c>esVigentePrimeraPostura</c>).
/// Este es el cálculo UI-only (qué ítems ofrecer en el selector); no rechaza en el guardado — mismo
/// criterio que el resto de la familia de flags de Santa Reyes (p. ej. <c>OcultaMachosEnPostura</c>),
/// que también son "solo UI".
/// </para>
/// </summary>
public static class HuevoPrimeraPosturaCalculos
{
    /// <summary>
    /// ¿El ítem de primera postura sigue vigente a esta edad? Fail-open (vigente) cuando falta el
    /// límite configurado (empresa sin <c>HuevoPrimeraPosturaHastaSemana</c>, es decir, todas salvo
    /// Santa Reyes) o cuando no hay semana de vida calculable (sin fecha de encaset todavía) — en
    /// ninguno de los dos casos hay una regla que aplicar, así que no se oculta nada.
    /// </summary>
    public static bool EsVigente(int? hastaSemana, int? semanaVida)
    {
        if (hastaSemana is null) return true;
        if (semanaVida is null) return true;
        return semanaVida.Value <= hastaSemana.Value;
    }
}
