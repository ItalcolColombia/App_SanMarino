using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.GestionVeterinaria;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class GestionVeterinariaService
{
    public async Task<IReadOnlyList<VeterinariaGranjaDto>> GetMiMapaAsync(CancellationToken ct = default)
    {
        var farmIds = await GetAssignedFarmIdsAsync(ct);
        if (farmIds.Count == 0) return Array.Empty<VeterinariaGranjaDto>();

        var farms = await _ctx.Farms.AsNoTracking()
            .Where(x => farmIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Latitud, x.Longitud })
            .ToListAsync(ct);
        var salida = new List<VeterinariaGranjaDto>(farms.Count);

        foreach (var farm in farms)
        {
            var acceso = await GetAccesoAsync(farm.Id, ct);
            var nucleos = await _ctx.Nucleos.AsNoTracking()
                .Where(x => x.GranjaId == farm.Id && x.DeletedAt == null)
                .OrderBy(x => x.NucleoNombre)
                .Select(x => new { x.NucleoId, x.NucleoNombre })
                .ToListAsync(ct);
            var galpones = await _ctx.Galpones.AsNoTracking()
                .Where(x => x.GranjaId == farm.Id && x.DeletedAt == null)
                .OrderBy(x => x.GalponNombre)
                .Select(x => new { x.GalponId, x.GalponNombre, x.NucleoId })
                .ToListAsync(ct);
            var lotes = await _ctx.Lotes.AsNoTracking()
                .Where(x => x.GranjaId == farm.Id && x.DeletedAt == null && x.LoteId != null)
                .OrderBy(x => x.LoteNombre)
                .Select(x => new { Id = x.LoteId!.Value, Nombre = x.LoteNombre, x.Fase, x.NucleoId, x.GalponId })
                .ToListAsync(ct);

            if (!acceso.ScopeGlobal)
            {
                nucleos = nucleos.Where(x => acceso.NucleosVisibles.Contains(x.NucleoId)).ToList();
                galpones = galpones.Where(x => acceso.GalponesVisibles.Contains(x.GalponId)).ToList();
                lotes = lotes.Where(x => acceso.LotesPermitidos.Contains(x.Id)).ToList();
            }

            var galponIds = galpones.Select(x => x.GalponId).ToHashSet();
            var nucleoIds = nucleos.Select(x => x.NucleoId).ToHashSet();
            var nodos = nucleos.Select(n => new VeterinariaNucleoDto(
                n.NucleoId,
                n.NucleoNombre,
                galpones.Where(g => g.NucleoId == n.NucleoId)
                    .Select(g => new VeterinariaGalponDto(
                        g.GalponId,
                        g.GalponNombre,
                        lotes.Where(l => l.GalponId == g.GalponId)
                            .Select(l => new VeterinariaLoteDto(l.Id, l.Nombre, l.Fase)).ToList()))
                    .ToList(),
                lotes.Where(l => l.NucleoId == n.NucleoId
                                 && (l.GalponId is null || !galponIds.Contains(l.GalponId)))
                    .Select(l => new VeterinariaLoteDto(l.Id, l.Nombre, l.Fase)).ToList()))
                .ToList();

            var sinUbicacion = lotes
                .Where(l => (l.NucleoId is null || !nucleoIds.Contains(l.NucleoId))
                            && (l.GalponId is null || !galponIds.Contains(l.GalponId)))
                .Select(l => new VeterinariaLoteDto(l.Id, l.Nombre, l.Fase))
                .ToList();
            salida.Add(new VeterinariaGranjaDto(
                farm.Id, farm.Name, farm.Latitud, farm.Longitud, nodos, sinUbicacion,
                galpones.Count, lotes.Count));
        }

        return salida;
    }

    public async Task<VeterinariaResumenDto> GetResumenAsync(CancellationToken ct = default)
    {
        var granjas = await GetAssignedFarmIdsAsync(ct);
        if (granjas.Count == 0) return new VeterinariaResumenDto(0, 0, 0, 0);
        var ahora = DateTime.UtcNow;
        var visitas = await _ctx.VisitasTecnicas.AsNoTracking()
            .CountAsync(x => x.CompanyId == _current.CompanyId
                             && x.VeterinarioUserId == CurrentGuid
                             && x.DeletedAt == null
                             && x.Estado == EstadoVisitaTecnica.Programada
                             && x.FechaProgramada >= ahora, ct);
        var candidatas = await _ctx.TareasCampo.AsNoTracking()
            .Where(x => x.CompanyId == _current.CompanyId && granjas.Contains(x.FarmId)
                        && x.DeletedAt == null && x.Estado == EstadoTareaCampo.Pendiente)
            .ToListAsync(ct);
        var visibles = new List<TareaCampo>();
        foreach (var tarea in candidatas)
            if (await PuedeAccederTareaAsync(tarea, ct)) visibles.Add(tarea);
        return new VeterinariaResumenDto(
            granjas.Count,
            visitas,
            visibles.Count,
            visibles.Count(x => x.FechaFin.Date < ahora.Date));
    }
}
