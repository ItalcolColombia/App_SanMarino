// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteContableService.Filtros.cs
// Filtros disponibles para armar el selector del reporte contable.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteContableService
{
    public async Task<FiltrosContablesDto> GetFiltrosDisponiblesAsync(CancellationToken ct = default)
    {
        IQueryable<Lote> q = _ctx.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Include(l => l.LotePosturaBase)
            .Where(l => l.LotePadreId == null &&
                        l.CompanyId == _currentUser.CompanyId &&
                        l.DeletedAt == null);

        // Alcance granular: el árbol granja→núcleo→galpón→lote base se construye a partir de los
        // lotes, así que basta podar los lotes NO permitidos de las granjas restringidas: los
        // núcleos/galpones sin lotes visibles (y las granjas con scope vacío) desaparecen solos.
        // lote_id es PK global ⇒ la unión entre granjas es exacta. Sin restricciones no filtra nada.
        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count > 0)
        {
            var granjasRestringidas = restringidos.Keys.ToList();
            var lotesPermitidos = restringidos.SelectMany(kv => kv.Value.LotesPermitidos).ToList();
            q = q.Where(l => !granjasRestringidas.Contains(l.GranjaId) ||
                             (l.LoteId != null && lotesPermitidos.Contains(l.LoteId.Value)));
        }

        var lotes = await q
            .OrderBy(l => l.Farm.Name)
            .ThenBy(l => l.NucleoId)
            .ThenBy(l => l.GalponId)
            .ThenBy(l => l.LoteNombre)
            .ToListAsync(ct);

        var granjas = lotes
            .GroupBy(l => l.GranjaId)
            .Select(gGranja => new GranjaFiltroContableDto
            {
                GranjaId = gGranja.Key,
                GranjaNombre = gGranja.First().Farm?.Name ?? gGranja.Key.ToString(),
                Nucleos = gGranja
                    .GroupBy(l => l.NucleoId)
                    .Select(gNucleo => new NucleoFiltroContableDto
                    {
                        NucleoId = gNucleo.Key,
                        NucleoNombre = gNucleo.First().Nucleo?.NucleoNombre ?? gNucleo.Key ?? "(Sin núcleo)",
                        Galpones = gNucleo
                            .GroupBy(l => l.GalponId)
                            .Select(gGalpon => new GalponFiltroContableDto
                            {
                                GalponId = gGalpon.Key,
                                GalponNombre = gGalpon.First().Galpon?.GalponNombre ?? gGalpon.Key ?? "(Sin galpón)",
                                LotesBase = gGalpon
                                    .Select(l => new LoteBaseFiltroContableDto
                                    {
                                        LoteId = l.LoteId!.Value,
                                        LoteNombre = l.LotePosturaBase?.LoteNombre ?? l.LoteNombre,
                                        LotePosturaBaseId = l.LotePosturaBaseId,
                                        CodigoErp = l.LotePosturaBase?.CodigoErp ?? l.LoteErp
                                    })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        return new FiltrosContablesDto { Granjas = granjas };
    }
}
