// src/ZooSanMarino.API/Infrastructure/EmpresaActivaHeadersOperationFilter.cs
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Declara en el contrato OpenAPI las tres cabeceras que deciden el <b>alcance multiempresa</b> de
/// casi todas las consultas: <c>X-Active-Company</c>, <c>X-Active-Company-Id</c> y
/// <c>X-Active-Pais</c>.
///
/// <para>
/// Existían en el código —las resuelve <see cref="ActiveCompanyMiddleware"/> y las consume
/// <see cref="HttpCurrentUser"/>— pero <b>no estaban en el swagger.json</b>. Consecuencia práctica:
/// desde Swagger no había forma de cambiar de empresa, así que el escenario que más importa probar
/// en este sistema (la misma petición devolviendo distinto según la empresa activa) no se podía
/// reproducir. El contrato mentía por omisión.
/// </para>
///
/// <para>
/// Son <b>opcionales</b> a propósito: sin ellas el backend cae al <c>company_id</c> del token, que es
/// el comportamiento vigente y no se cambia acá. Y son una <i>petición</i>, no una orden: el
/// middleware sólo publica la empresa si el usuario pertenece a ella (o es super admin). Mandar la
/// cabecera de una empresa ajena no amplía el alcance — deja el del token.
/// </para>
/// </summary>
public class EmpresaActivaHeadersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Sólo el API. /health, /hc y los ayudantes de Swagger no tienen alcance por empresa.
        var ruta = context.ApiDescription.RelativePath ?? "";
        if (!ruta.StartsWith("api/", StringComparison.OrdinalIgnoreCase)) return;

        operation.Parameters ??= [];

        operation.Parameters.Add(Cabecera(
            "X-Active-Company",
            "Empresa activa por NOMBRE (ej. `Sanmarino`, `ItalcolEcuador`). Opcional: sin ella se usa " +
            "la empresa del token. Se valida contra las empresas del usuario; si no pertenece, se ignora."));

        operation.Parameters.Add(Cabecera(
            "X-Active-Company-Id",
            "Empresa activa por ID. Tiene prioridad sobre `X-Active-Company` cuando llegan las dos."));

        operation.Parameters.Add(Cabecera(
            "X-Active-Pais",
            "País activo (ID). Acota además por país los módulos que lo soportan."));
    }

    private static OpenApiParameter Cabecera(string nombre, string descripcion) => new()
    {
        Name = nombre,
        In = ParameterLocation.Header,
        Required = false,
        Description = descripcion,
        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
    };
}
