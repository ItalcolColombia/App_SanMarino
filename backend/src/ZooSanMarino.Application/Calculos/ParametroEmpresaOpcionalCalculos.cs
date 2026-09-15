// src/ZooSanMarino.Application/Calculos/ParametroEmpresaOpcionalCalculos.cs
// Resuelve un parámetro entero opcional editable desde Configuración → Empresas cuando el DTO de
// actualización usa `null` = "no lo mandó, conservá lo que había" (convención documentada en
// UpdateCompanyDto: evita que un formulario que solo envía datos de contacto apague en silencio un
// flag/parámetro que no toca).
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Con esa convención un formulario que SÍ administra el parámetro (Configuración → Empresas) no
/// tiene forma de pedir «bórralo»: vaciar el campo también manda `null`, que el update lee como «no
/// lo toques» y el valor viejo queda pegado para siempre.
/// <para>
/// La salida: <c>0</c> como sentinel de borrado explícito. Los dos parámetros que usan esto hoy
/// (<c>HuevoPrimeraPosturaHastaSemana</c>, <c>HuevosLevanteDesdeSemana</c>) son semanas de vida con
/// piso 1 (<c>Validators.min(1)</c> en el formulario) — <c>0</c> no es un valor real, así que sirve
/// de sentinel sin robarle ningún caso legítimo al rango.
/// </para>
/// </summary>
public static class ParametroEmpresaOpcionalCalculos
{
    /// <summary>
    /// <c>null</c> ⇒ el cliente no mandó el campo: conserva <paramref name="actual"/> (comportamiento
    /// de siempre). <c>0</c> ⇒ sentinel de borrado explícito: el parámetro vuelve a <c>null</c>
    /// ("la empresa no usa el concepto"). Cualquier otro entero reemplaza el valor tal cual.
    /// </summary>
    public static int? ResolverEnteroOpcional(int? nuevo, int? actual) => nuevo switch
    {
        null => actual,
        0 => null,
        var v => v
    };
}
