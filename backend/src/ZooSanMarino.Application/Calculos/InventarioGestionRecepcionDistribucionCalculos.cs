// src/ZooSanMarino.Application/Calculos/InventarioGestionRecepcionDistribucionCalculos.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA de la recepción de un traslado inter-granja en tránsito: resuelve <b>en qué ubicaciones</b>
/// de la granja destino entra la cantidad recibida y <b>cuánto</b> entra en cada una.
/// <list type="bullet">
///   <item>Sin distribución → una sola ubicación con la cantidad completa (comportamiento clásico,
///     con los mismos mensajes de error de siempre).</item>
///   <item>Con distribución (solo alimento manejado por galpón) → N ubicaciones cuya suma debe igualar
///     la cantidad en tránsito; no hay recepción parcial.</item>
/// </list>
/// Sin EF ni estado: el service solo resuelve <c>usaUbicacion</c> y persiste lo que devuelve <see cref="Resolver"/>.
/// </summary>
public static class InventarioGestionRecepcionDistribucionCalculos
{
    /// <summary>Tolerancia al comparar la suma de la distribución contra la cantidad en tránsito.</summary>
    public const decimal ToleranciaSuma = 0.0001m;

    /// <summary>Ubicación resuelta de destino. Núcleo/galpón nulos = recepción a nivel granja.</summary>
    public sealed record Destino(string? NucleoId, string? GalponId, decimal Quantity);

    /// <summary>
    /// Resuelve las ubicaciones de destino de la recepción.
    /// </summary>
    /// <param name="distribucion">Reparto solicitado (puede ser null/vacío o traer filas sin llenar).</param>
    /// <param name="toNucleoId">Núcleo destino del camino clásico (una sola ubicación).</param>
    /// <param name="toGalponId">Galpón destino del camino clásico (una sola ubicación).</param>
    /// <param name="usaUbicacion">true = alimento manejado por galpón en la granja destino.</param>
    /// <param name="cantidadTransito">Cantidad que viaja en el tránsito (se recibe completa).</param>
    /// <returns>Destinos a aplicar, o el mensaje de error de validación (nunca ambos).</returns>
    public static (IReadOnlyList<Destino> Destinos, string? Error) Resolver(
        IReadOnlyList<InventarioGestionRecepcionDestinoDto>? distribucion,
        string? toNucleoId,
        string? toGalponId,
        bool usaUbicacion,
        decimal cantidadTransito)
    {
        var filas = (distribucion ?? Array.Empty<InventarioGestionRecepcionDestinoDto>())
            .Where(d => !EsFilaVacia(d))
            .ToList();

        // ─── Camino clásico: una sola ubicación (mensajes idénticos a los históricos) ───
        if (filas.Count == 0)
        {
            if (usaUbicacion && (string.IsNullOrWhiteSpace(toNucleoId) || string.IsNullOrWhiteSpace(toGalponId)))
                return (Array.Empty<Destino>(), "Para alimento debe indicar Núcleo y Galpón de recepción en la granja destino.");
            if (!usaUbicacion && (!string.IsNullOrWhiteSpace(toNucleoId) || !string.IsNullOrWhiteSpace(toGalponId)))
                return (Array.Empty<Destino>(), "La recepción es solo a nivel granja (sin Núcleo/Galpón).");

            var unico = usaUbicacion
                ? new Destino(toNucleoId!.Trim(), toGalponId!.Trim(), cantidadTransito)
                : new Destino(null, null, cantidadTransito);
            return (new[] { unico }, null);
        }

        // ─── Camino distribuido ───
        if (!usaUbicacion)
            return (Array.Empty<Destino>(), "La distribución por galpón solo aplica a alimento manejado por galpón. Esta recepción es a nivel granja.");

        var destinos = new List<Destino>(filas.Count);
        var vistos = new HashSet<(string, string)>();
        decimal suma = 0m;

        foreach (var fila in filas)
        {
            var nucleoId = fila.NucleoId?.Trim();
            var galponId = fila.GalponId?.Trim();

            if (string.IsNullOrWhiteSpace(nucleoId) || string.IsNullOrWhiteSpace(galponId))
                return (Array.Empty<Destino>(), "Cada destino de la distribución debe indicar Núcleo y Galpón.");
            if (fila.Quantity <= 0)
                return (Array.Empty<Destino>(), "Las cantidades de la distribución deben ser mayores a cero.");
            if (!vistos.Add((nucleoId, galponId)))
                return (Array.Empty<Destino>(), $"No repita el mismo galpón en la distribución (galpón {galponId}).");

            suma += fila.Quantity;
            destinos.Add(new Destino(nucleoId, galponId, fila.Quantity));
        }

        if (Math.Abs(suma - cantidadTransito) > ToleranciaSuma)
            return (Array.Empty<Destino>(), $"La suma de la distribución ({suma:0.###}) debe ser igual a la cantidad en tránsito ({cantidadTransito:0.###}).");

        return (destinos, null);
    }

    /// <summary>Fila que el usuario dejó sin llenar en la tabla de distribución: se ignora, no invalida el envío.</summary>
    private static bool EsFilaVacia(InventarioGestionRecepcionDestinoDto d) =>
        string.IsNullOrWhiteSpace(d.NucleoId) && string.IsNullOrWhiteSpace(d.GalponId) && d.Quantity == 0m;
}
