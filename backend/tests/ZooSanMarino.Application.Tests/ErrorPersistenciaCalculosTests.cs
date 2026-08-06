using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la traducción del <c>SqlState</c> de Postgres (incidente 2026-08-06): el handler global
/// devolvía el texto genérico de EF y el usuario no tenía forma de saber qué pasó.
/// Lo importante del contrato es que sea ADITIVO: un código no mapeado devuelve <c>null</c> y el
/// handler conserva el mensaje que ya mostraba.
/// </summary>
public class ErrorPersistenciaCalculosTests
{
    [Fact] // E1 — el código del incidente
    public void DescribirErrorSql_22001_HablaDeTextoDemasiadoLargo()
    {
        var msg = ErrorPersistenciaCalculos.DescribirErrorSql("22001");

        Assert.NotNull(msg);
        Assert.Contains("largo permitido", msg!);
        Assert.Contains("alimentos", msg);
    }

    [Theory] // E2-E4 + resto de códigos mapeados: siempre devuelven algo legible
    [InlineData("23505")]
    [InlineData("23503")]
    [InlineData("23502")]
    [InlineData("23514")]
    [InlineData("22003")]
    public void DescribirErrorSql_CodigosMapeados_DevuelvenMensaje(string sqlState)
    {
        var msg = ErrorPersistenciaCalculos.DescribirErrorSql(sqlState);

        Assert.False(string.IsNullOrWhiteSpace(msg));
    }

    [Theory] // E5 — no mapeado ⇒ null ⇒ el handler cae al comportamiento actual (cero regresión)
    [InlineData("42P01")]
    [InlineData("40001")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DescribirErrorSql_NoMapeado_DevuelveNull(string? sqlState)
    {
        Assert.Null(ErrorPersistenciaCalculos.DescribirErrorSql(sqlState));
    }

    [Fact] // tolera el espacio en blanco que a veces trae el driver
    public void DescribirErrorSql_ConEspacios_IgualReconoceElCodigo()
    {
        Assert.Equal(
            ErrorPersistenciaCalculos.DescribirErrorSql("22001"),
            ErrorPersistenciaCalculos.DescribirErrorSql("  22001 "));
    }

    [Fact] // los mensajes son para el usuario: no filtran nombres internos de la BD
    public void DescribirErrorSql_NoFiltraDetalleInternoDeLaBase()
    {
        string[] codigos = ["22001", "23505", "23503", "23502", "23514", "22003"];

        foreach (var codigo in codigos)
        {
            var msg = ErrorPersistenciaCalculos.DescribirErrorSql(codigo)!;
            Assert.DoesNotContain("varchar", msg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("column", msg, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("_", msg); // ningún identificador snake_case de tabla/columna
        }
    }
}
