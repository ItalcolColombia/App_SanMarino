using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Filtro de operación para manejar correctamente los archivos IFormFile en Swagger.
/// (Microsoft.OpenApi v2: Type es JsonSchemaType y Properties es IOpenApiSchema.)
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var fileParams = context.MethodInfo.GetParameters()
            .Where(p => p.ParameterType == typeof(IFormFile) ||
                       p.ParameterType == typeof(IFormFile[]) ||
                       p.ParameterType == typeof(List<IFormFile>))
            .ToList();

        if (!fileParams.Any()) return;

        // Propiedades del multipart/form-data: cada archivo como string/binary + params FromForm
        var properties = new Dictionary<string, IOpenApiSchema>();

        foreach (var param in fileParams)
        {
            properties[param.Name!] = new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "binary"
            };
        }

        // Otros parámetros FromForm como propiedades adicionales
        var formParams = context.MethodInfo.GetParameters()
            .Where(p => p.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromFormAttribute>() != null &&
                       p.ParameterType != typeof(IFormFile) &&
                       p.ParameterType != typeof(IFormFile[]) &&
                       p.ParameterType != typeof(List<IFormFile>))
            .ToList();

        foreach (var param in formParams)
        {
            properties[param.Name!] = new OpenApiSchema
            {
                Type = GetOpenApiType(param.ParameterType)
            };
        }

        // Configurar el content type para multipart/form-data
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = properties
                    }
                }
            }
        };
    }

    private static JsonSchemaType GetOpenApiType(Type type)
    {
        return type switch
        {
            var t when t == typeof(string) => JsonSchemaType.String,
            var t when t == typeof(int) || t == typeof(int?) => JsonSchemaType.Integer,
            var t when t == typeof(long) || t == typeof(long?) => JsonSchemaType.Integer,
            var t when t == typeof(float) || t == typeof(float?) => JsonSchemaType.Number,
            var t when t == typeof(double) || t == typeof(double?) => JsonSchemaType.Number,
            var t when t == typeof(decimal) || t == typeof(decimal?) => JsonSchemaType.Number,
            var t when t == typeof(bool) || t == typeof(bool?) => JsonSchemaType.Boolean,
            var t when t == typeof(DateTime) || t == typeof(DateTime?) => JsonSchemaType.String,
            var t when t == typeof(DateOnly) || t == typeof(DateOnly?) => JsonSchemaType.String,
            var t when t == typeof(TimeOnly) || t == typeof(TimeOnly?) => JsonSchemaType.String,
            _ => JsonSchemaType.String
        };
    }
}
