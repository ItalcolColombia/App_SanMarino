// src/ZooSanMarino.Infrastructure/Services/LoteGalponService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;
public class LoteGalponService : ILoteGalponService
{
    readonly ZooSanMarinoContext _ctx;
    public LoteGalponService(ZooSanMarinoContext ctx) => _ctx = ctx;

    public async Task<IEnumerable<LoteGalponDto>> GetAllAsync() =>
        await _ctx.LoteGalpones
           .Select(x => new LoteGalponDto(
               x.LoteId, x.ReproductoraId, x.GalponId, x.M, x.H))
           .ToListAsync();

    public async Task<LoteGalponDto?> GetByIdAsync(int loteId, string repId, string galponId) =>  // Changed from string to int
        await _ctx.LoteGalpones
           .Where(x => x.LoteId == loteId  // Changed from string comparison
                    && x.ReproductoraId == repId
                    && x.GalponId == galponId)
           .Select(x => new LoteGalponDto(
               x.LoteId, x.ReproductoraId, x.GalponId, x.M, x.H))
           .SingleOrDefaultAsync();

    public async Task<LoteGalponDto> CreateAsync(CreateLoteGalponDto dto)
    {
        var ent = new LoteGalpon {
            LoteId          = dto.LoteId,
            ReproductoraId  = dto.ReproductoraId,
            GalponId        = dto.GalponId,
            M               = dto.M,
            H               = dto.H
        };
        _ctx.LoteGalpones.Add(ent);
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(ent.LoteId, ent.ReproductoraId, ent.GalponId)!;
    }

    public async Task<LoteGalponDto?> UpdateAsync(UpdateLoteGalponDto dto)
    {
        var ent = await _ctx.LoteGalpones.FindAsync(dto.LoteId, dto.ReproductoraId, dto.GalponId);
        if (ent is null) return null;
        ent.M = dto.M; ent.H = dto.H;
        await _ctx.SaveChangesAsync();
        return await GetByIdAsync(ent.LoteId, ent.ReproductoraId, ent.GalponId)!;
    }

    public async Task<bool> DeleteAsync(int loteId, string repId, string galponId)  // Changed from string to int
    {
        var ent = await _ctx.LoteGalpones.FindAsync(loteId, repId, galponId);  // Changed from string to int
        if (ent is null) return false;
        _ctx.LoteGalpones.Remove(ent);
        await _ctx.SaveChangesAsync();
        return true;
    }
}
