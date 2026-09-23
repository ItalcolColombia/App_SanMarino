using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del backfill de <c>item_inventario</c> para Santa Reyes (`fase_de_desarrollo/catalogo_items_santa_reyes_erp_plan.md`):
/// el Excel de insumos no trae columna de unidad, así que se infiere del texto de "Desc. item".
/// Casos tomados de descripciones REALES del catálogo de Santa Reyes (verificados contra la BD).
/// </summary>
public class InferenciaUnidadInventarioCalculosTests
{
    [Theory]
    [InlineData("AVIYODOX GALON X 20 LITROS", "l")]
    [InlineData("ACEITE MOTOR MOBIL 2 TIEMPOS BOT.X 946ML", "ml")]
    [InlineData("ARENA CUARZO CRISTALIZADA SACO X 50 KG", "kg")]
    [InlineData("SULFATO COBRE AZUL CRISTALES SACO X 25KG", "kg")]
    [InlineData("ACTIVATECH X SOBRE DE 250 GR 5%", "g")]
    [InlineData("ACEITE HIDRAULICO IBC ISO-68 GARR.X 5 GL", "gal")]
    [InlineData("ACEITE PREMIUM REDUCTOR F3651-460 X5GLN", "gal")]
    [InlineData("A.C.P.M X GALON EXC. IVA", "gal")]
    [InlineData("AVISAN SECURE (SALMONELOSIS) X 1000 DS", "dosis")]
    [InlineData("BANDEJA CARTON HUEVOS AZUL X 30 UN", "und")]
    [InlineData("AGUJA DE 20G X 1/2 X UND", "und")]
    [InlineData("CINTA ADH TRANSP DE 5 CM ROLLO X 100 MT", "und")]
    [InlineData("GAS X CILINDRO DE 100 LBS", "lb")]
    public void Infiere_por_texto_cuando_hay_token_reconocible(string descripcion, string esperada) =>
        Assert.Equal(esperada, InferenciaUnidadInventarioCalculos.Inferir(descripcion, tipoItem: "insumo"));

    [Theory]
    [InlineData("MAREK RISPENS X 2000(CEVAC MD RISPENS)", "vacuna", "dosis")]
    [InlineData("NEW CASTLE +BRONQ MA5 CLONE 30 X2500", "vacuna", "dosis")]
    [InlineData("GASOLINA CORRIENTE", "combustible", "gal")]
    [InlineData("GRASA PARA GUADAÑA/LITIO #2", "combustible", "gal")]
    [InlineData("EMPAQUE DE POLIPROPILENO", "empaque", "und")]
    [InlineData("CADENA COMEDERO CHAMPION S.ALIMENTACION", "mantenimiento", "und")]
    [InlineData("BICARBONATO DE SODIO 99% Na27% IVA 19%", "materia_prima", "kg")]
    [InlineData("VENOCLISES (MACROGOTEO SENCILLO)", "insumo", "und")]
    public void Sin_token_en_el_texto_cae_al_default_del_tipo_item(string descripcion, string tipoItem, string esperada) =>
        Assert.Equal(esperada, InferenciaUnidadInventarioCalculos.Inferir(descripcion, tipoItem));

    [Fact]
    public void Sin_token_y_sin_tipo_item_conocido_cae_a_und() =>
        Assert.Equal("und", InferenciaUnidadInventarioCalculos.Inferir("ALGO SIN PISTA DE UNIDAD", tipoItem: "otro"));

    [Fact]
    public void Descripcion_nula_no_revienta_y_usa_el_default_del_tipo() =>
        Assert.Equal("l", InferenciaUnidadInventarioCalculos.Inferir(null, tipoItem: "desinfectante"));

    [Fact]
    public void No_confunde_una_palabra_que_contiene_el_token_como_substring() =>
        // "GRAVA" contiene "GR" pero no es una unidad de gramos.
        Assert.Equal("kg", InferenciaUnidadInventarioCalculos.Inferir("GRAVA DE CUARZO SACO X 50 KG", tipoItem: "insumo"));

    [Fact]
    public void Toma_el_token_mas_a_la_derecha_cuando_hay_varios() =>
        // "GALON" en el medio, "LITROS" al final: gana el de más a la derecha.
        Assert.Equal("l", InferenciaUnidadInventarioCalculos.Inferir("AVIYODOX GALON X 20 LITROS", tipoItem: "insumo"));
}
