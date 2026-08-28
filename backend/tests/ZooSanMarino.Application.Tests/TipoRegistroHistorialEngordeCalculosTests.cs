using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El catálogo en C# es el ESPEJO EJECUTABLE del CHECK que vive en la BD
/// (<c>ck_hlpe_tipo_registro</c>, migración <c>20260828190000_AmpliaCheckHistorialEngordeAjusteEncaset</c>).
///
/// <para>
/// <b>Qué protegen estos tests.</b> El 21-ago-2026 se mergeó el código que audita las correcciones de
/// encasetamiento con <c>tipo_registro = 'AjusteEncaset'</c> sin la migración que amplía el CHECK: en
/// producción TODA edición de aves de un lote de engorde moría con SQLSTATE 23514 y el usuario sólo
/// veía «Alguno de los valores no cumple una regla de validación de la base de datos». En local no se
/// vio porque la copia local no tiene ni una constraint CHECK. Si mañana alguien agrega un quinto
/// valor en C# sin su migración, estos tests fallan en el gate de CI en vez de fallar en producción.
/// </para>
/// </summary>
public class TipoRegistroHistorialEngordeCalculosTests
{
    /// <summary>
    /// La lista congelada, copiada A MANO del SQL de la migración. Si este arreglo y
    /// <see cref="TipoRegistroHistorialEngordeCalculos.Catalogo"/> divergen es porque alguien tocó uno
    /// de los dos: el CHECK de la BD es el dueño y esto es su test.
    /// </summary>
    private static readonly string[] CatalogoDeLaMigracion =
        { "Inicio", "Ajuste", "AjusteResync", "AjusteEncaset" };

    [Fact]
    public void Catalogo_es_exactamente_el_de_la_migracion()
    {
        Assert.Equal(CatalogoDeLaMigracion, TipoRegistroHistorialEngordeCalculos.Catalogo);
    }

    [Theory]
    [InlineData("Inicio")]
    [InlineData("Ajuste")]
    [InlineData("AjusteResync")]
    [InlineData("AjusteEncaset")]
    public void Los_cuatro_valores_que_escribe_el_codigo_son_validos(string tipoRegistro)
    {
        Assert.True(TipoRegistroHistorialEngordeCalculos.EsValido(tipoRegistro));
    }

    [Fact]
    public void AjusteEncaset_es_valido_ese_es_el_bug_que_esta_migracion_arregla()
    {
        // Antes de 20260828190000 este valor no cabía en ck_hlpe_tipo_registro ⇒ 23514.
        Assert.Contains(TipoRegistroHistorialEngordeCalculos.AjusteEncaset,
                        TipoRegistroHistorialEngordeCalculos.Catalogo);
    }

    [Theory]
    [InlineData("inicio")]        // el CHECK de Postgres compara literales exactos
    [InlineData("AJUSTE")]
    [InlineData("Ajuste ")]       // con espacio al final tampoco entra
    [InlineData("AjusteEncasetamiento")]
    [InlineData("Creacion")]      // ese es el catálogo de historico_lote_postura, otra tabla
    [InlineData("")]
    [InlineData(null)]
    public void Fail_closed_cualquier_otro_valor_es_invalido(string? tipoRegistro)
    {
        Assert.False(TipoRegistroHistorialEngordeCalculos.EsValido(tipoRegistro));
    }

    [Theory]
    [InlineData("Ajuste", true)]           // descuento por aves fantasma: SÍ se resta
    [InlineData("Inicio", false)]          // es la base, no un descuento
    [InlineData("AjusteResync", false)]    // sustituye un descuento que faltó
    [InlineData("AjusteEncaset", false)]   // ya está dentro del Inicio corregido
    [InlineData("Desconocido", false)]
    [InlineData(null, false)]
    public void ParticipaEnConservacion_solo_el_ajuste_fantasma(string? tipoRegistro, bool esperado)
    {
        Assert.Equal(esperado, TipoRegistroHistorialEngordeCalculos.ParticipaEnConservacion(tipoRegistro));
    }

    [Theory]
    [InlineData("AjusteEncaset", true)]    // guarda el DELTA con signo: bajar el encaset audita negativo
    [InlineData("Inicio", false)]
    [InlineData("Ajuste", false)]
    [InlineData("AjusteResync", false)]
    [InlineData("ajusteencaset", false)]
    [InlineData(null, false)]
    public void AdmiteDeltaNegativo_espeja_el_check_relajado(string? tipoRegistro, bool esperado)
    {
        Assert.Equal(esperado, TipoRegistroHistorialEngordeCalculos.AdmiteDeltaNegativo(tipoRegistro));
    }

    [Fact]
    public void Todo_tipo_que_admite_delta_negativo_esta_en_el_catalogo()
    {
        // El CHECK relajado nombra 'AjusteEncaset' en su predicado: si no estuviera en el catálogo,
        // la constraint de tipo_registro lo rechazaría antes y la excepción sería inalcanzable.
        foreach (var tipo in TipoRegistroHistorialEngordeCalculos.Catalogo)
            if (TipoRegistroHistorialEngordeCalculos.AdmiteDeltaNegativo(tipo))
                Assert.True(TipoRegistroHistorialEngordeCalculos.EsValido(tipo));

        Assert.True(TipoRegistroHistorialEngordeCalculos.AdmiteDeltaNegativo(
            TipoRegistroHistorialEngordeCalculos.AjusteEncaset));
    }

    [Fact]
    public void Ningun_tipo_participa_en_la_conservacion_y_admite_delta_negativo_a_la_vez()
    {
        // Una fila que se resta en la conservación Y puede ser negativa sumaría aves al esperado:
        // el par de predicados tiene que ser disjunto.
        foreach (var tipo in TipoRegistroHistorialEngordeCalculos.Catalogo)
            Assert.False(TipoRegistroHistorialEngordeCalculos.ParticipaEnConservacion(tipo)
                      && TipoRegistroHistorialEngordeCalculos.AdmiteDeltaNegativo(tipo));
    }
}
