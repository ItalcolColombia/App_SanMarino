// src/ZooSanMarino.Application/Calculos/MigracionEsquemaCalculos.cs
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO (sin EF ni estado) para validar los encabezados de un archivo contra un
/// <see cref="EsquemaMigracion"/> y para acotar la cantidad de errores reportados. Testeable en
/// aislamiento (xUnit).
/// </summary>
public static class MigracionEsquemaCalculos
{
    /// <summary>
    /// Compara los encabezados normalizados del archivo contra el esquema.
    /// Faltante = ninguna clave (título normalizado ni alias) de una columna REQUERIDA está presente.
    /// Desconocido = header del archivo que no matchea ninguna columna (título ni alias) del esquema.
    /// </summary>
    public static (IReadOnlyList<string> FaltantesRequeridos, IReadOnlyList<string> Desconocidos) ValidarEncabezados(
        EsquemaMigracion esquema, IReadOnlyCollection<string> headersNormalizados)
    {
        var headersSet = headersNormalizados.Select(MigracionCalculos.NormalizarClave).ToHashSet();
        var clavesPorColumna = esquema.Columnas.Select(c => ClavesDe(c).ToHashSet()).ToList();

        var faltantes = new List<string>();
        for (int i = 0; i < esquema.Columnas.Count; i++)
        {
            var columna = esquema.Columnas[i];
            if (!columna.Requerida) continue;
            if (!clavesPorColumna[i].Overlaps(headersSet)) faltantes.Add(columna.Titulo);
        }

        var todasLasClaves = clavesPorColumna.SelectMany(c => c).ToHashSet();
        var desconocidos = headersNormalizados
            .Where(h => !todasLasClaves.Contains(MigracionCalculos.NormalizarClave(h)))
            .ToList();

        return (faltantes, desconocidos);
    }

    /// <summary>Claves normalizadas aceptadas para una columna: el título y sus alias.</summary>
    private static IEnumerable<string> ClavesDe(ColumnaEsquema columna)
    {
        yield return MigracionCalculos.NormalizarClave(columna.Titulo);
        if (columna.Alias is null) yield break;
        foreach (var alias in columna.Alias) yield return MigracionCalculos.NormalizarClave(alias);
    }

    /// <summary>
    /// Capa <paramref name="errores"/> a lo sumo <paramref name="max"/> entradas; si corta, agrega una
    /// entrada meta informando cuántos problemas reales había. <c>TotalReal</c> siempre es el conteo
    /// original (sin capar).
    /// </summary>
    public static (IReadOnlyList<MigracionErrorDto> Capados, int TotalReal) LimitarErrores(
        IReadOnlyList<MigracionErrorDto> errores, int max)
    {
        var total = errores.Count;
        if (total <= max) return (errores, total);

        var capados = new List<MigracionErrorDto>(max + 1);
        capados.AddRange(errores.Take(max));
        capados.Add(new MigracionErrorDto(0, "-", null, $"Se muestran los primeros {max} de {total} problemas detectados.", "Advertencia"));
        return (capados, total);
    }
}
