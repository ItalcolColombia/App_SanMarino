using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// La empresa activa la pide el cliente por cabecera, pero la decide el backend.
///
/// <para>
/// Estos tests fijan el invariante que se rompió hasta el 18-ago-2026:
/// <c>ICurrentUser.ActiveCompanyName</c> devolvía el header crudo y 44 servicios resolvían su alcance
/// con él, así que cambiar una cabecera bastaba para leer con el alcance de otra empresa (medido: un
/// usuario de Sanmarino —61 ítems de inventario— recibía los 152 de ItalcolEcuador).
/// </para>
/// </summary>
public class EmpresaActivaCalculosTests
{
    // ───────────────────────── PuedeUsarEmpresa ─────────────────────────

    [Fact]
    public void T1_ElMiembroPuedeUsarSuEmpresa()
    {
        Assert.True(EmpresaActivaCalculos.PuedeUsarEmpresa(esSuperAdmin: false, perteneceALaEmpresa: true));
    }

    [Fact]
    public void T2_ElQueNoPertenece_NoPuede()
    {
        // Es exactamente el caso de la fuga: sesión válida, empresa ajena.
        Assert.False(EmpresaActivaCalculos.PuedeUsarEmpresa(esSuperAdmin: false, perteneceALaEmpresa: false));
    }

    [Fact]
    public void T3_ElSuperAdminAtraviesaElAislamiento_aProposito()
    {
        Assert.True(EmpresaActivaCalculos.PuedeUsarEmpresa(esSuperAdmin: true, perteneceALaEmpresa: false));
    }

    // ───────────────────────── NombreConfiable ─────────────────────────

    [Fact]
    public void T4_SiElMiddlewareValido_eseEsElNombre()
    {
        Assert.Equal("ItalcolEcuador", EmpresaActivaCalculos.NombreConfiable("ItalcolEcuador"));
    }

    [Fact]
    public void T5_SinValidacion_NoHayNombre_FailClosed()
    {
        // El llamador cae a la empresa del token. NUNCA al header.
        Assert.Null(EmpresaActivaCalculos.NombreConfiable(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void T6_NombreEnBlanco_EsComoNoTenerNombre(string nombre)
    {
        Assert.Null(EmpresaActivaCalculos.NombreConfiable(nombre));
    }

    [Fact]
    public void T7_ElNombreSeNormaliza()
    {
        Assert.Equal("Demo", EmpresaActivaCalculos.NombreConfiable("  Demo  "));
    }

    // ───────────────── IdDeLaEmpresaActivaSiCoincide ─────────────────

    [Fact]
    public void T8_SiElNombrePedidoEsElDeLaEmpresaActiva_SeUsaSuId()
    {
        Assert.Equal(3, EmpresaActivaCalculos.IdDeLaEmpresaActivaSiCoincide("ItalcolEcuador", "ItalcolEcuador", 3));
    }

    [Theory]
    [InlineData("italcolecuador")]
    [InlineData("  ItalcolEcuador  ")]
    [InlineData("ITALCOLECUADOR")]
    public void T9_LaCoincidenciaIgnoraMayusculasYEspacios(string pedido)
    {
        // El resolver histórico compara con ILIKE; el atajo no puede ser más estricto que él.
        Assert.Equal(3, EmpresaActivaCalculos.IdDeLaEmpresaActivaSiCoincide(pedido, "ItalcolEcuador", 3));
    }

    [Fact]
    public void T10_OtroNombre_LoResuelveQuienPregunto()
    {
        Assert.Null(EmpresaActivaCalculos.IdDeLaEmpresaActivaSiCoincide("Demo", "ItalcolEcuador", 3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void T11_SinEmpresaActivaValida_NoHayAtajo(int idActivo)
    {
        Assert.Null(EmpresaActivaCalculos.IdDeLaEmpresaActivaSiCoincide("Demo", "Demo", idActivo));
    }

    [Fact]
    public void T12_SinNombreActivo_NoHayAtajo()
    {
        // Sin validación no hay nombre activo ⇒ el atajo no puede inventar una empresa.
        Assert.Null(EmpresaActivaCalculos.IdDeLaEmpresaActivaSiCoincide("Demo", null, 4));
    }
}
