using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Movimientos de huevo clasificados POR ÍTEM del catálogo (empresas con
/// <c>clasificacion_huevo_por_items</c>). Lo que estos tests blindan es la CLAVE de idempotencia:
/// con las 11 categorías en cero, la clave por categorías haría colisionar dos movimientos distintos
/// del mismo día y el segundo se omitiría como «repetido».
/// </summary>
public class MigracionMovimientosHuevosPorItemTests
{
    private static readonly DateTime Dia = new(2026, 9, 1);

    private static string Clave(MovimientoHuevosMigracion tipo, params (int, int)[] items) =>
        MigracionMovimientosHuevosCalculos.ClaveArchivoPorItems(Dia, tipo, items);

    [Fact]
    public void DosMovimientosDistintosDelMismoDiaNoColisionan()
    {
        // Es el caso que la clave por categorías no distinguía: con las 11 en cero, ambas rendían
        // "2026-09-01|Traslado|0|0|0|0|0|0|0|0|0|0|0".
        var a = Clave(MovimientoHuevosMigracion.Traslado, (656, 2400));
        var b = Clave(MovimientoHuevosMigracion.Traslado, (666, 150));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ElOrdenDeLasFilasNoCambiaLaClave()
    {
        // El mismo movimiento escrito con los ítems en otro orden tiene que rendir la MISMA clave, o
        // un reimport no lo reconocería y lo volvería a aplicar.
        var a = Clave(MovimientoHuevosMigracion.Venta, (656, 2400), (666, 150));
        var b = Clave(MovimientoHuevosMigracion.Venta, (666, 150), (656, 2400));

        Assert.Equal(a, b);
    }

    [Fact]
    public void LasCantidadesEnCeroNoParticipanDeLaClave()
    {
        var conCero = Clave(MovimientoHuevosMigracion.Traslado, (656, 2400), (666, 0));
        var sinItem = Clave(MovimientoHuevosMigracion.Traslado, (656, 2400));

        Assert.Equal(sinItem, conCero);
    }

    [Fact]
    public void ElTipoDeOperacionFormaParteDeLaClave()
    {
        Assert.NotEqual(
            Clave(MovimientoHuevosMigracion.Traslado, (656, 2400)),
            Clave(MovimientoHuevosMigracion.Venta, (656, 2400)));
    }

    [Fact]
    public void LaFechaFormaParteDeLaClave()
    {
        var items = new[] { (656, 2400) };
        Assert.NotEqual(
            MigracionMovimientosHuevosCalculos.ClaveArchivoPorItems(Dia, MovimientoHuevosMigracion.Traslado, items),
            MigracionMovimientosHuevosCalculos.ClaveArchivoPorItems(Dia.AddDays(1), MovimientoHuevosMigracion.Traslado, items));
    }

    [Fact]
    public void UnaCantidadDistintaCambiaLaClave()
    {
        Assert.NotEqual(
            Clave(MovimientoHuevosMigracion.Traslado, (656, 2400)),
            Clave(MovimientoHuevosMigracion.Traslado, (656, 2401)));
    }

    [Fact]
    public void LaClavePorItemsNoSeConfundeConLaDeCategorias()
    {
        // Marcador "items" en el string: un movimiento por ítems y uno por categorías del mismo día y
        // tipo no pueden compartir clave aunque los dos sumen lo mismo.
        var porItems = Clave(MovimientoHuevosMigracion.Traslado, (656, 2400));
        var porCategorias = MigracionMovimientosHuevosCalculos.ClaveArchivo(
            Dia, MovimientoHuevosMigracion.Traslado, HuevosClasificacion.Cero with { Limpio = 2400 });

        Assert.NotEqual(porItems, porCategorias);
    }

    // ── El esquema de la hoja ────────────────────────────────────────────────────────────────────

    [Fact]
    public void LaHojaPorItemSeLlamaIgualQueLaDeCategorias()
    {
        // Es la MISMA hoja del archivo: cambia su forma, no su nombre. Si difirieran, el importador
        // buscaría una hoja que la plantilla no emite.
        Assert.Equal(
            MigracionEsquemas.MovimientosHuevosProduccion.Hoja,
            MigracionEsquemas.MovimientosHuevosPorItem.Hoja);
    }

    [Fact]
    public void LaHojaPorItemPideFechaTipoItemYCantidad()
    {
        var requeridas = MigracionEsquemas.MovimientosHuevosPorItem.Columnas
            .Where(c => c.Requerida).Select(c => c.Titulo).ToArray();

        Assert.Equal(new[] { "Fecha", "Tipo", "Ítem", "Cantidad" }, requeridas);
    }

    [Fact]
    public void LaHojaPorItemNoTraeNingunaDeLas11Categorias()
    {
        var titulos = MigracionEsquemas.MovimientosHuevosPorItem.Columnas.Select(c => c.Titulo).ToList();
        Assert.DoesNotContain(titulos, t => t.StartsWith("Huevo ", StringComparison.Ordinal));
    }

    [Fact]
    public void LasDosVariantesComparteLasColumnasDeCabecera()
    {
        // Fecha, Tipo y los campos del destino se leen igual en las dos formas: un usuario que pasa
        // de una empresa a otra no tiene que reaprender la cabecera.
        var comunes = new[] { "Fecha", "Tipo", "Tipo Destino", "Destino", "Motivo", "Descripción", "Observaciones" };
        var porItem = MigracionEsquemas.MovimientosHuevosPorItem.Columnas.Select(c => c.Titulo).ToHashSet();
        var porCategoria = MigracionEsquemas.MovimientosHuevosProduccion.Columnas.Select(c => c.Titulo).ToHashSet();

        foreach (var titulo in comunes)
        {
            Assert.Contains(titulo, porItem);
            Assert.Contains(titulo, porCategoria);
        }
    }
}
