// src/ZooSanMarino.Application/Calculos/VacunacionFilterDataJson.cs
using System.Text.Json;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Parser PURO del jsonb que devuelve fn_vacunacion_filter_data (claves camelCase 1:1 con
/// <see cref="VacunacionFilterDataDto"/>). Separado del servicio EF para poder testearlo con xUnit.
/// Si se cambia una clave en la función SQL, este contrato debe cambiar a la par.
/// </summary>
public static class VacunacionFilterDataJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>Deserializa el jsonb de la función. Nunca devuelve null: json vacío/objeto
    /// incompleto normaliza a colecciones vacías (el front siempre recibe las 4 listas).</summary>
    public static VacunacionFilterDataDto Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Vacio();

        var dto = JsonSerializer.Deserialize<VacunacionFilterDataDto>(json, Options);
        if (dto is null) return Vacio();

        return dto with
        {
            Granjas = dto.Granjas ?? new List<VacunacionGranjaOpcionDto>(),
            Lotes = dto.Lotes ?? new List<VacunacionLoteOpcionDto>(),
            Vacunas = dto.Vacunas ?? new List<VacunacionVacunaOpcionDto>(),
            Usuarios = dto.Usuarios ?? new List<VacunacionUsuarioOpcionDto>(),
        };
    }

    private static VacunacionFilterDataDto Vacio() => new(
        new List<VacunacionGranjaOpcionDto>(),
        new List<VacunacionLoteOpcionDto>(),
        new List<VacunacionVacunaOpcionDto>(),
        new List<VacunacionUsuarioOpcionDto>());
}
