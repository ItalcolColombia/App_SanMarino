using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El historial de traslados de lote mostraba el literal <c>"Usuario ID: 12345"</c> en la columna
/// Usuario. El puente real es que ese entero es la <b>cedula</b> del usuario (<c>users.id</c> es
/// <c>Guid</c>, asi que no hay FK). Estos tests fijan el mapeo y, sobre todo, que lo que no matchea
/// devuelva <c>null</c> —que la pantalla pinta como guion— y no reviente ni invente un nombre.
/// </summary>
public class HistorialTrasladoLoteCalculosTests
{
    [Fact]
    public void NombresPorCedula_CedulaNumerica_ComponeNombreYApellido()
    {
        var mapa = HistorialTrasladoLoteCalculos.NombresPorCedula(new (string?, string?, string?)[]
        {
            ("1020304050", "Carolina", "Perez")
        });

        Assert.Equal("Carolina Perez", mapa[1020304050]);
    }

    [Fact]
    public void NombresPorCedula_CedulaNoNumerica_SeIgnoraSinReventar()
    {
        // Existen cedulas alfanumericas (pasaportes, extranjeros). No pueden ser el int del
        // historial, asi que se descartan en vez de tirar FormatException.
        var mapa = HistorialTrasladoLoteCalculos.NombresPorCedula(new (string?, string?, string?)[]
        {
            ("AB-1234", "Jhon", "Doe"),
            ("777", "Ana", "Gomez")
        });

        Assert.Single(mapa);
        Assert.Equal("Ana Gomez", mapa[777]);
    }

    [Fact]
    public void NombresPorCedula_NombreEnBlanco_NoEntraAlMapa()
    {
        // Un mapa con " " haria que la UI muestre un espacio en vez de su guion.
        var mapa = HistorialTrasladoLoteCalculos.NombresPorCedula(new (string?, string?, string?)[]
        {
            ("555", "   ", null)
        });

        Assert.Empty(mapa);
        Assert.Null(HistorialTrasladoLoteCalculos.ResolverNombre(mapa, 555));
    }

    [Fact]
    public void NombresPorCedula_CedulaRepetida_UnaSolaEntrada()
    {
        var mapa = HistorialTrasladoLoteCalculos.NombresPorCedula(new (string?, string?, string?)[]
        {
            ("999", "Luis", "Uno"),
            ("999", "Luis", "Dos")
        });

        Assert.Single(mapa);
    }

    [Fact]
    public void NombresPorCedula_SinUsuarios_DevuelveMapaVacio()
    {
        Assert.Empty(HistorialTrasladoLoteCalculos.NombresPorCedula(
            Array.Empty<(string?, string?, string?)>()));
    }

    [Theory]
    [InlineData(0)]          // fn_mover_lote escribe COALESCE(p_user_id, 0): jamas matchea
    [InlineData(968091594)]  // hash del id de usuario, no una cedula (caso visto en tickets)
    public void CedulasAConsultar_DescartaElCeroYNoDuplica(int idExtra)
    {
        var cedulas = HistorialTrasladoLoteCalculos.CedulasAConsultar(new[] { 111, 111, 0, idExtra });

        Assert.DoesNotContain("0", cedulas);
        Assert.Single(cedulas, c => c == "111");
    }

    [Fact]
    public void ResolverNombre_IdSinUsuario_DevuelveNull()
    {
        var mapa = HistorialTrasladoLoteCalculos.NombresPorCedula(new (string?, string?, string?)[]
        {
            ("111", "Ana", "Gomez")
        });

        Assert.Null(HistorialTrasladoLoteCalculos.ResolverNombre(mapa, 222));
    }
}
