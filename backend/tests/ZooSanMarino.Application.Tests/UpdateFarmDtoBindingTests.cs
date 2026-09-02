using System.Text.Json;
using ZooSanMarino.Application.DTOs.Farms;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de alambre entre la pantalla de granjas y <see cref="UpdateFarmDto"/>.
///
/// <para><b>Por qué existe.</b> <c>FarmService.UpdateAsync</c> asigna los campos opcionales del DTO
/// SIN condicional (<c>entity.ManejaAlimentoPorGalpon = dto.ManejaAlimentoPorGalpon</c>): un campo
/// que el front no manda llega como <c>null</c> y se BORRA, sin error y sin aviso. El 1-sep-2026 la
/// pestaña «Granjas» armaba el payload campo por campo y omitía dos, así que cada edición de granja
/// borraba <c>codigo_erp_engorde</c> (el correlativo ERP de engorde de Panamá, que avanza +1 al
/// cerrar el ciclo) y <c>maneja_alimento_por_galpon</c> (el override por granja del nivel de
/// alimento).</para>
///
/// <para>El front ya tiene su propio test sobre el payload
/// (<c>frontend/src/tests/construir-payload-granja.funcion.spec.ts</c>). Éste cierra el eslabón que
/// aquél no puede ver: que ese JSON, con esos nombres, efectivamente llegue al DTO.</para>
/// </summary>
public class UpdateFarmDtoBindingTests
{
    // El binder de ASP.NET Core usa System.Text.Json con camelCase por convención.
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    /// <summary>Payload tal cual lo arma hoy `construir-payload-granja.funcion.ts`.</summary>
    private const string PayloadDeLaPantalla = """
    {
      "id": 55,
      "name": "Granja La Esperanza",
      "companyId": 7,
      "status": "A",
      "regionalId": 42,
      "departamentoId": 11,
      "ciudadId": 110,
      "clienteId": 3,
      "zona": "Zona 1",
      "certificadoGab": true,
      "latitud": 8.9824,
      "longitud": -79.5199,
      "manejaAlimentoPorGalpon": true,
      "codigoErpEngorde": "4001017",
      "codigoBodega": "B0601",
      "descripcionBodega": "Bodega Granja La Esperanza",
      "centroOperacion": "830",
      "descripcionCentroOperacion": "Centro de operacion Buga",
      "codigoInstalacion": "B06",
      "descripcionInstalacion": "Instalacion granja"
    }
    """;

    [Fact]
    public void El_payload_de_la_pantalla_llena_los_dos_campos_que_se_borraban()
    {
        var dto = JsonSerializer.Deserialize<UpdateFarmDto>(PayloadDeLaPantalla, Opciones)!;

        Assert.Equal("4001017", dto.CodigoErpEngorde);
        Assert.True(dto.ManejaAlimentoPorGalpon);
    }

    [Fact]
    public void El_payload_de_la_pantalla_no_pierde_ningun_otro_campo()
    {
        var dto = JsonSerializer.Deserialize<UpdateFarmDto>(PayloadDeLaPantalla, Opciones)!;

        Assert.Equal(55, dto.Id);
        Assert.Equal("Granja La Esperanza", dto.Name);
        Assert.Equal(7, dto.CompanyId);
        Assert.Equal("A", dto.Status);
        Assert.Equal(42, dto.RegionalId);
        Assert.Equal(11, dto.DepartamentoId);
        Assert.Equal(110, dto.CiudadId);
        Assert.Equal(3, dto.ClienteId);
        Assert.Equal("Zona 1", dto.Zona);
        Assert.True(dto.CertificadoGab);
        Assert.Equal(8.9824m, dto.Latitud);
        Assert.Equal(-79.5199m, dto.Longitud);
        Assert.Equal("B0601", dto.CodigoBodega);
        Assert.Equal("830", dto.CentroOperacion);
        Assert.Equal("B06", dto.CodigoInstalacion);
    }

    /// <summary>
    /// El nivel de alimento es TRI-ESTADO y `null` es un valor con significado propio
    /// («hereda el flag de la empresa»), no un «no informado». Por eso el fix no podía vivir en el
    /// backend con un «si viene null, conservar el actual»: la granja se quedaría sin forma de
    /// volver a heredar. Los tres estados tienen que poder viajar.
    /// </summary>
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("null", null)]
    public void El_nivel_de_alimento_viaja_en_sus_tres_estados(string valorJson, bool? esperado)
    {
        var json = $$"""
        {"id":1,"companyId":1,"name":"G","status":"A","departamentoId":1,"ciudadId":1,
         "manejaAlimentoPorGalpon":{{valorJson}}}
        """;

        var dto = JsonSerializer.Deserialize<UpdateFarmDto>(json, Opciones)!;

        Assert.Equal(esperado, dto.ManejaAlimentoPorGalpon);
    }

    /// <summary>
    /// La foto del defecto: un payload al que le faltan las dos claves deserializa igual —sin error—
    /// y los deja en `null`. Por eso el bug no dio ni una señal durante meses: para el backend, un
    /// campo ausente y un borrado explícito son exactamente lo mismo.
    /// </summary>
    [Fact]
    public void Un_payload_sin_esas_claves_deserializa_sin_error_y_las_deja_en_null()
    {
        var json = """
        {"id":55,"companyId":7,"name":"Granja La Esperanza","status":"A",
         "departamentoId":11,"ciudadId":110}
        """;

        var dto = JsonSerializer.Deserialize<UpdateFarmDto>(json, Opciones)!;

        Assert.Null(dto.CodigoErpEngorde);
        Assert.Null(dto.ManejaAlimentoPorGalpon);
    }
}
