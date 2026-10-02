// tests/ZooSanMarino.Application.Tests/TicketAdjuntoCalculosTests.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// El contenido de un adjunto se guarda como base64 puro. Los subidos desde el formulario de
/// creación llegaban como data URL y la descarga salía dañada (30-sep-2026).
/// </summary>
public class TicketAdjuntoCalculosTests
{
    // "%PDF-1.4" en base64.
    private const string PdfBase64 = "JVBERi0xLjQ=";

    [Fact]
    public void Base64_puro_vuelve_igual()
        => Assert.Equal(PdfBase64, TicketAdjuntoCalculos.NormalizarBase64(PdfBase64));

    [Theory]
    [InlineData("data:application/pdf;base64," + PdfBase64)]
    [InlineData("DATA:application/pdf;base64," + PdfBase64)]
    [InlineData("data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64," + PdfBase64)]
    [InlineData("data:;base64," + PdfBase64)]
    public void Data_url_pierde_el_prefijo(string entrada)
        => Assert.Equal(PdfBase64, TicketAdjuntoCalculos.NormalizarBase64(entrada));

    [Fact]
    public void Espacios_y_saltos_de_linea_se_quitan()
        => Assert.Equal(PdfBase64, TicketAdjuntoCalculos.NormalizarBase64("  JVBE\r\nRi0x\nLjQ= "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("data:application/pdf;base64")]
    public void Vacio_o_sin_contenido_da_cadena_vacia(string? entrada)
        => Assert.Equal(string.Empty, TicketAdjuntoCalculos.NormalizarBase64(entrada));

    [Fact]
    public void El_resultado_decodifica_a_los_bytes_originales()
    {
        var bytes = Convert.FromBase64String(
            TicketAdjuntoCalculos.NormalizarBase64("data:application/pdf;base64," + PdfBase64));
        Assert.Equal("%PDF-1.4"u8.ToArray(), bytes);
    }
}
