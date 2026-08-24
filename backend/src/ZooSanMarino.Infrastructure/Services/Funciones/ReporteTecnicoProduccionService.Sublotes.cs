// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoProduccionService.Sublotes.cs
// Resolucion de sublotes de un lote base de produccion.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoProduccionService
{
    public async Task<List<string>> ObtenerSublotesAsync(string loteNombreBase, CancellationToken ct = default)
    {
        // Priorizar LPP (lote_postura_produccion)
        var sublotesLpp = await _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Where(l => l.LoteNombre != null && l.LoteNombre.StartsWith(loteNombreBase) &&
                       l.CompanyId == _currentUser.CompanyId &&
                       l.DeletedAt == null)
            .Select(l => l.LoteNombre!)
            .OrderBy(n => n)
            .ToListAsync(ct);

        if (sublotesLpp.Any())
        {
            return sublotesLpp
                .Select(n => ExtraerSublote(n) ?? n)
                .Distinct()
                .ToList();
        }

        var sublotesLegacy = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LoteNombre != null && l.LoteNombre.StartsWith(loteNombreBase) &&
                       l.CompanyId == _currentUser.CompanyId &&
                       l.DeletedAt == null)
            .Select(l => l.LoteNombre!)
            .OrderBy(n => n)
            .ToListAsync(ct);

        return sublotesLegacy
            .Select(n => ExtraerSublote(n) ?? n)
            .Distinct()
            .ToList();
    }
}
