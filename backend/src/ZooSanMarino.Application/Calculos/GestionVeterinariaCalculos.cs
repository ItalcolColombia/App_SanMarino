using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Reglas puras de fechas, ubicación y evidencias de Gestión veterinaria.</summary>
public static class GestionVeterinariaCalculos
{
    public const int MaxImagenes = 3;
    public const int MaxImagenBytes = 2_500_000;
    public const int MaxObservacionChars = 2_000;
    public const string TemporalProxima = "PROXIMA";
    public const string TemporalActiva = "ACTIVA";
    public const string TemporalVencida = "VENCIDA";
    public const string TemporalCerrada = "CERRADA";

    private static readonly IReadOnlySet<string> TiposImagen =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg", "image/png", "image/webp"
        };

    public sealed record UbicacionAcceso(
        bool TieneGranja,
        bool ScopeGlobal,
        IReadOnlySet<string> NucleosVisibles,
        IReadOnlySet<string> GalponesVisibles,
        IReadOnlySet<int> LotesPermitidos);

    public static void ValidarPeriodo(DateTime fechaInicio, DateTime fechaFin)
    {
        if (fechaInicio.Date > fechaFin.Date)
            throw new InvalidOperationException("La fecha inicial no puede ser posterior a la fecha final.");
    }

    public static string EstadoTemporal(DateTime fechaInicio, DateTime fechaFin, string estado, DateTime ahora)
    {
        if (estado != EstadoTareaCampo.Pendiente) return TemporalCerrada;
        if (ahora.Date < fechaInicio.Date) return TemporalProxima;
        if (ahora.Date > fechaFin.Date) return TemporalVencida;
        return TemporalActiva;
    }

    /// <summary>
    /// Una tarea general de granja alcanza a cualquier usuario asignado. Una tarea más específica
    /// alcanza a quien tenga visible ese nivel (incluidos los ancestros visibles de un grant hijo).
    /// </summary>
    public static bool PuedeAcceder(
        UbicacionAcceso acceso, string? nucleoId, string? galponId, int? loteId)
    {
        if (!acceso.TieneGranja) return false;
        if (acceso.ScopeGlobal) return true;
        if (loteId.HasValue) return acceso.LotesPermitidos.Contains(loteId.Value);
        if (!string.IsNullOrWhiteSpace(galponId)) return acceso.GalponesVisibles.Contains(galponId);
        if (!string.IsNullOrWhiteSpace(nucleoId)) return acceso.NucleosVisibles.Contains(nucleoId);
        return true;
    }

    public static string? ValidarCumplimiento(
        bool requiereObservacion, bool requiereFoto, string? observacion, int cantidadImagenes)
    {
        if (requiereObservacion && string.IsNullOrWhiteSpace(observacion))
            return "La observación de cumplimiento es obligatoria para esta tarea.";
        if ((observacion?.Trim().Length ?? 0) > MaxObservacionChars)
            return $"La observación no puede superar {MaxObservacionChars} caracteres.";
        if (requiereFoto && cantidadImagenes == 0)
            return "Debe adjuntar al menos una fotografía para completar esta tarea.";
        if (cantidadImagenes > MaxImagenes)
            return $"Puede adjuntar máximo {MaxImagenes} fotografías.";
        return null;
    }

    public static string? ValidarImagen(string? base64, string? contentType, int sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(base64)) return "La fotografía está vacía.";
        if (string.IsNullOrWhiteSpace(contentType) || !TiposImagen.Contains(contentType.Trim()))
            return "La fotografía debe ser JPEG, PNG o WebP.";
        if (sizeBytes <= 0 || sizeBytes > MaxImagenBytes)
            return $"Cada fotografía debe pesar máximo {MaxImagenBytes / 1_000_000m:0.#} MB.";

        var expectedPrefix = $"data:{contentType.Trim().ToLowerInvariant()};base64,";
        var contenido = base64.Trim();
        if (!contenido.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return "El contenido de la fotografía no coincide con su tipo de archivo.";
        var payload = contenido[expectedPrefix.Length..];
        var maxEncodedChars = ((MaxImagenBytes + 2) / 3) * 4;
        if (payload.Length == 0 || payload.Length > maxEncodedChars)
            return $"Cada fotografía debe pesar máximo {MaxImagenBytes / 1_000_000m:0.#} MB.";
        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length != sizeBytes)
                return "El tamaño declarado de la fotografía no coincide con su contenido.";
        }
        catch (FormatException)
        {
            return "La fotografía no contiene un Base64 válido.";
        }
        return null;
    }

    public static string NormalizarTextoRequerido(string? valor, string campo, int maxChars)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length == 0) throw new InvalidOperationException($"{campo} es obligatorio.");
        if (limpio.Length > maxChars)
            throw new InvalidOperationException($"{campo} no puede superar {maxChars} caracteres.");
        return limpio;
    }
}
