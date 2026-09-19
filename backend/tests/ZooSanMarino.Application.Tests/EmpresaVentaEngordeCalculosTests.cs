using Xunit;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// «Empresa de venta» del despacho de pollo engorde (lista maestra <c>venta_pollo_engorde_empresa</c>): se guarda como
/// texto en <c>movimiento_pollo_engorde.planta_destino</c>. La normalización es lo único que el backend decide
/// (no valida pertenencia a la lista: es editable y la carga masiva escribe texto libre en la misma columna).
/// </summary>
public class EmpresaVentaEngordeCalculosTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public void SinEmpresa_QuedaNull(string? entrada)
    {
        // Venta sin empresa = comportamiento de siempre (columna en NULL).
        Assert.Null(MovimientoPolloEngordeCalculos.NormalizarEmpresaVenta(entrada));
    }

    [Theory]
    [InlineData("Planta", "Planta")]
    [InlineData("  Planta  ", "Planta")]
    [InlineData("\tPlanta Ecuador\n", "Planta Ecuador")]
    public void ConEmpresa_RecortaLosEspacios(string entrada, string esperado)
    {
        Assert.Equal(esperado, MovimientoPolloEngordeCalculos.NormalizarEmpresaVenta(entrada));
    }

    [Fact]
    public void NoCambiaMayusculasNiEspaciosInternos()
    {
        // El texto es la identidad de la opción: se conserva tal cual lo escribió la lista maestra.
        Assert.Equal("Cía. del  Norte S.A.", MovimientoPolloEngordeCalculos.NormalizarEmpresaVenta("Cía. del  Norte S.A."));
    }

    [Fact]
    public void ExactamenteElLargoMaximo_EsValido()
    {
        var texto = new string('E', MovimientoPolloEngordeCalculos.EmpresaVentaMaxLen);
        Assert.Equal(texto, MovimientoPolloEngordeCalculos.NormalizarEmpresaVenta(texto));
    }

    [Fact]
    public void UnCaracterDeMas_LanzaConMensajeLegible()
    {
        var texto = new string('E', MovimientoPolloEngordeCalculos.EmpresaVentaMaxLen + 1);
        var ex = Assert.Throws<InvalidOperationException>(() => MovimientoPolloEngordeCalculos.NormalizarEmpresaVenta(texto));
        Assert.Contains("empresa de venta", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("200", ex.Message);
    }

    [Fact]
    public void LosEspaciosDeLosBordesNoCuentanParaElLargo()
    {
        // 200 caracteres reales + espacios alrededor: se recorta primero y entra.
        var texto = "  " + new string('E', MovimientoPolloEngordeCalculos.EmpresaVentaMaxLen) + "  ";
        Assert.Equal(MovimientoPolloEngordeCalculos.EmpresaVentaMaxLen,
            MovimientoPolloEngordeCalculos.NormalizarEmpresaVenta(texto)!.Length);
    }

    [Fact]
    public void ElLargoMaximoEsElDeLaColumnaPlantaDestino()
    {
        // movimiento_pollo_engorde.planta_destino es varchar(200) (MovimientoPolloEngordeConfiguration).
        Assert.Equal(200, MovimientoPolloEngordeCalculos.EmpresaVentaMaxLen);
    }

    // ── Contrato de los DTO: las dos cabeceras de venta y la lectura llevan la empresa ─────────────────────────────

    [Fact]
    public void LasDosCabecerasDeVenta_AceptanPlantaDestino()
    {
        var ecuador = new CreateVentaGranjaDespachoDto { PlantaDestino = "Planta" };
        var panama = new CreateVentaPanamaDespachoDto { PlantaDestino = "Planta" };
        Assert.Equal("Planta", ecuador.PlantaDestino);
        Assert.Equal("Planta", panama.PlantaDestino);
    }

    [Fact]
    public void LasCabecerasDeVenta_SinEmpresa_NacenNull()
    {
        // Un cliente que no conoce el campo (app móvil, carga vieja) sigue funcionando: null = sin empresa.
        Assert.Null(new CreateVentaGranjaDespachoDto().PlantaDestino);
        Assert.Null(new CreateVentaPanamaDespachoDto().PlantaDestino);
    }

    [Fact]
    public void ElDtoDeLectura_TraeEmpresaYUbicacionDeOrigen_ConDefaultNull()
    {
        // Los campos nuevos van al FINAL y con default: quien construya el DTO sin ellos sigue compilando.
        var dto = new MovimientoPolloEngordeDto(
            Id: 1, NumeroMovimiento: "MPE-1", FechaMovimiento: DateTime.UtcNow, TipoMovimiento: "Venta",
            TipoLoteOrigen: "AveEngorde", LoteOrigenId: 10, LoteOrigenNombre: "L1",
            TipoLoteDestino: null, LoteDestinoId: null, LoteDestinoNombre: null,
            GranjaOrigenId: 1, GranjaOrigenNombre: "G", GranjaDestinoId: null, GranjaDestinoNombre: null,
            CantidadHembras: 0, CantidadMachos: 0, CantidadMixtas: 5, TotalAves: 5,
            Estado: "Pendiente", MotivoMovimiento: null, Observaciones: null,
            UsuarioMovimientoId: 1, UsuarioNombre: null, FechaProcesamiento: null, FechaCancelacion: null,
            CreatedAt: DateTime.UtcNow);

        Assert.Null(dto.PlantaDestino);
        Assert.Null(dto.NucleoOrigenId);
        Assert.Null(dto.GalponOrigenId);

        var conEmpresa = dto with { PlantaDestino = "Planta", NucleoOrigenId = "N1", GalponOrigenId = "G1" };
        Assert.Equal("Planta", conEmpresa.PlantaDestino);
        Assert.Equal("N1", conEmpresa.NucleoOrigenId);
        Assert.Equal("G1", conEmpresa.GalponOrigenId);
    }
}
