// file: src/ZooSanMarino.Infrastructure/Services/LoteReproductoraService.cs
using Microsoft.EntityFrameworkCore;
using System.Linq;
using AppInterfaces = ZooSanMarino.Application.Interfaces; // ILoteReproductoraService, ICurrentUser
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LoteReproductoraService : AppInterfaces.ILoteReproductoraService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly AppInterfaces.ICurrentUser _current;

    public LoteReproductoraService(ZooSanMarinoContext ctx, AppInterfaces.ICurrentUser current)
    {
        _ctx = ctx;
        _current = current;
    }

    // ----------------------------------------------------
    // Helper de mapeo (evita repetir selects manuales)
    // ----------------------------------------------------
    private static LoteReproductoraDto Map(LoteReproductora x) => new(
        x.LoteId,
        x.ReproductoraId,
        x.NombreLote,
        x.FechaEncasetamiento,
        x.M, x.H, x.Mixtas,
        x.MortCajaH, x.MortCajaM, x.UnifH, x.UnifM,
        x.PesoInicialM, x.PesoInicialH, x.PesoMixto
    );

    // ----------------------------------------------------
    // LISTADO (tenant-safe; opcional por LoteId)
    // ----------------------------------------------------
    public async Task<IEnumerable<LoteReproductoraDto>> GetAllAsync(string? loteId = null)
    {
        // Convertir lote_id de integer a string para el JOIN
        var q =
            from lr in _ctx.LoteReproductoras.AsNoTracking()
            join l in _ctx.Lotes.AsNoTracking() on lr.LoteId equals l.LoteId.ToString()
            where l.CompanyId == _current.CompanyId && l.DeletedAt == null
            select lr;

        if (!string.IsNullOrWhiteSpace(loteId))
            q = q.Where(lr => lr.LoteId == loteId);

        var list = await q
            .OrderBy(lr => lr.LoteId)
            .ThenBy(lr => lr.ReproductoraId)
            .ToListAsync();

        return list.Select(Map).ToList();
    }

    // ----------------------------------------------------
    // DETALLE (tenant-safe)
    // ----------------------------------------------------
    public async Task<LoteReproductoraDto?> GetByIdAsync(string loteId, string repId)
    {
        var ent =
            await (from lr in _ctx.LoteReproductoras.AsNoTracking()
                   join l in _ctx.Lotes.AsNoTracking() on lr.LoteId equals l.LoteId.ToString()
                   where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                      && lr.LoteId == loteId && lr.ReproductoraId == repId
                   select lr).SingleOrDefaultAsync();

        return ent is null ? null : Map(ent);
    }

    // ----------------------------------------------------
    // CREAR (valida tenant + duplicado PK compuesta)
    // ----------------------------------------------------
    public async Task<LoteReproductoraDto> CreateAsync(CreateLoteReproductoraDto dto)
    {
        await EnsureLoteExistsForTenant(dto.LoteId);

        // Duplicado PK compuesta
        var duplicado = await _ctx.LoteReproductoras.AsNoTracking()
            .AnyAsync(x => x.LoteId == dto.LoteId && x.ReproductoraId == dto.ReproductoraId);
        if (duplicado)
            throw new InvalidOperationException($"Ya existe {dto.LoteId}/{dto.ReproductoraId}.");

        // Normalización / saneo básico
        var ent = new LoteReproductora
        {
            LoteId               = dto.LoteId,
            ReproductoraId       = (dto.ReproductoraId ?? string.Empty).Trim(),
            NombreLote           = (dto.NombreLote ?? string.Empty).Trim(),
            // Fechas en UTC para consistencia
            FechaEncasetamiento  = dto.FechaEncasetamiento?.ToUniversalTime(),

            // Cantidades: default 0 si null (DB tiene checks de no-negativo)
            M                    = dto.M         ?? 0,
            H                    = dto.H         ?? 0,
            AvesInicioHembras    = dto.H         ?? 0,
            AvesInicioMachos     = dto.M         ?? 0,
            Mixtas               = dto.Mixtas    ?? 0,
            MortCajaH            = dto.MortCajaH ?? 0,
            MortCajaM            = dto.MortCajaM ?? 0,
            UnifH                = dto.UnifH     ?? 0,
            UnifM                = dto.UnifM     ?? 0,

            // Pesos: respeta null; precisión/escala la maneja EF config
            PesoInicialM         = dto.PesoInicialM,
            PesoInicialH         = dto.PesoInicialH,
            PesoMixto            = dto.PesoMixto
        };

        _ctx.LoteReproductoras.Add(ent);
        await _ctx.SaveChangesAsync();

        // Reconsulta opcional si esperas triggers/cols computadas:
        // return (await GetByIdAsync(ent.LoteId, ent.ReproductoraId))!;
        return Map(ent);
    }

    // ----------------------------------------------------
    // CREAR BULK (transaccional, sin N+1)
    // ----------------------------------------------------
    public async Task<IEnumerable<LoteReproductoraDto>> CreateBulkAsync(IEnumerable<CreateLoteReproductoraDto> dtos)
    {
        var list = dtos?.ToList() ?? new();
        if (list.Count == 0) return Enumerable.Empty<LoteReproductoraDto>();

        // Todos al mismo Lote (requisito de negocio actual)
        var distinctLotes = list.Select(x => x.LoteId).Distinct().ToList();
        if (distinctLotes.Count != 1)
            throw new InvalidOperationException("Todos los registros bulk deben pertenecer al mismo LoteId.");

        var loteId = distinctLotes[0];
        await EnsureLoteExistsForTenant(loteId);

        // Duplicados dentro del propio payload
        var dupInPayload = list
            .GroupBy(x => new { x.LoteId, Rep = (x.ReproductoraId ?? string.Empty).Trim() })
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key.LoteId}/{g.Key.Rep}")
            .ToList();
        if (dupInPayload.Count > 0)
            throw new InvalidOperationException($"El payload contiene duplicados: {string.Join(", ", dupInPayload)}");

        // Duplicados ya existentes en DB (consulta única)
        var incomingKeys = list
            .Select(x => new { x.LoteId, Rep = (x.ReproductoraId ?? string.Empty).Trim() })
            .Distinct()
            .ToList();

        var existing = await _ctx.LoteReproductoras.AsNoTracking()
            .Where(x => x.LoteId == loteId)
            .Select(x => new { x.LoteId, Rep = x.ReproductoraId })
            .ToListAsync();

        var existingSet = existing.Select(x => (x.LoteId, x.Rep)).ToHashSet();
        var dupKeys = incomingKeys.Where(k => existingSet.Contains((k.LoteId, k.Rep))).ToList();
        if (dupKeys.Count > 0)
        {
            var desc = string.Join(", ", dupKeys.Select(k => $"{k.LoteId}/{k.Rep}"));
            throw new InvalidOperationException($"Duplicados existentes: {desc}");
        }

        await using var tx = await _ctx.Database.BeginTransactionAsync();
        try
        {
            var entities = list.Select(dto => new LoteReproductora
            {
                LoteId               = dto.LoteId,
                ReproductoraId       = (dto.ReproductoraId ?? string.Empty).Trim(),
                NombreLote           = (dto.NombreLote ?? string.Empty).Trim(),
                FechaEncasetamiento  = dto.FechaEncasetamiento?.ToUniversalTime(),
                M                    = dto.M         ?? 0,
                H                    = dto.H         ?? 0,
                AvesInicioHembras    = dto.H         ?? 0,
                AvesInicioMachos     = dto.M         ?? 0,
                Mixtas               = dto.Mixtas    ?? 0,
                MortCajaH            = dto.MortCajaH ?? 0,
                MortCajaM            = dto.MortCajaM ?? 0,
                UnifH                = dto.UnifH     ?? 0,
                UnifM                = dto.UnifM     ?? 0,
                PesoInicialM         = dto.PesoInicialM,
                PesoInicialH         = dto.PesoInicialH,
                PesoMixto            = dto.PesoMixto
            }).ToList();

            _ctx.LoteReproductoras.AddRange(entities);
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();

            // Devolver lo del lote (como hacía tu versión anterior)
            return await GetAllAsync(loteId);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ----------------------------------------------------
    // UPDATE (tenant-safe)
    // ----------------------------------------------------
    public async Task<LoteReproductoraDto?> UpdateAsync(UpdateLoteReproductoraDto dto)
    {
        var ent =
            await (from lr in _ctx.LoteReproductoras
                   join l in _ctx.Lotes on lr.LoteId equals l.LoteId.ToString()
                   where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                      && lr.LoteId == dto.LoteId && lr.ReproductoraId == dto.ReproductoraId
                   select lr).SingleOrDefaultAsync();

        if (ent is null) return null;

        ent.NombreLote          = (dto.NombreLote ?? string.Empty).Trim();
        ent.FechaEncasetamiento = dto.FechaEncasetamiento?.ToUniversalTime();

        ent.M                   = dto.M         ?? 0;
        ent.H                   = dto.H         ?? 0;
        ent.Mixtas              = dto.Mixtas    ?? 0;
        ent.MortCajaH           = dto.MortCajaH ?? 0;
        ent.MortCajaM           = dto.MortCajaM ?? 0;
        ent.UnifH               = dto.UnifH     ?? 0;
        ent.UnifM               = dto.UnifM     ?? 0;

        ent.PesoInicialM        = dto.PesoInicialM;
        ent.PesoInicialH        = dto.PesoInicialH;
        ent.PesoMixto           = dto.PesoMixto;

        await _ctx.SaveChangesAsync();
        return Map(ent);
    }

    // ----------------------------------------------------
    // DELETE (tenant-safe)
    // ----------------------------------------------------
    public async Task<bool> DeleteAsync(string loteId, string repId)
    {
        var ent =
            await (from lr in _ctx.LoteReproductoras
                   join l in _ctx.Lotes on lr.LoteId equals l.LoteId.ToString()
                   where l.CompanyId == _current.CompanyId && l.DeletedAt == null
                      && lr.LoteId == loteId && lr.ReproductoraId == repId
                   select lr).SingleOrDefaultAsync();

        if (ent is null) return false;

        _ctx.LoteReproductoras.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }

    // ----------------------------------------------------
    // OBTENER AVES DISPONIBLES DE UN LOTE
    // ----------------------------------------------------
    public async Task<AvesDisponiblesDto?> GetAvesDisponiblesAsync(string loteId)
    {
        // Convertir loteId string a int para buscar en Lotes
        if (!int.TryParse(loteId, out var loteIdInt))
        {
            return null;
        }

        // Validar que el lote existe y pertenece al tenant
        var lote = await _ctx.Lotes.AsNoTracking()
            .Where(l => l.LoteId == loteIdInt &&
                       l.CompanyId == _current.CompanyId &&
                       l.DeletedAt == null)
            .Select(l => new { l.HembrasL, l.MachosL, l.MortCajaH, l.MortCajaM })
            .SingleOrDefaultAsync();

        if (lote == null) return null;

        // Obtener mortalidad acumulada desde seguimientos de levante
        var mortalidad = await _ctx.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteIdInt)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                MortH = (int?)g.Sum(x => x.MortalidadHembras) ?? 0,
                MortM = (int?)g.Sum(x => x.MortalidadMachos) ?? 0
            })
            .SingleOrDefaultAsync();

        int mortH = mortalidad?.MortH ?? 0;
        int mortM = mortalidad?.MortM ?? 0;

        // Obtener aves ya asignadas a lotes reproductoras (loteId es string en lote_reproductoras)
        var asignadas = await _ctx.LoteReproductoras
            .AsNoTracking()
            .Where(lr => lr.LoteId == loteId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AsignadasH = (int?)g.Sum(x => x.H) ?? 0,
                AsignadasM = (int?)g.Sum(x => x.M) ?? 0
            })
            .SingleOrDefaultAsync();

        int asignadasH = asignadas?.AsignadasH ?? 0;
        int asignadasM = asignadas?.AsignadasM ?? 0;

        // Calcular aves disponibles
        int hembrasIniciales = lote.HembrasL ?? 0;
        int machosIniciales = lote.MachosL ?? 0;
        int mortCajaH = lote.MortCajaH ?? 0;
        int mortCajaM = lote.MortCajaM ?? 0;

        int hembrasDisponibles = Math.Max(0, hembrasIniciales - mortCajaH - mortH - asignadasH);
        int machosDisponibles = Math.Max(0, machosIniciales - mortCajaM - mortM - asignadasM);

        return new AvesDisponiblesDto
        {
            HembrasIniciales = hembrasIniciales,
            MachosIniciales = machosIniciales,
            MortalidadAcumuladaHembras = mortH,
            MortalidadAcumuladaMachos = mortM,
            MortCajaHembras = mortCajaH,
            MortCajaMachos = mortCajaM,
            AsignadasHembras = asignadasH,
            AsignadasMachos = asignadasM,
            HembrasDisponibles = hembrasDisponibles,
            MachosDisponibles = machosDisponibles
        };
    }

    // ----------------------------------------------------
    // Helpers
    // ----------------------------------------------------
    private async Task EnsureLoteExistsForTenant(string loteId)
    {
        // Convertir loteId string a int para buscar en Lotes
        if (!int.TryParse(loteId, out var loteIdInt))
        {
            throw new InvalidOperationException($"Lote '{loteId}' no es un ID válido.");
        }

        var ok = await _ctx.Lotes.AsNoTracking()
            .AnyAsync(l => l.LoteId == loteIdInt &&
                           l.CompanyId == _current.CompanyId &&
                           l.DeletedAt == null);
        if (!ok)
            throw new InvalidOperationException($"Lote '{loteId}' no existe o no pertenece a la compañía.");
    }
}
