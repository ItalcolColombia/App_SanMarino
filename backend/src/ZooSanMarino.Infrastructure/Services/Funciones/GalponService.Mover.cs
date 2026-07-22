// file: src/ZooSanMarino.Infrastructure/Services/Funciones/GalponService.Mover.cs
// Mover un galpón (y todo su contenido) a otro núcleo/granja. Cascada transaccional en fn_mover_galpon.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs; // MoverGalponDto, MoverResultDto

namespace ZooSanMarino.Infrastructure.Services;

public partial class GalponService
{
    /// <inheritdoc />
    public async Task<MoverResultDto> MoverAsync(MoverGalponDto dto)
    {
        var companyId = await GetEffectiveCompanyIdAsync();

        var galpon = await _ctx.Galpones.AsNoTracking()
            .SingleOrDefaultAsync(g => g.GalponId == dto.GalponId && g.CompanyId == companyId);
        if (galpon is null || galpon.DeletedAt != null)
            throw new InvalidOperationException("El galpón no existe o no pertenece a la compañía.");

        var nucleoDest = (dto.NucleoDestinoId ?? string.Empty).Trim();
        if (nucleoDest.Length == 0)
            throw new InvalidOperationException("Debe indicar el núcleo destino.");

        // Destino válido (misma empresa) y coherente (núcleo ∈ granja destino).
        await EnsureFarmExists(dto.GranjaDestinoId);
        await EnsureNucleoExists(nucleoDest, dto.GranjaDestinoId);

        // No-op explícito: si ya está en ese destino, no hay nada que mover.
        if (galpon.GranjaId == dto.GranjaDestinoId &&
            string.Equals(galpon.NucleoId, nucleoDest, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El galpón ya está en ese núcleo/granja.");

        // Impacto para el mensaje (lotes reproductora/postura + engorde activos en el galpón).
        var lotesAfectados =
            await _ctx.Lotes.CountAsync(l => l.GalponId == dto.GalponId && l.DeletedAt == null) +
            await _ctx.LoteAveEngorde.CountAsync(l => l.GalponId == dto.GalponId && l.DeletedAt == null);

        // Cascada atómica: galpón + todas las tablas que denormalizan su ubicación.
        await _ctx.Database.ExecuteSqlRawAsync(
            "SELECT public.fn_mover_galpon({0}::varchar, {1}, {2}::varchar, {3})",
            dto.GalponId, dto.GranjaDestinoId, nucleoDest, _current.UserId);

        return new MoverResultDto(
            true,
            $"Galpón movido correctamente. {lotesAfectados} lote(s) reubicado(s).",
            GalponesAfectados: 1,
            LotesAfectados: lotesAfectados);
    }
}
