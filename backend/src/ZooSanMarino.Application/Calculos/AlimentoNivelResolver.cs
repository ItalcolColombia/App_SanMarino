namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Resuelve (PURO, sin EF ni estado) si el ALIMENTO de una granja se maneja a nivel GALPÓN
/// (exige núcleo/galpón) o a nivel GRANJA. Jerarquía: la granja overridea a la empresa.
///
///   efectivo = farm.ManejaAlimentoPorGalpon ?? company.ManejaAlimentoPorGalpon
///
/// - Granja con override (<c>true</c>/<c>false</c>) → gana la granja.
/// - Granja sin override (<c>null</c>) → hereda el default de la empresa.
/// Reemplaza la decisión hardcodeada por país (Ecuador/Panamá=galpón, Colombia=granja):
/// ahora es dinámico por (empresa, granja). Solo aplica a alimento; otros conceptos van a nivel granja.
/// </summary>
public static class AlimentoNivelResolver
{
    /// <summary>
    /// Devuelve <c>true</c> si el alimento se maneja a nivel GALPÓN para esta granja.
    /// </summary>
    /// <param name="farmOverride">Override de la granja (null = hereda empresa).</param>
    /// <param name="companyDefault">Default global de la empresa.</param>
    public static bool ManejaPorGalpon(bool? farmOverride, bool companyDefault)
        => farmOverride ?? companyDefault;
}
