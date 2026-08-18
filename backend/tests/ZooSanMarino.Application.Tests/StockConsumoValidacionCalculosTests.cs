using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.StockConsumoValidacionCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del rechazo por falta de stock al capturar un consumo.
///
/// <para>
/// Lo que fijan estos tests es el MENSAJE, no solo la decisión, porque el mensaje es el arreglo: hasta
/// ahora Ecuador y Panamá guardaban el día y se comían el rechazo en un <c>catch</c>, así que la
/// persona nunca se enteraba de que su consumo no había movido un kilo. Un texto que no dice qué ítem
/// falta ni cuánto hay deja el problema igual de invisible.
/// </para>
/// </summary>
public class StockConsumoValidacionCalculosTests
{
    private static IReadOnlyDictionary<int, decimal> Stock(params (int Id, decimal Kg)[] filas) =>
        filas.ToDictionary(f => f.Id, f => f.Kg);

    [Fact]
    public void Con_stock_suficiente_no_hay_motivo()
    {
        var motivo = MotivoStockInsuficiente(
            new[] { new ItemPedido(208, "Alimento ERP", 750m) },
            Stock((208, 1900m)));

        Assert.Null(motivo);
    }

    [Fact]
    public void El_stock_justo_alcanza()
    {
        // Consumir exactamente lo que hay es válido: deja el saldo en cero, no en negativo.
        Assert.Null(MotivoStockInsuficiente(
            new[] { new ItemPedido(208, "Alimento ERP", 750m) }, Stock((208, 750m))));
    }

    [Fact]
    public void Sin_fila_de_stock_el_mensaje_lo_dice_asi_y_no_como_saldo_cero()
    {
        // «No tiene stock registrado» y «hay 0 kg» se resuelven distinto: lo primero suele ser el
        // alimento equivocado, lo segundo un ingreso que falta cargar.
        var motivo = MotivoStockInsuficiente(
            new[] { new ItemPedido(412, "Alimento Postura", 100m) },
            Stock((208, 5000m)));

        Assert.NotNull(motivo);
        Assert.Contains("«Alimento Postura» no tiene stock registrado", motivo);
        Assert.Contains("se piden 100 kg", motivo);
    }

    [Fact]
    public void Con_stock_insuficiente_el_mensaje_dice_cuanto_se_pide_y_cuanto_hay()
    {
        var motivo = MotivoStockInsuficiente(
            new[] { new ItemPedido(208, "Alimento ERP", 750m) },
            Stock((208, 120m)),
            ubicacion: "el galpón G0490");

        Assert.NotNull(motivo);
        Assert.Contains("el galpón G0490", motivo);
        Assert.Contains("se piden 750 kg y hay 120 kg", motivo);
    }

    [Fact]
    public void Con_varios_items_el_mensaje_senala_cual_falla()
    {
        // El que alcanza no puede aparecer en el mensaje: si el texto los nombra a todos, la persona
        // no sabe cuál corregir.
        var motivo = MotivoStockInsuficiente(
            new[]
            {
                new ItemPedido(208, "Alimento ERP", 100m),
                new ItemPedido(412, "Alimento Postura", 900m),
            },
            Stock((208, 5000m), (412, 300m)));

        Assert.NotNull(motivo);
        Assert.Contains("Alimento Postura", motivo);
        Assert.DoesNotContain("Alimento ERP", motivo);
    }

    [Fact]
    public void Las_cantidades_no_positivas_no_se_validan()
    {
        // Una línea en cero (o negativa, que en una edición es una devolución) no consume nada, así
        // que no puede bloquear el guardado por falta de stock.
        Assert.Null(MotivoStockInsuficiente(
            new[] { new ItemPedido(208, "Alimento ERP", 0m), new ItemPedido(412, "Otro", -50m) },
            Stock()));
    }

    [Fact]
    public void Sin_nombre_el_mensaje_cae_al_id_en_vez_de_quedar_vacio()
    {
        var motivo = MotivoStockInsuficiente(
            new[] { new ItemPedido(777, null, 10m) }, Stock());

        Assert.NotNull(motivo);
        Assert.Contains("el ítem #777", motivo);
    }

    [Fact]
    public void Los_kilos_se_escriben_igual_desde_cualquier_cultura()
    {
        // El separador decimal no puede depender del servidor: el mensaje se compara en tests y lo
        // lee gente en tres países.
        var anterior = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-AR");
            var motivo = MotivoStockInsuficiente(
                new[] { new ItemPedido(208, "Alimento ERP", 12.5m) }, Stock((208, 1.25m)));

            Assert.Contains("se piden 12.5 kg y hay 1.25 kg", motivo);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = anterior;
        }
    }
}
