using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.GuiaGeneticaRequisitoCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Guía genética CONDICIONAL al crear/editar lote (Santa Reyes §8):
///  - empresa SIN guía cargada → raza/año opcionales (raza texto libre, año nulo) y sin
///    verificación de existencia;
///  - empresa CON guía → comportamiento actual intacto: requeridos + verificar existencia en BD.
/// </summary>
public class GuiaGeneticaRequisitoCalculosTests
{
    // ── 1) Empresa sin guía + raza null + año null → válido (no exige) ───────────
    [Fact]
    public void SinGuia_RazaNullYAnioNull_EsValido()
    {
        Assert.False(DebeExigirGuia(companyTieneGuia: false));
        Assert.Null(ValidarSeleccion(companyTieneGuia: false, raza: null, anoTablaGenetica: null));
        // Sin guía nunca se va a la BD a verificar existencia.
        Assert.False(DebeVerificarExistenciaEnGuia(companyTieneGuia: false, raza: null, anoTablaGenetica: null));
    }

    // ── 2) Empresa sin guía + raza texto libre + año null → válido ───────────────
    [Theory]
    [InlineData("BABCOK BROWN")]
    [InlineData("LOHMANN LSL")]
    [InlineData("raza que no existe en ninguna guía")]
    public void SinGuia_RazaTextoLibreYAnioNull_EsValido(string raza)
    {
        Assert.Null(ValidarSeleccion(companyTieneGuia: false, raza: raza, anoTablaGenetica: null));
        Assert.False(DebeVerificarExistenciaEnGuia(companyTieneGuia: false, raza: raza, anoTablaGenetica: null));
    }

    [Theory]
    [InlineData("  HY LINE  ", "HY LINE")]   // se guarda con trim
    [InlineData("   ", null)]                 // solo espacios → null
    [InlineData(null, null)]                  // null → null
    public void SinGuia_RazaSeGuardaComoTextoLibreNormalizado(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, ResolverRazaAGuardar(companyTieneGuia: false, raza: entrada));
    }

    [Fact]
    public void ConGuia_RazaSeGuardaTalCual_ComportamientoActualIntacto()
    {
        // Con guía cargada NO se toca el valor recibido (byte a byte como hoy).
        Assert.Equal("  ROSS 308  ", ResolverRazaAGuardar(companyTieneGuia: true, raza: "  ROSS 308  "));
    }

    // ── 3) Empresa con guía + raza null → inválido ("requeridos") ────────────────
    [Fact]
    public void ConGuia_RazaNull_DevuelveMensajeRequeridos()
    {
        var error = ValidarSeleccion(companyTieneGuia: true, raza: null, anoTablaGenetica: 2023);

        Assert.Equal(
            "Raza y Año de tabla genética son requeridos y deben existir en la guía genética cargada.",
            error);
        Assert.Equal(MensajeRequeridos, error);
        // Al fallar los requeridos no tiene sentido consultar la BD.
        Assert.False(DebeVerificarExistenciaEnGuia(companyTieneGuia: true, raza: null, anoTablaGenetica: 2023));
    }

    [Theory]
    [InlineData("", 2023)]          // raza vacía
    [InlineData("   ", 2023)]       // raza en blanco
    [InlineData("ROSS 308", null)]  // año ausente
    [InlineData("ROSS 308", 0)]     // año 0 (no positivo)
    [InlineData("ROSS 308", -1)]    // año negativo
    public void ConGuia_FaltantesOAnioNoPositivo_DevuelveMensajeRequeridos(string? raza, int? anio)
    {
        Assert.Equal(MensajeRequeridos, ValidarSeleccion(companyTieneGuia: true, raza, anio));
        Assert.False(DebeVerificarExistenciaEnGuia(companyTieneGuia: true, raza, anio));
    }

    // ── 4) Empresa con guía + raza/año presentes → hay que verificar existencia ──
    // (el AnyAsync vive en LoteService; la calc solo decide que se debe verificar y
    //  construye el mensaje exacto cuando la combinación no existe)
    [Fact]
    public void ConGuia_RazaYAnioPresentes_ExigeVerificarExistenciaEnBd()
    {
        Assert.True(DebeExigirGuia(companyTieneGuia: true));
        Assert.True(DebeVerificarExistenciaEnGuia(companyTieneGuia: true, raza: "ROSS 308", anoTablaGenetica: 2023));
    }

    [Fact]
    public void MensajeGuiaInexistente_ConservaElTextoActual()
    {
        Assert.Equal(
            "No existe guía genética para la raza 'ROSS 308' y el año '2023' en la compañía actual. Cargue la guía genética primero.",
            MensajeGuiaInexistente("ROSS 308", 2023));
    }

    // ── 5) Empresa con guía + combinación completa → sin error de requeridos ─────
    [Fact]
    public void ConGuia_CombinacionCompleta_NoHayErrorDeRequeridos()
    {
        Assert.Null(ValidarSeleccion(companyTieneGuia: true, raza: "LOHMANN BROWN", anoTablaGenetica: 2024));
    }
}
