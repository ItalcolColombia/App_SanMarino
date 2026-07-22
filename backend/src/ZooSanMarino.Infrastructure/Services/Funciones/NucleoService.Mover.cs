// file: src/ZooSanMarino.Infrastructure/Services/Funciones/NucleoService.Mover.cs
// Mover un núcleo (re-key) a otra granja arrastrando galpones y lotes. Cascada transaccional
// insert-repoint-delete en fn_rekey_nucleo (la granja es parte de la PK del núcleo).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs; // MoverNucleoDto, MoverResultDto

namespace ZooSanMarino.Infrastructure.Services
{
    public partial class NucleoService
    {
        /// <inheritdoc />
        public async Task<MoverResultDto> MoverAsync(MoverNucleoDto dto)
        {
            var companyId = await GetEffectiveCompanyIdAsync();

            if (dto.GranjaOrigenId == dto.GranjaDestinoId)
                throw new InvalidOperationException("La granja destino es la misma que la de origen.");

            var nucleo = await _ctx.Nucleos.AsNoTracking()
                .SingleOrDefaultAsync(n => n.NucleoId == dto.NucleoId &&
                                           n.GranjaId == dto.GranjaOrigenId &&
                                           n.CompanyId == companyId);
            if (nucleo is null || nucleo.DeletedAt != null)
                throw new InvalidOperationException("El núcleo no existe en la granja origen o no pertenece a la compañía.");

            // La granja destino debe existir y pertenecer a la misma empresa (bloquea mover entre empresas).
            await EnsureFarmExists(dto.GranjaDestinoId);

            // Colisión: mismo NucleoId ya presente en la granja destino.
            var colision = await _ctx.Nucleos.AsNoTracking()
                .AnyAsync(n => n.NucleoId == dto.NucleoId && n.GranjaId == dto.GranjaDestinoId);
            if (colision)
                throw new InvalidOperationException(
                    $"Ya existe un núcleo con Id '{dto.NucleoId}' en la granja destino. Renómbrelo antes de mover.");

            // Impacto para el mensaje.
            var galponesAfectados = await _ctx.Galpones.CountAsync(g =>
                g.NucleoId == dto.NucleoId && g.GranjaId == dto.GranjaOrigenId && g.DeletedAt == null);
            var lotesAfectados =
                await _ctx.Lotes.CountAsync(l => l.NucleoId == dto.NucleoId && l.GranjaId == dto.GranjaOrigenId && l.DeletedAt == null) +
                await _ctx.LoteAveEngorde.CountAsync(l => l.NucleoId == dto.NucleoId && l.GranjaId == dto.GranjaOrigenId && l.DeletedAt == null);

            // Cascada atómica insert-repoint-delete (colisión/inexistencia → RAISE → InvalidOperationException).
            await _ctx.Database.ExecuteSqlRawAsync(
                "SELECT public.fn_rekey_nucleo({0}::varchar, {1}, {2}, {3})",
                dto.NucleoId, dto.GranjaOrigenId, dto.GranjaDestinoId, _current.UserId);

            return new MoverResultDto(
                true,
                $"Núcleo movido a la granja destino. {galponesAfectados} galpón(es) y {lotesAfectados} lote(s) reubicados.",
                GalponesAfectados: galponesAfectados,
                LotesAfectados: lotesAfectados);
        }
    }
}
