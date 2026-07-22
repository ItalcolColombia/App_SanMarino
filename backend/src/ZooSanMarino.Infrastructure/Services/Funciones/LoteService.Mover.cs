// file: src/ZooSanMarino.Infrastructure/Services/Funciones/LoteService.Mover.cs
// Reubicación (mover) de un lote. Valida destino y delega la cascada transaccional a fn_mover_lote.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;        // MoverLoteDto
using ZooSanMarino.Application.DTOs.Lotes;  // LoteDetailDto

namespace ZooSanMarino.Infrastructure.Services;

public partial class LoteService
{
    /// <inheritdoc />
    public async Task<LoteDetailDto?> MoverUbicacionAsync(MoverLoteDto dto)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException("El lote no existe o no pertenece a la compañía.");

        await EnsureFarmExists(dto.GranjaDestinoId, companyId);

        string? nucleoDest = string.IsNullOrWhiteSpace(dto.NucleoDestinoId) ? null : dto.NucleoDestinoId.Trim();
        string? galponDest = string.IsNullOrWhiteSpace(dto.GalponDestinoId) ? null : dto.GalponDestinoId.Trim();

        // Galpón destino: debe existir, pertenecer a la granja destino y (si se indicó) al núcleo destino.
        // Si no viene núcleo, lo derivamos del galpón.
        if (galponDest is not null)
        {
            var g = await _ctx.Galpones.AsNoTracking()
                .SingleOrDefaultAsync(x => x.GalponId == galponDest && x.CompanyId == companyId);
            if (g is null)
                throw new InvalidOperationException("El galpón destino no existe o no pertenece a la compañía.");
            if (g.GranjaId != dto.GranjaDestinoId)
                throw new InvalidOperationException("El galpón destino no pertenece a la granja destino.");
            if (nucleoDest is not null && !string.Equals(g.NucleoId, nucleoDest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El galpón destino no pertenece al núcleo destino indicado.");
            nucleoDest ??= g.NucleoId;
        }

        // Núcleo destino: debe existir en la granja destino.
        if (nucleoDest is not null)
        {
            var existe = await _ctx.Nucleos.AsNoTracking()
                .AnyAsync(x => x.NucleoId == nucleoDest && x.GranjaId == dto.GranjaDestinoId && x.CompanyId == companyId);
            if (!existe)
                throw new InvalidOperationException("El núcleo destino no existe en la granja destino.");
        }

        // Cascada atómica (lotes + espejos de fase). No toca nombre/numeración del lote.
        await _ctx.Database.ExecuteSqlRawAsync(
            "SELECT public.fn_mover_lote({0}, {1}, {2}::varchar, {3}::varchar, {4})",
            dto.LoteId, dto.GranjaDestinoId,
            (object?)nucleoDest ?? DBNull.Value, (object?)galponDest ?? DBNull.Value,
            _current.UserId);

        return await GetByIdAsync(dto.LoteId);
    }
}
