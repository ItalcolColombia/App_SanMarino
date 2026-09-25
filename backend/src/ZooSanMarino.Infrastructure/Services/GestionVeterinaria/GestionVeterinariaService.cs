using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.GestionVeterinaria;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>Ancla del servicio; la lógica se reparte por mapa, visitas y tareas.</summary>
public partial class GestionVeterinariaService : IGestionVeterinariaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly ILocationScopeResolver _scopeResolver;

    public GestionVeterinariaService(
        ZooSanMarinoContext ctx,
        ICurrentUser current,
        ILocationScopeResolver scopeResolver)
    {
        _ctx = ctx;
        _current = current;
        _scopeResolver = scopeResolver;
    }

    private Guid CurrentGuid => _current.UserGuid
        ?? throw new UnauthorizedAccessException("La sesión no contiene un usuario válido.");

    private Task<List<int>> GetAssignedFarmIdsAsync(CancellationToken ct) =>
        _ctx.UserFarms.AsNoTracking()
            .Where(x => x.UserId == CurrentGuid
                        && x.Farm.CompanyId == _current.CompanyId
                        && x.Farm.DeletedAt == null
                        && x.Farm.Status == "A")
            .Select(x => x.FarmId)
            .Distinct()
            .ToListAsync(ct);

    private async Task<GestionVeterinariaCalculos.UbicacionAcceso> GetAccesoAsync(
        int farmId, CancellationToken ct)
    {
        var tieneGranja = await _ctx.UserFarms.AsNoTracking()
            .AnyAsync(x => x.UserId == CurrentGuid
                           && x.FarmId == farmId
                           && x.Farm.CompanyId == _current.CompanyId
                           && x.Farm.DeletedAt == null
                           && x.Farm.Status == "A", ct);
        if (!tieneGranja)
            return new GestionVeterinariaCalculos.UbicacionAcceso(
                false, false, new HashSet<string>(), new HashSet<string>(), new HashSet<int>());

        var scope = await _scopeResolver.GetScopeAsync(farmId);
        return new GestionVeterinariaCalculos.UbicacionAcceso(
            true,
            scope.IsGlobal,
            scope.NucleosVisibles,
            scope.GalponesVisibles,
            scope.LotesPermitidos);
    }

    private async Task<(string? NucleoId, string? GalponId, int? LoteId)> ValidarUbicacionAsync(
        int farmId, string? nucleoId, string? galponId, int? loteId, CancellationToken ct)
    {
        var acceso = await GetAccesoAsync(farmId, ct);
        if (!acceso.TieneGranja)
            throw new UnauthorizedAccessException("La granja no está asignada al usuario actual.");

        var nucleo = string.IsNullOrWhiteSpace(nucleoId) ? null : nucleoId.Trim();
        var galpon = string.IsNullOrWhiteSpace(galponId) ? null : galponId.Trim();

        if (loteId.HasValue)
        {
            var lote = await _ctx.Lotes.AsNoTracking()
                .Where(x => x.LoteId == loteId.Value && x.GranjaId == farmId && x.DeletedAt == null)
                .Select(x => new { x.NucleoId, x.GalponId })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("El lote no existe en la granja seleccionada.");
            if (nucleo is not null && !string.Equals(nucleo, lote.NucleoId, StringComparison.Ordinal))
                throw new InvalidOperationException("El lote no pertenece al núcleo seleccionado.");
            if (galpon is not null && !string.Equals(galpon, lote.GalponId, StringComparison.Ordinal))
                throw new InvalidOperationException("El lote no pertenece al galpón seleccionado.");
            nucleo ??= lote.NucleoId;
            galpon ??= lote.GalponId;
        }
        else if (galpon is not null)
        {
            var galponDb = await _ctx.Galpones.AsNoTracking()
                .Where(x => x.GalponId == galpon && x.GranjaId == farmId && x.DeletedAt == null)
                .Select(x => new { x.NucleoId })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("El galpón no existe en la granja seleccionada.");
            if (nucleo is not null && !string.Equals(nucleo, galponDb.NucleoId, StringComparison.Ordinal))
                throw new InvalidOperationException("El galpón no pertenece al núcleo seleccionado.");
            nucleo ??= galponDb.NucleoId;
        }
        else if (nucleo is not null)
        {
            var existe = await _ctx.Nucleos.AsNoTracking()
                .AnyAsync(x => x.NucleoId == nucleo && x.GranjaId == farmId && x.DeletedAt == null, ct);
            if (!existe) throw new InvalidOperationException("El núcleo no existe en la granja seleccionada.");
        }

        if (!GestionVeterinariaCalculos.PuedeAcceder(acceso, nucleo, galpon, loteId))
            throw new UnauthorizedAccessException("La ubicación seleccionada está fuera del alcance asignado.");

        return (nucleo, galpon, loteId);
    }

    private async Task<bool> PuedeAccederTareaAsync(TareaCampo tarea, CancellationToken ct)
    {
        if (tarea.CompanyId != _current.CompanyId) return false;
        var acceso = await GetAccesoAsync(tarea.FarmId, ct);
        return GestionVeterinariaCalculos.PuedeAcceder(
            acceso, tarea.NucleoId, tarea.GalponId, tarea.LoteId);
    }

    private static string NombreUsuario(User? user) => user is null
        ? "Usuario"
        : $"{user.firstName} {user.surName}".Trim();
}
