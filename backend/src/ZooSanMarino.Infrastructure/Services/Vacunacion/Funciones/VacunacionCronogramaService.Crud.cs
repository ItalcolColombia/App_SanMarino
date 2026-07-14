// Vacunacion/Funciones/VacunacionCronogramaService.Crud.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionCronogramaService
{
    private readonly record struct LoteResuelto(int GranjaId, string? NucleoId, string? GalponId, DateTime? FechaEncaset, string LoteNombre);

    private async Task<LoteResuelto> ResolverLoteAsync(string lineaProductiva, int loteId, CancellationToken ct)
    {
        switch (lineaProductiva)
        {
            case "Levante":
            {
                var lpl = await _ctx.LotePosturaLevante.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.LotePosturaLevanteId == loteId && x.CompanyId == _currentUser.CompanyId, ct)
                    ?? throw new InvalidOperationException($"Lote Levante {loteId} no existe o no pertenece a la empresa activa.");
                return new LoteResuelto(lpl.GranjaId, lpl.NucleoId, lpl.GalponId, lpl.FechaEncaset, lpl.LoteNombre);
            }
            case "Produccion":
            {
                var lpp = await _ctx.LotePosturaProduccion.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.LotePosturaProduccionId == loteId && x.CompanyId == _currentUser.CompanyId, ct)
                    ?? throw new InvalidOperationException($"Lote Producción {loteId} no existe o no pertenece a la empresa activa.");
                return new LoteResuelto(lpp.GranjaId, lpp.NucleoId, lpp.GalponId, lpp.FechaEncaset, lpp.LoteNombre);
            }
            case "Engorde":
            {
                var lae = await _ctx.LoteAveEngorde.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.LoteAveEngordeId == loteId && x.CompanyId == _currentUser.CompanyId, ct)
                    ?? throw new InvalidOperationException($"Lote Engorde {loteId} no existe o no pertenece a la empresa activa.");
                return new LoteResuelto(lae.GranjaId, lae.NucleoId, lae.GalponId, lae.FechaEncaset, lae.LoteNombre);
            }
            default:
                throw new InvalidOperationException($"lineaProductiva inválida: '{lineaProductiva}'.");
        }
    }

    private async Task<string?> ResolverGranjaNombreAsync(int granjaId, CancellationToken ct)
        => (await _ctx.Farms.AsNoTracking().FirstOrDefaultAsync(f => f.Id == granjaId, ct))?.Name;

    /// <inheritdoc />
    public async Task<VacunacionCronogramaItemDto> CreateAsync(VacunacionCronogramaItemCreateRequest req, CancellationToken ct = default)
    {
        if (!LineasValidas.Contains(req.LineaProductiva))
            throw new InvalidOperationException($"lineaProductiva inválida: '{req.LineaProductiva}'.");
        ValidarUnidadObjetivo(req.UnidadObjetivo, req.ValorObjetivo, req.FechaObjetivo);

        var lote = await ResolverLoteAsync(req.LineaProductiva, req.LoteId, ct);

        var vacuna = await _ctx.ItemInventario.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.ItemInventarioId && x.CompanyId == _currentUser.CompanyId, ct)
            ?? throw new InvalidOperationException($"Vacuna (ItemInventario) {req.ItemInventarioId} no existe o no pertenece a la empresa activa.");

        var entity = new VacunacionCronogramaItem
        {
            CompanyId = _currentUser.CompanyId,
            PaisId = _currentUser.PaisId,
            LineaProductiva = req.LineaProductiva,
            GranjaId = lote.GranjaId,
            NucleoId = lote.NucleoId,
            GalponId = lote.GalponId,
            ItemInventarioId = req.ItemInventarioId,
            UnidadObjetivo = req.UnidadObjetivo,
            ValorObjetivo = req.ValorObjetivo,
            FechaObjetivo = req.FechaObjetivo,
            RangoDiasAntes = req.RangoDiasAntes,
            RangoDiasDespues = req.RangoDiasDespues,
            Orden = req.Orden,
            Activo = true,
            Notas = req.Notas,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };
        switch (req.LineaProductiva)
        {
            case "Levante": entity.LotePosturaLevanteId = req.LoteId; break;
            case "Produccion": entity.LotePosturaProduccionId = req.LoteId; break;
            case "Engorde": entity.LoteAveEngordeId = req.LoteId; break;
        }

        _ctx.VacunacionCronogramaItem.Add(entity);
        await _ctx.SaveChangesAsync(ct);

        var granjaNombre = await ResolverGranjaNombreAsync(entity.GranjaId, ct);
        return MapItem(entity, lote.FechaEncaset, lote.LoteNombre, granjaNombre, vacuna.Nombre);
    }

    /// <inheritdoc />
    public async Task<VacunacionCronogramaItemDto?> UpdateAsync(int id, VacunacionCronogramaItemUpdateRequest req, CancellationToken ct = default)
    {
        ValidarUnidadObjetivo(req.UnidadObjetivo, req.ValorObjetivo, req.FechaObjetivo);

        var entity = await _ctx.VacunacionCronogramaItem
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId, ct);
        if (entity is null) return null;

        var vacuna = await _ctx.ItemInventario.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.ItemInventarioId && x.CompanyId == _currentUser.CompanyId, ct)
            ?? throw new InvalidOperationException($"Vacuna (ItemInventario) {req.ItemInventarioId} no existe o no pertenece a la empresa activa.");

        entity.ItemInventarioId = req.ItemInventarioId;
        entity.UnidadObjetivo = req.UnidadObjetivo;
        entity.ValorObjetivo = req.ValorObjetivo;
        entity.FechaObjetivo = req.FechaObjetivo;
        entity.RangoDiasAntes = req.RangoDiasAntes;
        entity.RangoDiasDespues = req.RangoDiasDespues;
        entity.Orden = req.Orden;
        entity.Activo = req.Activo;
        entity.Notas = req.Notas;
        entity.UpdatedByUserId = _currentUser.UserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);

        var lote = await ResolverLoteAsync(entity.LineaProductiva,
            entity.LotePosturaLevanteId ?? entity.LotePosturaProduccionId ?? entity.LoteAveEngordeId!.Value, ct);
        var granjaNombre = await ResolverGranjaNombreAsync(entity.GranjaId, ct);
        return MapItem(entity, lote.FechaEncaset, lote.LoteNombre, granjaNombre, vacuna.Nombre);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _ctx.VacunacionCronogramaItem
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == _currentUser.CompanyId, ct);
        if (entity is null) return false;

        _ctx.VacunacionCronogramaItem.Remove(entity); // cascade elimina el registro de aplicación (1:1) si existe
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}
