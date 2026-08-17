// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionPendienteDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Una vacuna que le falta a un lote: la bandeja "hoy me toca". Trae dónde es (granja/núcleo/galpón/
/// lote), qué es (vacuna) y cuándo era (franja), más la situación ya resuelta contra el día de hoy.
/// </summary>
/// <param name="Situacion"><c>Vencido</c> · <c>EnFranja</c> · <c>Proximo</c> (VacunacionPendientesCalculos).</param>
/// <param name="Dias">Positivo = días de atraso · 0 = hoy está dentro de la franja · negativo = faltan tantos días.</param>
public record VacunacionPendienteDto(
    int CronogramaItemId,
    string LineaProductiva,
    int LoteId,
    string LoteNombre,
    int GranjaId,
    string? GranjaNombre,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioId,
    string ItemInventarioNombre,
    string UnidadObjetivo,
    int? ValorObjetivo,
    DateTime FechaInicioFranja,
    DateTime FechaFinFranja,
    string Situacion,
    int Dias
);
