// Vacunacion/Funciones/VacunacionCronogramaService.Filtros.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionCronogramaService
{
    /// <inheritdoc />
    public async Task<VacunacionFilterDataDto> GetFilterDataAsync(CancellationToken ct = default)
    {
        if (!_currentUser.UserGuid.HasValue)
            throw new UnauthorizedAccessException("Sesión inválida. Inicie sesión de nuevo.");

        var granjasDto = (await _farmService
            .GetAssignedFarmsForCompanyAsync(_currentUser.UserGuid.Value, _currentUser.CompanyId, _currentUser.PaisId)
            .ConfigureAwait(false)).ToList();
        var granjaIds = granjasDto.Select(f => f.Id).ToList();

        var lotes = new List<VacunacionLoteOpcionDto>();
        if (granjaIds.Count > 0)
        {
            var levante = await _ctx.LotePosturaLevante.AsNoTracking()
                .Where(x => x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null
                    && granjaIds.Contains(x.GranjaId) && x.LotePosturaLevanteId != null)
                .Select(x => new VacunacionLoteOpcionDto(
                    x.LotePosturaLevanteId!.Value, "Levante", x.LoteNombre, x.GranjaId, x.NucleoId, x.GalponId, x.FechaEncaset, x.EstadoCierre))
                .ToListAsync(ct);

            var produccion = await _ctx.LotePosturaProduccion.AsNoTracking()
                .Where(x => x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null
                    && granjaIds.Contains(x.GranjaId) && x.LotePosturaProduccionId != null)
                .Select(x => new VacunacionLoteOpcionDto(
                    x.LotePosturaProduccionId!.Value, "Produccion", x.LoteNombre, x.GranjaId, x.NucleoId, x.GalponId, x.FechaEncaset, x.EstadoCierre))
                .ToListAsync(ct);

            var engorde = await _ctx.LoteAveEngorde.AsNoTracking()
                .Where(x => x.CompanyId == _currentUser.CompanyId && x.DeletedAt == null
                    && granjaIds.Contains(x.GranjaId) && x.LoteAveEngordeId != null)
                .Select(x => new VacunacionLoteOpcionDto(
                    x.LoteAveEngordeId!.Value, "Engorde", x.LoteNombre, x.GranjaId, x.NucleoId, x.GalponId, x.FechaEncaset, x.EstadoOperativoLote))
                .ToListAsync(ct);

            lotes.AddRange(levante);
            lotes.AddRange(produccion);
            lotes.AddRange(engorde);
        }

        // Case-insensitive: el dato real trae "Vacuna"/"vacuna" mezclado (ver Fase 0 del plan).
        var vacunas = await _ctx.ItemInventario.AsNoTracking()
            .Where(x => x.CompanyId == _currentUser.CompanyId && x.Activo && EF.Functions.ILike(x.TipoItem, "vacuna"))
            .OrderBy(x => x.Nombre)
            .Select(x => new VacunacionVacunaOpcionDto(x.Id, x.Codigo, x.Nombre, x.Unidad))
            .ToListAsync(ct);

        return new VacunacionFilterDataDto(
            granjasDto, lotes.OrderByDescending(l => l.FechaEncaset).ToList(), vacunas);
    }
}
