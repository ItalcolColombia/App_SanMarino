using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace ZooSanMarino.API.Infrastructure;

/// <summary>
/// Filtro de operación para manejar correctamente los archivos IFormFile en Swagger
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

        // Configurar el content type para multipart/form-data
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>()
                    }
                }
            }
        };

        // Agregar propiedades para cada parámetro de archivo
        foreach (var param in fileParams)
        {
            var schema = new OpenApiSchema
            {
                Type = "string",
                Format = "binary"
            };

            operation.RequestBody.Content["multipart/form-data"].Schema.Properties[param.Name!] = schema;
        }

        // Agregar otros parámetros FromForm como propiedades adicionales
        var formParams = context.MethodInfo.GetParameters()
            .Where(p => p.GetCustomAttribute<Microsoft.AspNetCore.Mvc.FromFormAttribute>() != null &&
                       p.ParameterType != typeof(IFormFile) &&
                       p.ParameterType != typeof(IFormFile[]) &&
                       p.ParameterType != typeof(List<IFormFile>))
            .ToList();

        foreach (var param in formParams)
        {
            var schema = new OpenApiSchema
            {
                Type = GetOpenApiType(param.ParameterType)
            };

            operation.RequestBody.Content["multipart/form-data"].Schema.Properties[param.Name!] = schema;
        }
    }

    private static string GetOpenApiType(Type type)
    {
        return type switch
        {
            var t when t == typeof(string) => "string",
            var t when t == typeof(int) || t == typeof(int?) => "integer",
            var t when t == typeof(long) || t == typeof(long?) => "integer",
            var t when t == typeof(float) || t == typeof(float?) => "number",
            var t when t == typeof(double) || t == typeof(double?) => "number",
            var t when t == typeof(decimal) || t == typeof(decimal?) => "number",
            var t when t == typeof(bool) || t == typeof(bool?) => "boolean",
            var t when t == typeof(DateTime) || t == typeof(DateTime?) => "string",
            var t when t == typeof(DateOnly) || t == typeof(DateOnly?) => "string",
            var t when t == typeof(TimeOnly) || t == typeof(TimeOnly?) => "string",
            _ => "string"
        };
    }
}
