// src/ZooSanMarino.Application/DTOs/Lotes/HistorialTrasladoLoteDto.cs
namespace ZooSanMarino.Application.DTOs.Lotes;

/// <summary>
/// Una fila del historial de traslados de un lote.
/// </summary>
/// <remarks>
/// <b><see cref="FechaTraslado"/> y <see cref="CreatedAt"/> no son lo mismo.</b> La primera es el
/// <b>dia real</b> en que el lote se movio —el que elige quien registra, en el modal— y la segunda
/// el instante en que se digito. Un lote movido la semana pasada y cargado hoy tiene las dos
/// distintas, y la que corresponde mostrar (y la que usa el Reporte Diario de Costos de POSTURA)
/// es la primera. Es nullable porque la columna lo es: filas anteriores a la migracion
/// <c>20260831170000_FechaTrasladoLote</c> que no hubieran alcanzado el backfill valen <c>null</c>,
/// y ahi el consumidor cae a <see cref="CreatedAt"/>.
///
/// <b><see cref="CreatedByUserName"/> es nullable</b> porque <see cref="CreatedByUserId"/> es la
/// cedula del usuario y no siempre hay una fila que la tenga (ver
/// <c>HistorialTrasladoLoteCalculos</c>). El id crudo viaja igual, para diagnostico.
/// </remarks>
public sealed record HistorialTrasladoLoteDto(
    int Id,
    int LoteOriginalId,
    int LoteNuevoId,
    int GranjaOrigenId,
    string GranjaOrigenNombre,
    int GranjaDestinoId,
    string GranjaDestinoNombre,
    string? NucleoDestinoId,
    string? NucleoDestinoNombre,
    string? GalponDestinoId,
    string? GalponDestinoNombre,
    string? Observaciones,
    int CreatedByUserId,
    string? CreatedByUserName,
    DateTime CreatedAt,
    DateOnly? FechaTraslado
);
