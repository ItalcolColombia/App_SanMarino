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

    /// <summary>
    /// Mensaje de rechazo si el ítem de primera postura ya no está vigente a esa edad, o <c>null</c>
    /// si sí lo está (o si no hay regla que aplicar).
    ///
    /// <para>
    /// <b>D5 — por qué esto existe desde el 21-ago-2026.</b> Hasta acá <see cref="EsVigente"/> no
    /// tenía UN SOLO llamador en <c>backend/src</c>: la vigencia era 100 % UI (un <c>[disabled]</c>
    /// en el <c>&lt;option&gt;</c>). Y como la fecha del registro es EDITABLE dentro del mismo
    /// modal, alcanzaba con elegir el ítem con fecha de semana 21 y corregir la fecha a semana 30:
    /// la opción quedaba elegida y el guardado la aceptaba. O sea que la regla del cliente —«desde
    /// el primer día de la semana 23 no usa más el ítem de primera postura»— no se cumplía por
    /// ningún lado salvo la buena voluntad del operario.
    /// </para>
    ///
    /// <para>
    /// Sigue siendo fail-open donde no hay regla: sin límite configurado (toda empresa que no sea
    /// Santa Reyes) o sin semana de vida calculable (lote sin fecha de encaset), no rechaza nada.
    /// </para>
    /// </summary>
    /// <param name="hastaSemana"><c>Company.HuevoPrimeraPosturaHastaSemana</c>.</param>
    /// <param name="semanaVida">Semana de vida del lote a la fecha del registro (1-based).</param>
    /// <param name="nombreItem">Nombre legible del ítem, para que el mensaje diga cuál sobra.</param>
    public static string? MensajeFueraDeVigencia(int? hastaSemana, int? semanaVida, string nombreItem)
    {
        if (EsVigente(hastaSemana, semanaVida)) return null;

        return $"El ítem «{nombreItem}» es de primera postura y solo se puede registrar hasta la " +
               $"semana {hastaSemana} de vida del lote. El registro es de la semana {semanaVida}.";
    }

    /// <summary>
    /// Clave de <c>catalogo_items.metadata</c> que marca a un ítem como «huevo de primera postura».
    /// El catálogo la escribe en camelCase; el lector tolera además snake_case (ver el espejo del
    /// front, <c>items-huevo-catalogo.funcion.ts</c>).
    /// </summary>
    public const string MetadataKeyPrimeraPostura = "primeraPostura";

    /// <summary>Variante snake_case de <see cref="MetadataKeyPrimeraPostura"/>, tolerada al leer.</summary>
    public const string MetadataKeyPrimeraPosturaSnake = "primera_postura";
}
