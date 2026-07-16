using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Parser puro del jsonb de fn_vacunacion_filter_data: claves camelCase 1:1 con el DTO,
/// normalización a listas vacías (nunca null) y preservación del orden que fija la función SQL.
/// </summary>
public class VacunacionFilterDataJsonTests
{
    private const string JsonCompleto = """
    {
      "granjas": [
        { "id": 10, "companyId": 2, "name": "Granja Alfa" },
        { "id": 25, "companyId": 2, "name": "Granja Beta" }
      ],
      "lotes": [
        { "loteId": 501, "lineaProductiva": "Levante", "loteNombre": "L-501", "granjaId": 10,
          "nucleoId": "N1", "galponId": "G3", "fechaEncaset": "2026-03-02T00:00:00", "estadoCierre": null },
        { "loteId": 702, "lineaProductiva": "Engorde", "loteNombre": "E-702", "granjaId": 25,
          "nucleoId": null, "galponId": null, "fechaEncaset": null, "estadoCierre": "Activo" }
      ],
      "vacunas": [
        { "id": 7, "codigo": "VAC-NEW", "nombre": "Newcastle", "unidad": "dosis" }
      ],
      "usuarios": [
        { "id": 123456, "nombre": "Ana Pérez" },
        { "id": 654321, "nombre": null }
      ]
    }
    """;

    [Fact]
    public void Parse_JsonCompleto_MapeaLasCuatroColecciones()
    {
        var dto = VacunacionFilterDataJson.Parse(JsonCompleto);

        Assert.Equal(2, dto.Granjas.Count);
        Assert.Equal(2, dto.Lotes.Count);
        Assert.Single(dto.Vacunas);
        Assert.Equal(2, dto.Usuarios.Count);
    }

    [Fact]
    public void Parse_Granja_EsLiteConCamposExactos()
    {
        var g = VacunacionFilterDataJson.Parse(JsonCompleto).Granjas[0];
        Assert.Equal(10, g.Id);
        Assert.Equal(2, g.CompanyId);
        Assert.Equal("Granja Alfa", g.Name);
    }

    [Fact]
    public void Parse_Lote_ConFechaEncasetYNulos()
    {
        var dto = VacunacionFilterDataJson.Parse(JsonCompleto);

        var conFecha = dto.Lotes[0];
        Assert.Equal(501, conFecha.LoteId);
        Assert.Equal("Levante", conFecha.LineaProductiva);
        Assert.Equal(new DateTime(2026, 3, 2), conFecha.FechaEncaset);
        Assert.Null(conFecha.EstadoCierre);

        var sinFecha = dto.Lotes[1];
        Assert.Null(sinFecha.FechaEncaset);
        Assert.Null(sinFecha.NucleoId);
        Assert.Equal("Activo", sinFecha.EstadoCierre);
    }

    [Fact]
    public void Parse_PreservaElOrdenDeLaFuncion()
    {
        var dto = VacunacionFilterDataJson.Parse(JsonCompleto);
        Assert.Equal(new[] { "Granja Alfa", "Granja Beta" }, dto.Granjas.Select(g => g.Name).ToArray());
        Assert.Equal(new[] { 501, 702 }, dto.Lotes.Select(l => l.LoteId).ToArray());
    }

    [Fact]
    public void Parse_Usuario_AdmiteNombreNull()
    {
        var u = VacunacionFilterDataJson.Parse(JsonCompleto).Usuarios[1];
        Assert.Equal(654321, u.Id);
        Assert.Null(u.Nombre);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_JsonVacioONull_DevuelveListasVacias(string? json)
    {
        var dto = VacunacionFilterDataJson.Parse(json);
        Assert.Empty(dto.Granjas);
        Assert.Empty(dto.Lotes);
        Assert.Empty(dto.Vacunas);
        Assert.Empty(dto.Usuarios);
    }

    [Fact]
    public void Parse_ObjetoSinClaves_NormalizaAListasVacias()
    {
        var dto = VacunacionFilterDataJson.Parse("{}");
        Assert.NotNull(dto.Granjas);
        Assert.Empty(dto.Granjas);
        Assert.Empty(dto.Lotes);
        Assert.Empty(dto.Vacunas);
        Assert.Empty(dto.Usuarios);
    }

    [Fact]
    public void Parse_ClavesParciales_CompletaLasFaltantes()
    {
        var dto = VacunacionFilterDataJson.Parse("""{ "granjas": [ { "id": 1, "companyId": 2, "name": "Solo" } ] }""");
        Assert.Single(dto.Granjas);
        Assert.Empty(dto.Lotes);
        Assert.Empty(dto.Vacunas);
        Assert.Empty(dto.Usuarios);
    }
}
