// src/ZooSanMarino.Infrastructure/Services/Silos/GalponSiloService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Qué silos alimentan a un galpón. <b>Navegación, no contención</b>: el stock vive en el silo (que
/// es de la granja), y un mismo silo puede estar en varios galpones.
/// </summary>
public class GalponSiloService : IGalponSiloService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IFarmSiloService _farmSilos;

    public GalponSiloService(ZooSanMarinoContext ctx, ICurrentUser current, IFarmSiloService farmSilos)
    {
        _ctx = ctx;
        _current = current;
        _farmSilos = farmSilos;
    }

    private async Task<int> EnsureGranjaDeEmpresaAsync(int granjaId, CancellationToken ct)
    {
        var companyId = await _ctx.Farms.AsNoTracking()
            .Where(f => f.Id == granjaId && f.DeletedAt == null)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (companyId is null)
            throw new InvalidOperationException($"La granja {granjaId} no existe.");
        if (companyId.Value != _current.CompanyId)
            throw new InvalidOperationException($"La granja {granjaId} no pertenece a la empresa activa.");

        return companyId.Value;
    }

    public async Task<IEnumerable<GalponSiloDto>> GetAsync(int granjaId, string? nucleoId = null, string? galponId = null, CancellationToken ct = default)
    {
        var companyId = _current.CompanyId;

        var q = from gs in _ctx.GalponSilos.AsNoTracking()
                where gs.CompanyId == companyId && gs.GranjaId == granjaId && gs.Activo
                join fs in _ctx.FarmSilos.AsNoTracking() on gs.FarmSiloId equals fs.Id
                where fs.DeletedAt == null
                join sc in _ctx.SiloCatalogo.AsNoTracking() on fs.SiloCatalogoId equals sc.Id into scg
                from cat in scg.DefaultIfEmpty()
                select new { gs, fs, Numero = cat != null ? cat.Numero : (int?)null };

        if (!string.IsNullOrWhiteSpace(nucleoId)) { var n = nucleoId!.Trim(); q = q.Where(x => x.gs.NucleoId == n); }
        if (!string.IsNullOrWhiteSpace(galponId)) { var g = galponId!.Trim(); q = q.Where(x => x.gs.GalponId == g); }

        var filas = await q.ToListAsync(ct);

        return filas
            .Select(x => new GalponSiloDto(
                x.gs.Id, x.gs.GranjaId, x.gs.NucleoId, x.gs.GalponId,
                x.fs.Id, x.fs.Nombre, x.fs.Tipo, x.Numero, x.gs.Activo))
            .OrderBy(d => SiloCalculos.ClaveOrden(d.SiloTipo, d.SiloNumero, d.SiloNombre).Grupo)
            .ThenBy(d => SiloCalculos.ClaveOrden(d.SiloTipo, d.SiloNumero, d.SiloNombre).Numero)
            .ThenBy(d => d.SiloNombre)
            .ToList();
    }

    public async Task<IEnumerable<FarmSiloDto>> GetDisponiblesAsync(int granjaId, CancellationToken ct = default)
    {
        await EnsureGranjaDeEmpresaAsync(granjaId, ct);
        return await _farmSilos.GetAsync(granjaId, soloActivos: true, ct);
    }

    public async Task<IEnumerable<GalponSiloDto>> AsignarAsync(int granjaId, string nucleoId, string galponId, AsignarSilosDto dto, CancellationToken ct = default)
    {
        var companyId = await EnsureGranjaDeEmpresaAsync(granjaId, ct);

        var nucleo = (nucleoId ?? string.Empty).Trim();
        var galpon = (galponId ?? string.Empty).Trim();
        if (nucleo.Length == 0 || galpon.Length == 0)
            throw new InvalidOperationException("Se requieren núcleo y galpón para asignar silos.");

        var existeGalpon = await _ctx.Galpones.AsNoTracking()
            .AnyAsync(g => g.GalponId == galpon && g.NucleoId == nucleo && g.GranjaId == granjaId && g.DeletedAt == null, ct);
        if (!existeGalpon)
            throw new InvalidOperationException($"El galpón '{galpon}' del núcleo '{nucleo}' no existe en la granja {granjaId}.");

        var pedidos = (dto.FarmSiloIds ?? Array.Empty<int>()).Distinct().ToHashSet();

        // Invariante: el silo tiene que ser de la MISMA granja. No se puede garantizar con una FK
        // simple (farm_silos.granja_id no es parte de su PK), así que se valida acá.
        if (pedidos.Count > 0)
        {
            var validos = await _ctx.FarmSilos.AsNoTracking()
                .Where(fs => pedidos.Contains(fs.Id)
                          && fs.GranjaId == granjaId
                          && fs.CompanyId == companyId
                          && fs.DeletedAt == null)
                .Select(fs => fs.Id)
                .ToListAsync(ct);

            var ajenos = pedidos.Except(validos).ToList();
            if (ajenos.Count > 0)
                throw new InvalidOperationException(
                    $"Estos silos no existen o no pertenecen a la granja {granjaId}: {string.Join(", ", ajenos)}.");
        }

        var actuales = await _ctx.GalponSilos
            .Where(gs => gs.GranjaId == granjaId && gs.NucleoId == nucleo && gs.GalponId == galpon)
            .ToListAsync(ct);

        var ahora = DateTime.UtcNow;

        // Reactivar / crear los pedidos.
        foreach (var siloId in pedidos)
        {
            var fila = actuales.FirstOrDefault(a => a.FarmSiloId == siloId);
            if (fila is null)
            {
                _ctx.GalponSilos.Add(new GalponSilo
                {
                    CompanyId = companyId,
                    GranjaId = granjaId,
                    NucleoId = nucleo,
                    GalponId = galpon,
                    FarmSiloId = siloId,
                    Activo = true,
                    CreatedAt = ahora,
                    CreatedByUserId = _current.UserGuid
                });
            }
            else if (!fila.Activo)
            {
                // Se reactiva la fila en vez de insertar otra: el índice único no distingue activos.
                fila.Activo = true;
            }
        }

        // Desactivar (no borrar) los que ya no se piden: conserva quién los había asignado.
        foreach (var sobra in actuales.Where(a => a.Activo && !pedidos.Contains(a.FarmSiloId)))
            sobra.Activo = false;

        await _ctx.SaveChangesAsync(ct);
        return await GetAsync(granjaId, nucleo, galpon, ct);
    }
}
