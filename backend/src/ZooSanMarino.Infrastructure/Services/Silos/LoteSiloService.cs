// src/ZooSanMarino.Infrastructure/Services/Silos/LoteSiloService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// De qué silos consume un lote. Se cuelga de <c>lotes.lote_id</c> (el maestro) para que la
/// asignación sobreviva al cierre del levante y siga valiendo en producción.
/// </summary>
public class LoteSiloService : ILoteSiloService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _current;
    private readonly IFarmSiloService _farmSilos;

    public LoteSiloService(ZooSanMarinoContext ctx, ICurrentUser current, IFarmSiloService farmSilos)
    {
        _ctx = ctx;
        _current = current;
        _farmSilos = farmSilos;
    }

    /// <summary>Ubicación del lote, validando que sea de la empresa activa (fail-closed).</summary>
    private async Task<(int CompanyId, int GranjaId, string? NucleoId, string? GalponId)> EnsureLoteAsync(int loteId, CancellationToken ct)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteId && l.DeletedAt == null)
            .Select(l => new { l.CompanyId, l.GranjaId, l.NucleoId, l.GalponId })
            .FirstOrDefaultAsync(ct);

        if (lote is null)
            throw new InvalidOperationException($"El lote {loteId} no existe.");
        if (lote.CompanyId != _current.CompanyId)
            throw new InvalidOperationException($"El lote {loteId} no pertenece a la empresa activa.");

        return (lote.CompanyId, lote.GranjaId, lote.NucleoId, lote.GalponId);
    }

    public async Task<IEnumerable<LoteSiloDto>> GetByLoteAsync(int loteId, CancellationToken ct = default)
    {
        await EnsureLoteAsync(loteId, ct);

        var filas = await (
            from ls in _ctx.LoteSilos.AsNoTracking()
            where ls.LoteId == loteId && ls.Activo
            join fs in _ctx.FarmSilos.AsNoTracking() on ls.FarmSiloId equals fs.Id
            where fs.DeletedAt == null
            join sc in _ctx.SiloCatalogo.AsNoTracking() on fs.SiloCatalogoId equals sc.Id into scg
            from cat in scg.DefaultIfEmpty()
            select new { ls, fs, Numero = cat != null ? cat.Numero : (int?)null }
        ).ToListAsync(ct);

        return filas
            .Select(x => new LoteSiloDto(x.ls.Id, x.ls.LoteId, x.fs.Id, x.fs.Nombre, x.fs.Tipo, x.Numero, x.ls.Activo))
            .OrderBy(d => SiloCalculos.ClaveOrden(d.SiloTipo, d.SiloNumero, d.SiloNombre).Grupo)
            .ThenBy(d => SiloCalculos.ClaveOrden(d.SiloTipo, d.SiloNumero, d.SiloNombre).Numero)
            .ThenBy(d => d.SiloNombre)
            .ToList();
    }

    public async Task<IEnumerable<FarmSiloDto>> GetDisponiblesAsync(int loteId, CancellationToken ct = default)
    {
        var (_, granjaId, nucleoId, galponId) = await EnsureLoteAsync(loteId, ct);

        var todosDeLaGranja = (await _farmSilos.GetAsync(granjaId, soloActivos: true, ct)).ToList();

        // Sin galpón no hay a qué restringir: se ofrecen todos los de la granja.
        if (string.IsNullOrWhiteSpace(galponId)) return todosDeLaGranja;

        var galpon = galponId!.Trim();
        var nucleo = nucleoId?.Trim();

        var delGalpon = await _ctx.GalponSilos.AsNoTracking()
            .Where(gs => gs.GranjaId == granjaId
                      && gs.GalponId == galpon
                      && (nucleo == null || gs.NucleoId == nucleo)
                      && gs.Activo)
            .Select(gs => gs.FarmSiloId)
            .ToListAsync(ct);

        // Galpón sin silos asignados ⇒ se ofrecen todos los de la granja. Devolver vacío dejaría al
        // usuario sin poder registrar consumo y sin ninguna pista de por qué.
        if (delGalpon.Count == 0) return todosDeLaGranja;

        var permitidos = delGalpon.ToHashSet();
        return todosDeLaGranja.Where(s => permitidos.Contains(s.Id)).ToList();
    }

    public async Task<IEnumerable<LoteSiloDto>> AsignarAsync(int loteId, AsignarSilosDto dto, CancellationToken ct = default)
    {
        var (companyId, granjaId, _, _) = await EnsureLoteAsync(loteId, ct);
        var pedidos = (dto.FarmSiloIds ?? Array.Empty<int>()).Distinct().ToHashSet();

        if (pedidos.Count > 0)
        {
            // El silo tiene que ser de la granja del lote. No se restringe a los del galpón a
            // propósito: el negocio reasigna silos cuando uno se queda sin alimento, y bloquearlo
            // acá obligaría a tocar antes la configuración del galpón para resolver una urgencia.
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
                    $"Estos silos no existen o no pertenecen a la granja del lote: {string.Join(", ", ajenos)}.");
        }

        var actuales = await _ctx.LoteSilos.Where(ls => ls.LoteId == loteId).ToListAsync(ct);
        var ahora = DateTime.UtcNow;

        foreach (var siloId in pedidos)
        {
            var fila = actuales.FirstOrDefault(a => a.FarmSiloId == siloId);
            if (fila is null)
            {
                _ctx.LoteSilos.Add(new LoteSilo
                {
                    CompanyId = companyId,
                    LoteId = loteId,
                    FarmSiloId = siloId,
                    Activo = true,
                    CreatedAt = ahora,
                    CreatedByUserId = _current.UserGuid
                });
            }
            else if (!fila.Activo)
            {
                fila.Activo = true;
            }
        }

        // Desactivar los que ya no se piden. Los consumos YA registrados conservan su silo: cada
        // movimiento guarda el suyo y no depende de esta tabla.
        foreach (var sobra in actuales.Where(a => a.Activo && !pedidos.Contains(a.FarmSiloId)))
            sobra.Activo = false;

        await _ctx.SaveChangesAsync(ct);
        return await GetByLoteAsync(loteId, ct);
    }
}
