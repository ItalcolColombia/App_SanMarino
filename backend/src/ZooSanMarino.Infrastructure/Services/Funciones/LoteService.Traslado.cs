// file: src/ZooSanMarino.Infrastructure/Services/Funciones/LoteService.Traslado.cs
// Traslado de un lote a otra granja y su historial.
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

using ZooSanMarino.Application.Calculos;       // GuiaGeneticaRequisitoCalculos (logica pura)
using ZooSanMarino.Application.DTOs;           // LoteDto, Create/Update
using ZooSanMarino.Application.DTOs.Lotes;     // LoteDetailDto, LoteSearchRequest, TrasladoLoteRequestDto, TrasladoLoteResponseDto, HistorialTrasladoLoteDto
using CommonDtos = ZooSanMarino.Application.DTOs.Common;
using AppInterfaces = ZooSanMarino.Application.Interfaces;

using FarmLiteDto   = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

using ZooSanMarino.Domain.Entities;
using HistorialTrasladoLote = ZooSanMarino.Domain.Entities.HistorialTrasladoLote;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class LoteService
{
    public async Task<TrasladoLoteResponseDto> TrasladarLoteAsync(TrasladoLoteRequestDto dto)
    {
        // Alcance granular (fix QA M1): el lote ORIGEN debe estar en el cierre del usuario
        // (el DESTINO se elige libre por diseño: paraDestino).
        if (!await _scopeResolver.PermiteLoteAsync(dto.LoteId))
            throw new InvalidOperationException(
                "Tu acceso a esta granja está restringido: el lote a trasladar está fuera de tu alcance asignado.");

        var companyId = await GetEffectiveCompanyIdAsync();
        // 1. Validar y obtener el lote original
        var loteOriginal = await _ctx.Lotes
            .Include(l => l.Farm)
            .SingleOrDefaultAsync(x =>
                x.LoteId == dto.LoteId &&
                x.CompanyId == companyId &&
                x.DeletedAt == null);

        if (loteOriginal == null)
        {
            throw new InvalidOperationException($"No se encontró el lote con ID {dto.LoteId} o no pertenece a la compañía actual.");
        }

        // 2. Validar que no sea el mismo lote (misma granja)
        if (loteOriginal.GranjaId == dto.GranjaDestinoId)
        {
            throw new InvalidOperationException("No se puede trasladar un lote a la misma granja.");
        }

        // 3. Validar que la granja destino existe y pertenece a la compañía
        var granjaDestino = await _ctx.Farms
            .AsNoTracking()
            .SingleOrDefaultAsync(f =>
                f.Id == dto.GranjaDestinoId &&
                f.CompanyId == companyId);

        if (granjaDestino == null)
        {
            throw new InvalidOperationException($"La granja destino con ID {dto.GranjaDestinoId} no existe o no pertenece a la compañía actual.");
        }

        // 5. Validar núcleo destino si se proporciona
        if (!string.IsNullOrWhiteSpace(dto.NucleoDestinoId))
        {
            var nucleoDestino = await _ctx.Nucleos
                .AsNoTracking()
                .SingleOrDefaultAsync(n =>
                    n.NucleoId == dto.NucleoDestinoId &&
                    n.GranjaId == dto.GranjaDestinoId);

            if (nucleoDestino == null)
            {
                throw new InvalidOperationException($"El núcleo destino con ID {dto.NucleoDestinoId} no existe en la granja destino.");
            }
        }

        // 6. Validar galpón destino si se proporciona
        if (!string.IsNullOrWhiteSpace(dto.GalponDestinoId))
        {
            var galponDestino = await _ctx.Galpones
                .AsNoTracking()
                .SingleOrDefaultAsync(g =>
                    g.GalponId == dto.GalponDestinoId &&
                    g.CompanyId == companyId);

            if (galponDestino == null)
            {
                throw new InvalidOperationException($"El galpón destino con ID {dto.GalponDestinoId} no existe o no pertenece a la compañía actual.");
            }

            if (galponDestino.GranjaId != dto.GranjaDestinoId)
            {
                throw new InvalidOperationException("El galpón destino no pertenece a la granja destino.");
            }

            if (!string.IsNullOrWhiteSpace(dto.NucleoDestinoId) &&
                !string.Equals(galponDestino.NucleoId, dto.NucleoDestinoId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("El galpón destino no pertenece al núcleo destino indicado.");
            }
        }

        var loteId = loteOriginal.LoteId ?? 0;
        var granjaOrigenId = loteOriginal.GranjaId;

        // 7. Calcular edad en semanas (desde fecha encaset) para decidir si actualizar Levante o Producción
        int edadSemanas = 0;
        if (loteOriginal.FechaEncaset.HasValue)
        {
            var dias = (DateTime.UtcNow.Date - loteOriginal.FechaEncaset.Value.Date).TotalDays;
            edadSemanas = (int)Math.Floor(dias / 7.0);
            if (edadSemanas < 0) edadSemanas = 0;
        }

        // 8. Actualizar el mismo lote: granja, núcleo y galpón destino; estado "lote_transferido"
        loteOriginal.GranjaId = dto.GranjaDestinoId;
        loteOriginal.NucleoId = dto.NucleoDestinoId ?? null;
        loteOriginal.GalponId = dto.GalponDestinoId ?? null;
        loteOriginal.EstadoTraslado = "lote_transferido";
        loteOriginal.UpdatedByUserId = _current.UserId;
        loteOriginal.UpdatedAt = DateTime.UtcNow;

        // 9. Según fase: actualizar solo LotePosturaLevante (< 26 sem) o solo LotePosturaProducción (>= 26 sem)
        //    - Levante (< 26): el lote sigue en levante; actualizar LPL (granja, núcleo, galpón).
        //    - Producción (>= 26): el lote ya pasó a producción; actualizar LPP; no tocar LPL (queda en granja de origen como historial).
        if (edadSemanas < 26)
        {
            var lpls = await _ctx.LotePosturaLevante
                .Where(l => l.LoteId == loteId && l.DeletedAt == null)
                .ToListAsync();
            foreach (var lpl in lpls)
            {
                lpl.GranjaId = dto.GranjaDestinoId;
                lpl.NucleoId = dto.NucleoDestinoId ?? null;
                lpl.GalponId = dto.GalponDestinoId ?? null;
                lpl.EstadoTraslado = "lote_transferido";
                lpl.UpdatedByUserId = _current.UserId;
                lpl.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            var lpps = await _ctx.LotePosturaProduccion
                .Where(l => l.LoteId == loteId && l.DeletedAt == null)
                .ToListAsync();
            foreach (var lpp in lpps)
            {
                lpp.GranjaId = dto.GranjaDestinoId;
                lpp.NucleoId = dto.NucleoDestinoId ?? null;
                lpp.GalponId = dto.GalponDestinoId ?? null;
                lpp.EstadoTraslado = "lote_transferido";
                lpp.UpdatedByUserId = _current.UserId;
                lpp.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _ctx.SaveChangesAsync();

        // 9. Registrar en el historial de traslados (mismo lote: origen y destino es el mismo registro movido)
        var historial = new HistorialTrasladoLote
        {
            LoteOriginalId = loteId,
            LoteNuevoId = loteId,
            GranjaOrigenId = granjaOrigenId,
            GranjaDestinoId = dto.GranjaDestinoId,
            NucleoDestinoId = dto.NucleoDestinoId,
            GalponDestinoId = dto.GalponDestinoId,
            Observaciones = dto.Observaciones,
            CompanyId = companyId,
            CreatedByUserId = _current.UserId,
            CreatedAt = DateTime.UtcNow,
            // El día del hecho lo elige quien registra; sin dato, hoy.
            FechaTraslado = dto.FechaTraslado ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _ctx.HistorialTrasladoLote.Add(historial);
        await _ctx.SaveChangesAsync();

        // 10. Respuesta (origen = granja antes del cambio; destino = granja destino)
        var granjaOrigenNombre = loteOriginal.Farm?.Name ?? "N/A";

        return new TrasladoLoteResponseDto
        {
            Success = true,
            Message = $"Lote trasladado exitosamente de '{granjaOrigenNombre}' a '{granjaDestino.Name}'.",
            LoteOriginalId = loteId,
            LoteNuevoId = loteId,
            LoteNombre = loteOriginal.LoteNombre,
            GranjaOrigen = granjaOrigenNombre,
            GranjaDestino = granjaDestino.Name
        };
    }

    public async Task<IEnumerable<HistorialTrasladoLoteDto>> GetHistorialTrasladosAsync(int loteId)
    {
        // Alcance granular (fix QA M2): acceso directo por loteId respeta el scope (fail-closed → vacío)
        if (!await _scopeResolver.PermiteLoteAsync(loteId))
            return Array.Empty<HistorialTrasladoLoteDto>();

        var companyId = await GetEffectiveCompanyIdAsync();
        var historiales = await _ctx.HistorialTrasladoLote
            .AsNoTracking()
            .Where(h => 
                (h.LoteOriginalId == loteId || h.LoteNuevoId == loteId) &&
                h.CompanyId == companyId)
            .OrderByDescending(h => h.CreatedAt)
            .Include(h => h.GranjaOrigen)
            .Include(h => h.GranjaDestino)
            .ToListAsync();

        var result = new List<HistorialTrasladoLoteDto>();

        foreach (var h in historiales)
        {
            // Obtener nombres de núcleo y galpón si existen
            string? nucleoNombre = null;
            if (!string.IsNullOrWhiteSpace(h.NucleoDestinoId))
            {
                var nucleo = await _ctx.Nucleos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.NucleoId == h.NucleoDestinoId);
                nucleoNombre = nucleo?.NucleoNombre;
            }

            string? galponNombre = null;
            if (!string.IsNullOrWhiteSpace(h.GalponDestinoId))
            {
                var galpon = await _ctx.Galpones
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.GalponId == h.GalponDestinoId);
                galponNombre = galpon?.GalponNombre;
            }

            // Obtener nombre del usuario (CreatedByUserId es int, pero User.Id es Guid)
            // Por ahora, no podemos hacer la relación directa, así que usamos un valor por defecto
            // TODO: Si se necesita el nombre del usuario, se podría crear una tabla de mapeo o cambiar el sistema
            var nombreUsuario = $"Usuario ID: {h.CreatedByUserId}";

            result.Add(new HistorialTrasladoLoteDto(
                h.Id,
                h.LoteOriginalId,
                h.LoteNuevoId,
                h.GranjaOrigenId,
                h.GranjaOrigen?.Name ?? "N/A",
                h.GranjaDestinoId,
                h.GranjaDestino?.Name ?? "N/A",
                h.NucleoDestinoId,
                nucleoNombre,
                h.GalponDestinoId,
                galponNombre,
                h.Observaciones,
                h.CreatedByUserId,
                nombreUsuario,
                h.CreatedAt
            ));
        }

        return result;
    }

    /// <summary>
    /// Verifica si un lote es descendiente de otro (para evitar ciclos)
    /// </summary>
    private async Task<bool> EsDescendienteAsync(int loteIdActual, int loteIdPadre)
    {
        var companyId = await GetEffectiveCompanyIdAsync();
        return await EsDescendienteAsyncInternal(loteIdActual, loteIdPadre, companyId);
    }

    private async Task<bool> EsDescendienteAsyncInternal(int loteIdActual, int loteIdPadre, int companyId)
    {
        // Obtener todos los hijos del lote actual
        var hijos = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LotePadreId == loteIdActual &&
                       l.CompanyId == companyId &&
                       l.DeletedAt == null)
            .Select(l => l.LoteId)
            .ToListAsync();

        // Si el lote padre está en la lista de hijos, es un descendiente
        if (hijos.Contains(loteIdPadre))
            return true;

        // Verificar recursivamente en los hijos
        foreach (var hijoId in hijos.Where(h => h.HasValue).Select(h => h!.Value))
        {
            var esDescendiente = await EsDescendienteAsyncInternal(hijoId, loteIdPadre, companyId);
            if (esDescendiente)
                return true;
        }

        return false;
    }
}
