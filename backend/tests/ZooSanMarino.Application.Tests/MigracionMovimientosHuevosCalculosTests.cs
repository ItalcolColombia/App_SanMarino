using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de las reglas puras de la hoja "Movimientos Huevos" de la carga masiva de producción:
/// interpretación de la operación, tipo de destino efectivo y clave de duplicado.
/// </summary>
public class MigracionMovimientosHuevosCalculosTests
{
    private static readonly HuevosClasificacion Cantidades = new(
        Limpio: 2000, Tratado: 300, Sucio: 0, Deforme: 0, Blanco: 0, DobleYema: 0,
        Piso: 0, Pequeno: 0, Roto: 0, Desecho: 0, Otro: 0);

    [Theory]
    [InlineData("Traslado")]
    [InlineData("TRASLADOS")]
    [InlineData("Traslado a planta")]
    [InlineData("Planta")]
    [InlineData("Envío a planta")]
    public void TryOperacion_reconoce_traslado_con_sinonimos(string texto)
    {
        Assert.True(MigracionMovimientosHuevosCalculos.TryOperacion(texto, out var tipo));
        Assert.Equal(MovimientoHuevosMigracion.Traslado, tipo);
    }

    [Theory]
    [InlineData("Venta")]
    [InlineData("ventas")]
    [InlineData("Venta de huevos")]
    [InlineData("venta huevos")]
    public void TryOperacion_reconoce_venta_con_sinonimos(string texto)
    {
        Assert.True(MigracionMovimientosHuevosCalculos.TryOperacion(texto, out var tipo));
        Assert.Equal(MovimientoHuevosMigracion.Venta, tipo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Retiro")]
    [InlineData("cualquier cosa")]
    public void TryOperacion_rechaza_vacio_y_texto_no_reconocido(string? texto)
    {
        Assert.False(MigracionMovimientosHuevosCalculos.TryOperacion(texto, out _));
    }

    [Fact]
    public void TipoDestinoEfectivo_defaults_de_la_UI_y_normalizacion()
    {
        // Vacío ⇒ default por operación (los mismos de la pantalla de traslados de huevos).
        Assert.Equal("Planta", MigracionMovimientosHuevosCalculos.TipoDestinoEfectivo(null, MovimientoHuevosMigracion.Traslado));
        Assert.Equal("Cliente", MigracionMovimientosHuevosCalculos.TipoDestinoEfectivo("", MovimientoHuevosMigracion.Venta));
        // Texto válido se normaliza a la opción canónica; inválido ⇒ null (error de fila).
        Assert.Equal("Empresa", MigracionMovimientosHuevosCalculos.TipoDestinoEfectivo("  EMPRESA ", MovimientoHuevosMigracion.Venta));
        Assert.Null(MigracionMovimientosHuevosCalculos.TipoDestinoEfectivo("bodega", MovimientoHuevosMigracion.Venta));
    }

    [Fact]
    public void ClaveArchivo_distingue_fecha_tipo_y_cantidades()
    {
        var fecha = new DateTime(2026, 6, 10);
        var clave = MigracionMovimientosHuevosCalculos.ClaveArchivo(fecha, MovimientoHuevosMigracion.Traslado, Cantidades);

        Assert.Equal(clave, MigracionMovimientosHuevosCalculos.ClaveArchivo(fecha, MovimientoHuevosMigracion.Traslado, Cantidades));
        Assert.NotEqual(clave, MigracionMovimientosHuevosCalculos.ClaveArchivo(fecha.AddDays(1), MovimientoHuevosMigracion.Traslado, Cantidades));
        Assert.NotEqual(clave, MigracionMovimientosHuevosCalculos.ClaveArchivo(fecha, MovimientoHuevosMigracion.Venta, Cantidades));
        Assert.NotEqual(clave, MigracionMovimientosHuevosCalculos.ClaveArchivo(fecha, MovimientoHuevosMigracion.Traslado, Cantidades with { Limpio = 1999 }));
    }
}
